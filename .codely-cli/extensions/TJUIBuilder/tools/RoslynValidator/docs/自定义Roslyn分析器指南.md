# 自定义 Roslyn 分析器指南

> 本文档面向 RoslynValidator 工具，指导如何新增自定义编译规则。
> 即使代码没有编译错误，也能通过分析器报 Error/Warning。

---

## 目录

1. [整体架构](#1-整体架构)
2. [规则分类与写法](#2-规则分类与写法)
3. [SyntaxKind 速查表](#3-syntaxkind-速查表)
4. [SymbolKind 速查表](#4-symbolkind-速查表)
5. [SemanticModel 常用 API](#5-semanticmodel-常用-api)
6. [类型比较与符号判断](#6-类型比较与符号判断)
7. [JSON 规则配置格式](#7-json-规则配置格式)
8. [在 RoslynValidator 中集成新规则](#8-在-roslynvalidator-中集成新规则)
9. [完整示例：通用规则分析器](#9-完整示例通用规则分析器)
10. [常见问题](#10-常见问题)

---

## 1. 整体架构

### 1.1 分析器生命周期

```
DiagnosticAnalyzer.Initialize()          ← 入口：注册"我对什么感兴趣"
       │
       ├─ RegisterSyntaxNodeAction()     ← 按语法节点类型注册（最常用）
       ├─ RegisterSymbolAction()         ← 按符号类型注册（类/方法/字段）
       ├─ RegisterOperationAction()      ← 按 IL 操作注册（语言无关）
       └─ RegisterCompilationStartAction() ← 编译开始时注册（延迟注册/条件注册）
              │
              ▼
Roslyn 编译器遍历语法树/符号表
       │
       ▼ 匹配到已注册的节点/符号时回调
callback(SyntaxNodeAnalysisContext / SymbolAnalysisContext)
       │
       ├─ 用 SemanticModel 做语义判断
       └─ context.ReportDiagnostic()     ← 报告诊断
```

### 1.2 RoslynValidator 中的集成点

当前 `Program.cs` 中 `PerformSemanticAnalysis` 方法：

```csharp
// 当前代码
var compilation = CSharpCompilation.Create(...);
var allDiagnostics = compilation.GetDiagnostics();

// 改为支持分析器
var analyzers = LoadCustomAnalyzers();  // 加载自定义分析器
if (analyzers.Length > 0)
{
    var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
    allDiagnostics = compilationWithAnalyzers.GetAllDiagnostics();
}
else
{
    allDiagnostics = compilation.GetDiagnostics();
}
```

### 1.3 核心概念区分

| 概念 | 层级 | 说明 |
|------|------|------|
| **SyntaxNode** | 语法层 | 源码的树形结构表示，如 `InvocationExpressionSyntax` |
| **SemanticModel** | 语义层 | 连接语法树与符号表的桥梁，可查询类型、解析引用 |
| **ISymbol** | 符号层 | 编译器解析出的声明实体，如 `IMethodSymbol`、`IPropertySymbol` |
| **Compilation** | 编译层 | 完整的编译上下文，可获取所有引用的类型 |

---

## 2. 规则分类与写法

### 2.1 方法调用禁止（method-invocation）

**场景**：禁止 `GameObject.Find`、`FindObjectOfType` 等 API 调用。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ForbiddenMethodAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR001",
        title: "禁止使用 GameObject.Find",
        messageFormat: "不允许使用 '{0}'，请使用引用或依赖注入替代",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        // 跳过无法解析语义的情况
        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol method) return;

        // 匹配：containingType 为 GameObject，方法名为 Find
        if (method.ContainingType.Name == "GameObject" && method.Name == "Find")
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), "GameObject.Find"));
        }
    }
}
```

**检测目标代码**：
```csharp
var player = GameObject.Find("Player");   // ← 报 UR001 Error
var obj = FindObjectOfType<Player>();      // ← 不匹配（非 GameObject 类型方法）
```

### 2.2 成员访问禁止（member-access）

**场景**：禁止直接访问 `transform`、`gameObject` 等快捷属性。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ForbiddenMemberAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR003",
        title: "禁止直接访问 transform",
        messageFormat: "禁止直接访问 '{0}'，请使用缓存引用",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
    {
        var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;

        var symbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol == null) return;

        // 匹配属性或字段名为 "transform"，且所属类型继承自 Component
        if ((symbol is IPropertySymbol || symbol is IFieldSymbol) && symbol.Name == "transform")
        {
            var containingType = symbol.ContainingType;
            var componentType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.Component");
            if (componentType != null && InheritsFrom(containingType, componentType))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), "transform"));
            }
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

**检测目标代码**：
```csharp
var pos = transform.position;        // ← 报 UR003 Error
var pos2 = GetComponent<Transform>(); // ← 不触发（不是成员访问）
var transform = 5;                   // ← 不触发（是局部变量，非成员访问）
```

### 2.3 类型使用禁止（type-reference）

**场景**：禁止 `new GameObject()`、禁止使用特定类型。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ForbiddenTypeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR004",
        title: "禁止直接 new GameObject",
        messageFormat: "禁止使用 new {0}()，请使用工厂方法",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // 对象创建表达式
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        // 也检测 typeof() 表达式
        context.RegisterSyntaxNodeAction(AnalyzeTypeOf, SyntaxKind.TypeOfExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var creation = (ObjectCreationExpressionSyntax)ctx.Node;
        var type = ctx.SemanticModel.GetTypeInfo(creation).Type;

        var gameObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");
        if (gameObjectType == null) return;

        if (type != null && SymbolEqualityComparer.Default.Equals(type, gameObjectType))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation(), "GameObject"));
        }
    }

    private static void AnalyzeTypeOf(SyntaxNodeAnalysisContext ctx)
    {
        var typeOfExpr = (TypeOfExpressionSyntax)ctx.Node;
        var type = ctx.SemanticModel.GetTypeInfo(typeOfExpr.Type).Type;

        var gameObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");
        if (gameObjectType == null) return;

        if (type != null && SymbolEqualityComparer.Default.Equals(type, gameObjectType))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, typeOfExpr.GetLocation(), "GameObject"));
        }
    }
}
```

### 2.4 基类/接口继承检测（inheritance）

**场景**：要求 MonoBehaviour 子类必须实现特定接口、禁止多重继承等。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class InheritanceRuleAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR005",
        title: "MonoBehaviour 子类必须实现 IInitializable",
        messageFormat: "类 '{0}' 继承自 MonoBehaviour 但未实现 IInitializable",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // RegisterSymbolAction 不依赖语法，直接在符号层工作
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext ctx)
    {
        var type = (INamedTypeSymbol)ctx.Symbol;

        // 只检查 class
        if (type.TypeKind != TypeKind.Class) return;

        // 检查是否继承自 MonoBehaviour
        var monoType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
        if (monoType == null) return;
        if (!InheritsFrom(type, monoType)) return;

        // 检查是否实现了 IInitializable
        var interfaceType = ctx.Compilation.GetTypeByMetadataName("MyGame.IInitializable");
        if (interfaceType == null) return;

        bool implementsInterface = type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, interfaceType));

        if (!implementsInterface)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

### 2.5 特性标注检测（attribute）

**场景**：要求 private 字段必须有 `[SerializeField]`、禁止使用 `[Obsolete]` 等。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SerializeFieldRuleAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR006",
        title: "private 字段必须标注 [SerializeField]",
        messageFormat: "private 字段 '{0}' 未标注 [SerializeField]，请显式声明序列化意图",
        category: "Convention",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
    }

    private static void AnalyzeField(SymbolAnalysisContext ctx)
    {
        var field = (IFieldSymbol)ctx.Symbol;

        // 只检查 private 非 static 非 const 字段
        if (field.DeclaredAccessibility != Accessibility.Private) return;
        if (field.IsStatic) return;
        if (field.IsConst) return;

        // 检查是否已有 [SerializeField]
        bool hasSerializeField = field.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "SerializeField" ||
            a.AttributeClass?.Name == "SerializeFieldAttribute");

        if (!hasSerializeField)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, field.Locations[0], field.Name));
        }
    }
}
```

### 2.6 数据流分析（dataflow）

**场景**：变量声明后未被使用、变量可声明为 const 等。

```csharp
private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext ctx)
{
    var localDecl = (LocalDeclarationStatementSyntax)ctx.Node;

    // 跳过已有 const 的声明
    if (localDecl.Modifiers.Any(SyntaxKind.ConstKeyword)) return;

    // 数据流分析：检查变量是否在声明后被写入
    var dataFlow = ctx.SemanticModel.AnalyzeDataFlow(localDecl);
    if (dataFlow == null) return;

    foreach (var variable in localDecl.Declaration.Variables)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(variable, ctx.CancellationToken);
        if (symbol == null) continue;

        // 变量在声明外部被写入 → 不能为 const
        if (dataFlow.WrittenOutside.Contains(symbol)) continue;

        // 检查初始化值是否为编译期常量
        if (variable.Initializer?.Value != null)
        {
            var constantValue = ctx.SemanticModel.GetConstantValue(variable.Initializer.Value);
            if (constantValue.HasValue)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, variable.GetLocation(), variable.Identifier.ValueText));
            }
        }
    }
}
```

### 2.7 ✅ null 检查模式（unity-null-check）

**场景**：禁止对 `UnityEngine.Object` 派生类使用 `== null` / `!= null` 比较（重载了 `operator==`，行为与 C# 标准 null 不同）。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnityNullCheckAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR010",
        title: "禁止对 UnityEngine.Object 使用 == null",
        messageFormat: "对 '{0}' 使用 == null 比较不可靠，UnityEngine.Object 重载了 operator==，请使用 is null 或参考 bool 转换",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeEqualsNull, SyntaxKind.EqualsExpression);
        context.RegisterSyntaxNodeAction(AnalyzeNotEqualsNull, SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeEqualsNull(SyntaxNodeAnalysisContext ctx)
    {
        var binary = (BinaryExpressionSyntax)ctx.Node;
        // 只检查 "expr == null" 模式
        if (!binary.IsKind(SyntaxKind.EqualsExpression)) return;

        // 检查左侧是否为 null literal
        bool leftIsNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        bool rightIsNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (!leftIsNull && !rightIsNull) return;

        // 获取被比较侧的表达式类型
        var comparedExpr = leftIsNull ? binary.Right : binary.Left;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(comparedExpr);
        if (typeInfo.Type == null) return;

        // 检查是否继承自 UnityEngine.Object
        var unityObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.Object");
        if (unityObjectType == null) return;
        if (!IsOrInheritsFrom(typeInfo.Type, unityObjectType)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(),
            typeInfo.Type.Name));
    }

    private static void AnalyzeNotEqualsNull(SyntaxNodeAnalysisContext ctx)
    {
        var binary = (BinaryExpressionSyntax)ctx.Node;
        bool leftIsNull = binary.Left.IsKind(SyntaxKind.NullLiteralExpression);
        bool rightIsNull = binary.Right.IsKind(SyntaxKind.NullLiteralExpression);
        if (!leftIsNull && !rightIsNull) return;

        var comparedExpr = leftIsNull ? binary.Right : binary.Left;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(comparedExpr);
        if (typeInfo.Type == null) return;

        var unityObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.Object");
        if (unityObjectType == null) return;
        if (!IsOrInheritsFrom(typeInfo.Type, unityObjectType)) return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(),
            typeInfo.Type.Name));
    }

    private static bool IsOrInheritsFrom(ITypeSymbol? type, ITypeSymbol expected)
    {
        if (type == null) return false;
        return SymbolEqualityComparer.Default.Equals(type, expected)
            || (type is INamedTypeSymbol named && InheritsFrom(named, expected));
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

**检测目标代码**：
```csharp
var go = GetComponent<Rigidbody>();
if (go == null) { }           // ← 报 UR010 Warning（UnityEngine.Object 子类）
if (go != null) { }           // ← 报 UR010 Warning

object obj = new object();
if (obj == null) { }          // ← 不触发（非 UnityEngine.Object 类型）
```

**说明**：`UnityEngine.Object` 重载了 `operator==`，销毁后的对象通过 `== null` 返回 `true`，但实际引用仍非 null。应使用 `obj is null`（C# 9+）或利用 Unity 的隐式 bool 转换 `if (obj)`。

### 2.8 Update 中的性能反模式（update-performance）

**场景**：禁止在 `Update` / `FixedUpdate` / `LateUpdate` 等方法中调用 `GetComponent`、`Find` 等重型 API。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UpdatePerformanceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR011",
        title: "禁止在 Update 中调用 GetComponent",
        messageFormat: "在 '{0}' 方法中调用 '{1}' 会造成每帧性能损耗，请将结果缓存在 Awake/Start 中",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // 每帧回调方法名
    private static readonly HashSet<string> PerFrameMethods = new()
    {
        "Update", "FixedUpdate", "LateUpdate", "OnRenderObject", "OnGUI"
    };

    // 需要禁止的重型 API
    private static readonly HashSet<string> HeavyApis = new()
    {
        "GetComponent", "GetComponents", "GetComponentInChildren",
        "GetComponentsInChildren", "GetComponentInParent", "GetComponentsInParent",
        "Find", "FindObjectOfType", "FindObjectsOfType",
        "FindFirstObjectByType", "FindAnyObjectByType",
        "GetComponentInChildren", "GetComponentsInChildren",
        "GetComponentInParent", "GetComponentsInParent"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        // 获取所在方法
        var enclosingMethod = invocation.Ancestors()
            .OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (enclosingMethod == null) return;

        // 检查是否在每帧回调方法中
        if (!PerFrameMethods.Contains(enclosingMethod.Identifier.ValueText))
            return;

        // 检查方法名是否在重型 API 列表中
        var methodSymbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (methodSymbol is not IMethodSymbol method) return;
        if (!HeavyApis.Contains(method.Name)) return;

        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(),
            enclosingMethod.Identifier.ValueText, method.Name);
        ctx.ReportDiagnostic(diagnostic);
    }
}
```

**检测目标代码**：
```csharp
void Update()
{
    var rb = GetComponent<Rigidbody>();      // ← 报 UR011 Warning
    var cols = GetComponentsInChildren<Collider>(); // ← 报 UR011 Warning
    var obj = FindObjectOfType<Player>();    // ← 报 UR011 Warning
}

void Awake()
{
    var rb = GetComponent<Rigidbody>();      // ← 不触发（不在每帧回调中）
}
```

### 2.9 字符串 Tag 比较（string-tag-compare）

**场景**：禁止使用 `==` 直接比较 Tag 字符串，应使用 `CompareTag()`。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TagComparisonAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR012",
        title: "禁止使用 == 比较 Tag 字符串",
        messageFormat: "使用 '{0}' 直接比较 Tag 字符串，请改用 CompareTag() 方法（性能更优且无 GC 分配）",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTagCompare, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeTagCompare(SyntaxNodeAnalysisContext ctx)
    {
        var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;

        // 检查是否为 .tag 属性访问
        if (memberAccess.Name.Identifier.ValueText != "tag") return;

        // 检查 .tag 是否来自 UnityEngine.Component 或 GameObject
        var symbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType;
        var componentType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.Component");
        var gameObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");

        bool isUnityType = (componentType != null && InheritsFrom(containingType, componentType))
            || (gameObjectType != null && SymbolEqualityComparer.Default.Equals(containingType, gameObjectType));
        if (!isUnityType) return;

        // 检查父节点是否为 EqualsExpression（== 比较）
        var parent = memberAccess.Parent;
        if (parent is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.EqualsExpression))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, binary.GetLocation(),
                memberAccess.ToString()));
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

**检测目标代码**：
```csharp
if (collider.tag == "Player") { }    // ← 报 UR012 Warning
if (gameObject.tag == "Enemy") { }   // ← 报 UR012 Warning
if (CompareTag("Player")) { }        // ← 不触发
if (myCustomObj.tag == "abc") { }    // ← 不触发（非 Unity 类型）
```

### 2.10 对象创建构造函数检测（object-creation-constructor）

**场景**：禁止 `new GameObject("name")` 字符串构造、`new MonoBehaviour()` 等。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ObjectCreationConstructorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor GameObjectCtorRule = new(
        id: "UR013",
        title: "禁止使用 new GameObject(string)",
        messageFormat: "使用 new GameObject('{0}') 字符串构造不利于资源追踪，请使用预制体实例化",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MonoCtorRule = new(
        id: "UR014",
        title: "禁止直接 new MonoBehaviour",
        messageFormat: "禁止使用 new {0}()，MonoBehaviour 只能通过 AddComponent 挂载到 GameObject",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(GameObjectCtorRule, MonoCtorRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
    {
        var creation = (ObjectCreationExpressionSyntax)ctx.Node;
        var typeInfo = ctx.SemanticModel.GetTypeInfo(creation);
        var type = typeInfo.Type;
        if (type == null) return;

        // 检查是否为 GameObject
        var goType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");
        var monoType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");

        if (goType != null && SymbolEqualityComparer.Default.Equals(type, goType))
        {
            // 进一步检查：是否使用了字符串参数的构造函数
            var args = creation.ArgumentList?.Arguments;
            if (args != null && args.Count > 0)
            {
                var firstArgType = ctx.SemanticModel.GetTypeInfo(
                    args[0].Expression).Type;
                if (firstArgType?.SpecialType == SpecialType.System_String)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        GameObjectCtorRule, creation.GetLocation(), args[0]));
                }
            }
        }

        if (monoType != null && IsOrInheritsFrom(type, monoType))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                MonoCtorRule, creation.GetLocation(), type.Name));
        }
    }

    private static bool IsOrInheritsFrom(ITypeSymbol? type, ITypeSymbol expected)
    {
        if (type == null) return false;
        return SymbolEqualityComparer.Default.Equals(type, expected)
            || (type is INamedTypeSymbol named && InheritsFrom(named, expected));
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

**检测目标代码**：
```csharp
var go = new GameObject("Player");      // ← 报 UR013 Warning
var comp = new MyComponent();            // ← 报 UR014 Error（MonoBehaviour 子类）
var go2 = new GameObject();              // ← 不触发 UR013（无参构造不触发字符串规则）
```

### 2.11 反射式方法调用（reflection-method-call）

**场景**：禁止使用 `SendMessage`、`BroadcastMessage`、`StartCoroutine(string)` 等字符串反射式 API。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReflectionMethodCallAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR015",
        title: "禁止使用 SendMessage / BroadcastMessage",
        messageFormat: "禁止使用 '{0}'，基于字符串的反射调用效率低且易出错，请使用直接方法调用或接口",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly HashSet<string> ForbiddenMethods = new()
    {
        "SendMessage", "SendMessageUpwards", "BroadcastMessage",
        "StartCoroutine" // 仅字符串参数版本
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol method) return;

        // 检查是否为 Component 或 GameObject 上的方法
        if (!ForbiddenMethods.Contains(method.Name)) return;

        // 对 StartCoroutine：只检测字符串参数版本
        if (method.Name == "StartCoroutine")
        {
            var args = invocation.ArgumentList?.Arguments;
            if (args == null || args.Count == 0) return;
            var firstArgType = ctx.SemanticModel.GetTypeInfo(
                args[0].Expression).Type;
            // 只检测字符串参数版本
            if (firstArgType == null ||
                firstArgType.SpecialType != SpecialType.System_String) return;
        }

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
            method.Name));
    }
}
```

**检测目标代码**：
```csharp
SendMessage("ApplyDamage", 10);                // ← 报 UR015 Error
BroadcastMessage("OnPlayerJoined");              // ← 报 UR015 Error
StartCoroutine("DelayedAction");                // ← 报 UR015 Error（字符串参数）
StartCoroutine(MyCoroutine());                  // ← 不触发（IEnumerator 参数）
```

### 2.12 重复属性访问（repeated-property-access）

**场景**：禁止在连续代码中对 `transform.position`、`gameObject.name` 等属性重复访问（应缓存到局部变量）。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RepeatedPropertyAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR016",
        title: "禁止重复访问 transform.position 等属性",
        messageFormat: "重复访问 '{0}' 属性（Unity 引擎层会进行 C++ 跨域调用），请缓存到局部变量",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // 需要检查的高频属性
    private static readonly HashSet<string> ExpensiveProperties = new()
    {
        "transform", "gameObject", "position", "rotation",
        "localPosition", "localRotation", "localScale",
        "name", "tag", "layer", "activeSelf", "activeInHierarchy",
        "enabled"
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
    {
        var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;

        var memberName = memberAccess.Name.Identifier.ValueText;
        if (!ExpensiveProperties.Contains(memberName)) return;

        // 检查是否为 Component 或 GameObject 上的属性
        var symbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType;
        var componentType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.Component");
        var gameObjectType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");

        bool isUnityType = (componentType != null && InheritsFrom(containingType, componentType))
            || (gameObjectType != null && SymbolEqualityComparer.Default.Equals(containingType, gameObjectType));
        if (!isUnityType) return;

        // 向上查找：检查是否在同一个方法内被多次访问
        // 简化实现：只要出现在非 Awake/Start 的方法中就报告
        var enclosingMethod = memberAccess.Ancestors()
            .OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (enclosingMethod == null) return;

        var methodName = enclosingMethod.Identifier.ValueText;
        // 跳过初始化方法（Awake/Start/OnEnable 中允许首次访问）
        if (methodName is "Awake" or "Start" or "OnEnable") return;

        ctx.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(),
            memberAccess.ToString()));
    }

    private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
    {
        while (type != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
```

**检测目标代码**：
```csharp
void Update()
{
    var pos = transform.position;          // ← 报 UR016 Warning（在 Update 中访问）
    var name = gameObject.name;            // ← 报 UR016 Warning
}

void Awake()
{
    var rb = GetComponent<Rigidbody>();    // ← 不触发（在初始化方法中）
}
int myX = x;                              // ← 不触发（非 Unity 类型）
```

> **注意**：此规则检查粒度较粗（在非初始化方法中访问即报告），实际使用时可调整阈值（如要求同一方法内访问 ≥3 次才报告）。

### 2.13 废弃 API 检测（deprecated-api）

**场景**：检测 `FindObjectOfType<T>()`（旧 API）但未使用 `FindFirstObjectByType<T>()`（新 API）等情况。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DeprecatedApiAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR017",
        title: "建议使用 FindFirstObjectByType 替代 FindObjectOfType",
        messageFormat: "使用了已过时的方法 '{0}'，Unity 2022+ 推荐使用 FindFirstObjectByType<T>() 或 FindAnyObjectByType<T>()",
        category: "Deprecation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol is not IMethodSymbol method) return;

        // 检测 FindObjectOfType<T> 泛型版本
        if (method.Name == "FindObjectOfType" &&
            method.ContainingType.Name == "Object" &&
            method.TypeArguments.Length > 0)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                "FindObjectOfType<T>"));
        }

        // 检测 FindObjectsOfType<T> 泛型版本
        if (method.Name == "FindObjectsOfType" &&
            method.ContainingType.Name == "Object" &&
            method.TypeArguments.Length > 0)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(),
                "FindObjectsOfType<T>"));
        }
    }
}
```

**检测目标代码**：
```csharp
var player = FindObjectOfType<Player>();         // ← 报 UR017 Warning
var allEnemies = FindObjectsOfType<Enemy>();     // ← 报 UR017 Warning

var player2 = FindFirstObjectByType<Player>();   // ← 不触发（新 API）
var player3 = FindAnyObjectByType<Player>();     // ← 不触发（新 API）
```

### 2.14 顶级语句检测（top-level-statement）

**场景**：检测 Unity 项目脚本中的顶级语句（C# 9 特性），多个文件使用顶级语句会导致 CS8802。

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class TopLevelStatementAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: "UR020",
        title: "禁止在 Unity 项目脚本中使用顶级语句",
        messageFormat: "检测到顶级语句。Unity 项目限制每程序集只能有一个文件使用顶级语句，请改用标准 class 结构",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCompilationUnit, SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeCompilationUnit(SyntaxNodeAnalysisContext ctx)
    {
        var root = (CompilationUnitSyntax)ctx.Node;

        // 检查是否有顶级语句（GlobalStatement）
        if (root.Members.Any(m => m.IsKind(SyntaxKind.GlobalStatement)))
        {
            // 进一步检查：只在 Unity 项目文件中报告
            var filePath = root.SyntaxTree.FilePath;
            if (filePath.Contains("/Assets/") || filePath.Contains("\\Assets\\"))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Rule, root.GetLocation()));
            }
        }
    }
}
```

**检测目标代码**：
```csharp
// File: Assets/Games/MyGame/SomeTempScript.cs
using UnityEngine;
Debug.Log("Hello");                           // ← 报 UR020 Error（顶级语句）
GameObject.Find("Player").SetActive(false);   // ← 同上触发
```

---

## 3. SyntaxKind 速查表

### 3.1 表达式（Expression）

| SyntaxKind | 对应 Syntax 类 | 代码示例 |
|-----------|---------------|---------|
| `InvocationExpression` | `InvocationExpressionSyntax` | `Foo()` |
| `SimpleMemberAccessExpression` | `MemberAccessExpressionSyntax` | `a.b` |
| `ObjectCreationExpression` | `ObjectCreationExpressionSyntax` | `new Foo()` |
| `SimpleAssignmentExpression` | `AssignmentExpressionSyntax` | `x = 5` |
| `AddAssignmentExpression` | `AssignmentExpressionSyntax` | `x += 5` |
| `ElementAccessExpression` | `ElementAccessExpressionSyntax` | `arr[0]` |
| `CastExpression` | `CastExpressionSyntax` | `(int)obj` |
| `BinaryExpression` | `BinaryExpressionSyntax` | `a + b`, `a && b` |
| `ConditionalExpression` | `ConditionalExpressionSyntax` | `a ? b : c` |
| `PrefixUnaryExpression` | `PrefixUnaryExpressionSyntax` | `!a`, `-b` |
| `PostfixUnaryExpression` | `PostfixUnaryExpressionSyntax` | `i++`, `i--` |
| `TypeOfExpression` | `TypeOfExpressionSyntax` | `typeof(Foo)` |
| `DefaultExpression` | `DefaultExpressionSyntax` | `default(Foo)` |
| `SizeOfExpression` | `SizeOfExpressionSyntax` | `sizeof(int)` |
| `AwaitExpression` | `AwaitExpressionSyntax` | `await task` |
| `LambdaExpression` | `LambdaExpressionSyntax` | `x => x + 1` |
| `ParenthesizedLambdaExpression` | `ParenthesizedLambdaExpressionSyntax` | `(x) => x + 1` |
| `AnonymousMethodExpression` | `AnonymousMethodExpressionSyntax` | `delegate { ... }` |
| `InterpolatedStringExpression` | `InterpolatedStringExpressionSyntax` | `$"{name}"` |
| `IsPatternExpression` | `IsPatternExpressionSyntax` | `x is int i` |
| `SwitchExpression` | `SwitchExpressionSyntax` | `x switch { ... }` |
| `WithExpression` | `WithExpressionSyntax` | `record with { A = 1 }` |
| `ImplicitObjectCreationExpression` | `ImplicitObjectCreationExpressionSyntax` | `new()` |
| `BaseExpression` | `BaseExpressionSyntax` | `base` |
| `ThisExpression` | `ThisExpressionSyntax` | `this` |

### 3.2 语句（Statement）

| SyntaxKind | 对应 Syntax 类 | 代码示例 |
|-----------|---------------|---------|
| `LocalDeclarationStatement` | `LocalDeclarationStatementSyntax` | `int x = 0;` |
| `ExpressionStatement` | `ExpressionStatementSyntax` | `Foo();` |
| `ReturnStatement` | `ReturnStatementSyntax` | `return x;` |
| `IfStatement` | `IfStatementSyntax` | `if (x) { }` |
| `ForEachStatement` | `ForEachStatementSyntax` | `foreach (var x in list)` |
| `ForStatement` | `ForStatementSyntax` | `for (int i=0; ...)` |
| `WhileStatement` | `WhileStatementSyntax` | `while (x) { }` |
| `DoStatement` | `DoStatementSyntax` | `do { } while (x)` |
| `SwitchStatement` | `SwitchStatementSyntax` | `switch (x) { ... }` |
| `TryStatement` | `TryStatementSyntax` | `try { } catch { }` |
| `UsingStatement` | `UsingStatementSyntax` | `using (var x = ...)` |
| `ThrowStatement` | `ThrowStatementSyntax` | `throw new Exception()` |
| `LockStatement` | `LockStatementSyntax` | `lock (obj) { }` |
| `YieldReturnStatement` | `YieldStatementSyntax` | `yield return x;` |
| `BreakStatement` | `BreakStatementSyntax` | `break;` |
| `ContinueStatement` | `ContinueStatementSyntax` | `continue;` |

### 3.3 声明（Declaration）

| SyntaxKind | 对应 Syntax 类 | 代码示例 |
|-----------|---------------|---------|
| `ClassDeclaration` | `ClassDeclarationSyntax` | `class Foo { }` |
| `StructDeclaration` | `StructDeclarationSyntax` | `struct Foo { }` |
| `InterfaceDeclaration` | `InterfaceDeclarationSyntax` | `interface IFoo { }` |
| `EnumDeclaration` | `EnumDeclarationSyntax` | `enum Color { }` |
| `MethodDeclaration` | `MethodDeclarationSyntax` | `void Foo() { }` |
| `ConstructorDeclaration` | `ConstructorDeclarationSyntax` | `Foo() { }` |
| `PropertyDeclaration` | `PropertyDeclarationSyntax` | `int X { get; set; }` |
| `FieldDeclaration` | `FieldDeclarationSyntax` | `private int x;` |
| `EventDeclaration` | `EventDeclarationSyntax` | `event Action OnClick;` |
| `DelegateDeclaration` | `DelegateDeclarationSyntax` | `delegate void Foo();` |
| `NamespaceDeclaration` | `NamespaceDeclarationSyntax` | `namespace Foo { }` |
| `FileScopedNamespaceDeclaration` | `FileScopedNamespaceDeclarationSyntax` | `namespace Foo;` |
| `EnumMemberDeclaration` | `EnumMemberDeclarationSyntax` | `Red,` |

### 3.4 子节点访问（在已注册的 SyntaxNode 内导航）

| 场景 | 代码 | 说明 |
|------|------|------|
| 获取调用的方法名 | `invocation.Expression` | `Foo()` → `Foo` |
| 获取成员访问的名部分 | `memberAccess.Name` | `a.b` → `b` |
| 获取成员访问的对象部分 | `memberAccess.Expression` | `a.b` → `a` |
| 获取创建的类型名 | `creation.Type` | `new Foo()` → `Foo` |
| 获取参数列表 | `invocation.ArgumentList` | `Foo(a, b)` → `a, b` |
| 获取修饰符 | `decl.Modifiers` | `public static` |
| 获取特性列表 | `decl.AttributeLists` | `[SerializeField]` |
| 获取父节点 | `node.Parent` | 向上导航 |
| 获取祖先节点 | `node.Ancestors()` | 所有祖先 |
| 获取特定类型祖先 | `node.Ancestors().OfType<ClassDeclarationSyntax>().First()` | 找到所属类 |

---

## 4. SymbolKind 速查表

| SymbolKind | 对应 ISymbol 子类型 | 说明 |
|-----------|-------------------|------|
| `NamedType` | `INamedTypeSymbol` | class, struct, interface, enum, delegate |
| `Method` | `IMethodSymbol` | 方法（含构造函数、运算符、析构函数） |
| `Field` | `IFieldSymbol` | 字段（含 const、event 字段） |
| `Property` | `IPropertySymbol` | 属性 |
| `Event` | `IEventSymbol` | 事件 |
| `Parameter` | `IParameterSymbol` | 参数 |
| `Local` | `ILocalSymbol` | 局部变量 |
| `Namespace` | `INamespaceSymbol` | 命名空间 |
| `Assembly` | `IAssemblySymbol` | 程序集 |
| `TypeParameter` | `ITypeParameterSymbol` | 泛型类型参数 `T` |
| `Label` | `ILabelSymbol` | goto 标签 |
| `Alias` | `IAliasSymbol` | using 别名 |

### ISymbol 常用属性

```csharp
symbol.Name                  // 名称字符串
symbol.ContainingType        // 所属类型（INamedTypeSymbol）
symbol.ContainingNamespace   // 所属命名空间
symbol.ContainingAssembly    // 所属程序集
symbol.DeclaredAccessibility // Accessibility (Public/Private/Internal/...)
symbol.IsStatic              // 是否 static
symbol.IsAbstract             // 是否 abstract
symbol.IsVirtual              // 是否 virtual
symbol.IsOverride             // 是否 override
symbol.IsSealed               // 是否 sealed
symbol.GetAttributes()        // 获取所有 Attribute
symbol.Locations             // 声明位置（可用于报告诊断）
```

### IMethodSymbol 专用属性

```csharp
method.ReturnType            // 返回类型 (ITypeSymbol)
method.Parameters            // 参数列表 (ImmutableArray<IParameterSymbol>)
method.TypeParameters        // 泛型参数
method.IsExtensionMethod     // 是否扩展方法
method.MethodKind            // Ordinary/Constructor/Operator/Destructor/...
method.IsAsync                // 是否 async
```

### INamedTypeSymbol 专用属性

```csharp
type.BaseType                // 基类
type.Interfaces              // 直接实现的接口
type.AllInterfaces           // 所有实现的接口（含继承的）
type.TypeKind                // Class/Struct/Interface/Enum/Delegate
type.IsGenericType            // 是否泛型
type.TypeParameters           // 泛型参数定义
type.TypeArguments            // 泛型实际参数
type.MemberNames             // 所有成员名
type.GetMembers()            // 所有成员
```

---

## 5. SemanticModel 常用 API

SemanticModel 是连接**语法树**与**符号表**的桥梁，每个源文件对应一个 SemanticModel。

### 5.1 获取 SemanticModel

```csharp
// 在 SyntaxNodeAnalysisContext 中直接访问
var model = ctx.SemanticModel;

// 也可从 Compilation 获取
var model = compilation.GetSemanticModel(syntaxTree);
```

### 5.2 符号解析

| API | 返回类型 | 用途 | 示例 |
|-----|---------|------|------|
| `GetSymbolInfo(node)` | `SymbolInfo` | 解析节点对应的符号 | `var sym = model.GetSymbolInfo(invocation).Symbol;` |
| `GetDeclaredSymbol(node)` | `ISymbol` | 获取声明节点定义的符号 | `var field = model.GetDeclaredSymbol(varDeclarator);` |
| `GetTypeInfo(node)` | `TypeInfo` | 获取表达式的类型信息 | `var type = model.GetTypeInfo(expr).Type;` |
| `GetConstantValue(node)` | `Optional<object>` | 获取编译期常量值 | `var val = model.GetConstantValue(expr).Value;` |
| `AnalyzeDataFlow(node)` | `DataFlowAnalysis` | 数据流分析 | `var flow = model.AnalyzeDataFlow(stmt);` |
| `ClassifyConversion(expr, type)` | `Conversion` | 判断类型转换是否合法 | `var conv = model.ClassifyConversion(expr, targetType);` |

### 5.3 GetSymbolInfo vs GetDeclaredSymbol

```csharp
// GetSymbolInfo: 解析"引用" → 跳转到定义
//   适用于：方法调用、成员访问、类型引用
var method = model.GetSymbolInfo(invocationExpr).Symbol as IMethodSymbol;

// GetDeclaredSymbol: 获取"声明" → 声明本身的符号
//   适用于：变量声明、类声明、方法声明
var local = model.GetDeclaredSymbol(variableDeclarator) as ILocalSymbol;
var cls   = model.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
```

### 5.4 TypeInfo 的 Type vs ConvertedType

```csharp
int? a = 10;   // Type = int,       ConvertedType = int?
int? b = (int?)10; // Type = int?,  ConvertedType = int?
string c = "";     // Type = string, ConvertedType = string

var info = model.GetTypeInfo(node);
var actualType = info.Type;            // 表达式本身的类型
var afterConversion = info.ConvertedType; // 隐式转换后的类型
```

### 5.5 DataFlowAnalysis

```csharp
var flow = model.AnalyzeDataFlow(statementOrBlock);

flow.AlwaysAssigned   // 总是被赋值的变量
flow.DataFlowsIn      // 流入区域的变量
flow.DataFlowsOut     // 流出区域的变量
flow.ReadInside       // 区域内读取的变量
flow.ReadOutside      // 区域外读取的变量
flow.WrittenInside    // 区域内写入的变量
flow.WrittenOutside   // 区域外写入的变量
flow.VariablesDeclared // 区域内声明的变量
```

---

## 6. 类型比较与符号判断

### 6.1 获取目标类型

```csharp
// 方式1: SpecialType（.NET 核心类型）
var stringType = compilation.GetSpecialType(SpecialType.System_String);
var intType    = compilation.GetSpecialType(SpecialType.System_Int32);
var objectType = compilation.GetSpecialType(SpecialType.System_Object);

// 方式2: GetTypeByMetadataName（任意类型，用全限定名）
var goType      = compilation.GetTypeByMetadataName("UnityEngine.GameObject");
var monoType    = compilation.GetTypeByMetadataName("UnityEngine.MonoBehaviour");
var listType    = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1"); // 泛型用反引号

// 方式3: 从 SemanticModel 获取
var type = model.GetTypeInfo(node).Type;
```

### 6.2 比较类型（⚠️ 重要）

```csharp
// ❌ 错误：不要用 == 比较 ISymbol
if (type1 == type2) { ... }

// ✅ 正确：用 SymbolEqualityComparer
if (SymbolEqualityComparer.Default.Equals(type1, type2)) { ... }

// ✅ 包含 Nullable 语义的比较
if (SymbolEqualityComparer.IncludeNullability.Equals(type1, type2)) { ... }

// ✅ 对 SpecialType 可直接比较属性
if (type.SpecialType == SpecialType.System_String) { ... }
```

### 6.3 继承与接口检查

```csharp
// 检查是否实现了某接口
bool Implements(INamedTypeSymbol type, ITypeSymbol iface)
{
    return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));
}

// 检查是否继承自某基类
bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
{
    while (type != null)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
        type = type.BaseType;
    }
    return false;
}

// 检查是否是或继承自某类型
bool IsOrInheritsFrom(ITypeSymbol type, ITypeSymbol expected)
{
    return SymbolEqualityComparer.Default.Equals(type, expected)
        || type is INamedTypeSymbol named && InheritsFrom(named, expected);
}
```

### 6.4 构造泛型类型

```csharp
// 获取 List<T> 开放泛型
var listOpenGeneric = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

// 构造 List<string>
var listString = listOpenGeneric.Construct(
    compilation.GetSpecialType(SpecialType.System_String));

// 从已构造类型获取开放泛型
var openGeneric = listString.OriginalDefinition; // → List<T>
```

### 6.5 检查特性

```csharp
bool HasAttribute(ISymbol symbol, string attributeName)
{
    return symbol.GetAttributes().Any(a =>
        a.AttributeClass?.Name == attributeName ||
        a.AttributeClass?.Name == attributeName + "Attribute");
}

// 用法
if (HasAttribute(field, "SerializeField")) { ... }
if (HasAttribute(method, "Obsolete")) { ... }
```

---

## 7. JSON 规则配置格式

### 7.1 规则定义结构

```json
{
  "rules": [
    {
      "id": "UR001",
      "title": "禁止使用 GameObject.Find",
      "messageFormat": "不允许使用 '{0}'，请使用引用或依赖注入替代",
      "category": "Architecture",
      "severity": "error",
      "type": "method-invocation",
      "containingType": "UnityEngine.GameObject",
      "memberName": "Find"
    },
    {
      "id": "UR002",
      "title": "禁止使用 FindObjectOfType",
      "severity": "warning",
      "type": "method-invocation",
      "containingType": "UnityEngine.Object",
      "memberName": "FindObjectOfType"
    },
    
    
    {
      "id": "UR010",
      "title": "禁止对 UnityEngine.Object 使用 == null",
      "severity": "warning",
      "type": "unity-null-check",
      "messageFormat": "对 '{0}' 使用 == null 比较不可靠，请使用 is null 或参考 bool 转换",
      "category": "Correctness"
    },
    {
      "id": "UR011",
      "title": "禁止在 Update 中调用 GetComponent",
      "severity": "warning",
      "type": "update-performance",
      "messageFormat": "在 '{0}' 方法中调用 '{1}' 会造成每帧性能损耗，请将结果缓存在 Awake/Start 中",
      "category": "Performance"
    },
    {
      "id": "UR012",
      "title": "禁止使用 == 比较 Tag 字符串",
      "severity": "warning",
      "type": "string-tag-compare",
      "messageFormat": "使用 == 直接比较 Tag 字符串，请改用 CompareTag()",
      "category": "Performance"
    },
    {
      "id": "UR013",
      "title": "禁止使用 new GameObject(string)",
      "severity": "warning",
      "type": "object-creation-constructor",
      "messageFormat": "使用 new GameObject(name) 字符串构造不利于资源追踪，请使用预制体实例化",
      "category": "Architecture"
    },
    {
      "id": "UR014",
      "title": "禁止直接 new MonoBehaviour",
      "severity": "error",
      "type": "object-creation-constructor",
      "messageFormat": "MonoBehaviour 只能通过 AddComponent 挂载到 GameObject",
      "category": "Architecture"
    },
    {
      "id": "UR015",
      "title": "禁止使用 SendMessage / BroadcastMessage",
      "severity": "error",
      "type": "reflection-method-call",
      "messageFormat": "禁止使用基于字符串的反射调用，请使用直接方法调用或接口",
      "category": "Performance"
    },
    {
      "id": "UR016",
      "title": "禁止重复访问 transform.position 等属性",
      "severity": "warning",
      "type": "repeated-property-access",
      "messageFormat": "重复访问 Unity 引擎属性（C++ 跨域），请缓存到局部变量",
      "category": "Performance"
    },
    {
      "id": "UR017",
      "title": "建议使用 FindFirstObjectByType 替代 FindObjectOfType",
      "severity": "warning",
      "type": "deprecated-api",
      "messageFormat": "使用了已过时的方法，Unity 2022+ 推荐使用新 API",
      "category": "Deprecation"
    },
    {
      "id": "UR020",
      "title": "禁止在 Unity 项目脚本中使用顶级语句",
      "severity": "error",
      "type": "top-level-statement",
      "messageFormat": "顶级语句在 Unity 项目中可能导致 CS8802 错误",
      "category": "Correctness"
    }
  ]
}
```

### 7.2 type 字段与注册方法映射

| type 值 | 注册方法 | SyntaxKind / SymbolKind | ISymbol 类型 |
|---------|---------|------------------------|-------------|
| `method-invocation` | `RegisterSyntaxNodeAction` | `SyntaxKind.InvocationExpression` | `IMethodSymbol` |
| `member-access` | `RegisterSyntaxNodeAction` | `SyntaxKind.SimpleMemberAccessExpression` | `IPropertySymbol` / `IFieldSymbol` |
| `type-reference` | `RegisterSyntaxNodeAction` | `SyntaxKind.ObjectCreationExpression` | `INamedTypeSymbol` |
| `inheritance` | `RegisterSymbolAction` | `SymbolKind.NamedType` | `INamedTypeSymbol` |
| `attribute` | `RegisterSymbolAction` | `SymbolKind.Field` / `SymbolKind.Method` 等 | `IFieldSymbol` / `IMethodSymbol` 等 |
| `dataflow` | `RegisterSyntaxNodeAction` | `SyntaxKind.LocalDeclarationStatement` | `ILocalSymbol` |
| `unity-null-check` | `RegisterSyntaxNodeAction` | `SyntaxKind.EqualsExpression` / `SyntaxKind.NotEqualsExpression` | `INamedTypeSymbol` |
| `update-performance` | `RegisterSyntaxNodeAction` | `SyntaxKind.InvocationExpression` | `IMethodSymbol`（按方法名 + 父方法名） |
| `string-tag-compare` | `RegisterSyntaxNodeAction` | `SyntaxKind.SimpleMemberAccessExpression` | `IPropertySymbol` / `IFieldSymbol` |
| `object-creation-constructor` | `RegisterSyntaxNodeAction` | `SyntaxKind.ObjectCreationExpression` | `INamedTypeSymbol`（按构造函数参数） |
| `reflection-method-call` | `RegisterSyntaxNodeAction` | `SyntaxKind.InvocationExpression` | `IMethodSymbol` |
| `repeated-property-access` | `RegisterSyntaxNodeAction` | `SyntaxKind.SimpleMemberAccessExpression` | `IPropertySymbol` / `IFieldSymbol` |
| `deprecated-api` | `RegisterSyntaxNodeAction` | `SyntaxKind.InvocationExpression` | `IMethodSymbol` |
| `top-level-statement` | `RegisterSyntaxNodeAction` | `SyntaxKind.CompilationUnit` | （全局语句检测） |

### 7.3 severity 字段映射

| JSON 值 | DiagnosticSeverity | 效果 |
|---------|-------------------|------|
| `"error"` | `DiagnosticSeverity.Error` | 编译失败（等同于 CS 错误） |
| `"warning"` | `DiagnosticSeverity.Warning` | 警告，不阻断编译 |
| `"info"` | `DiagnosticSeverity.Info` | 信息提示 |
| `"hidden"` | `DiagnosticSeverity.Hidden` | 隐藏，不显示但可被 IDE 感知 |

---

## 8. 在 RoslynValidator 中集成新规则

### 8.1 集成步骤

```
1. 定义 JSON 规则文件 (custom-rules.json)
2. 在 Program.cs 中添加 LoadCustomAnalyzers() 方法
3. 修改 PerformSemanticAnalysis() 使用 WithAnalyzers()
4. 命令行参数增加 --rules 选项
5. 测试验证
```

### 8.2 PerformSemanticAnalysis 修改

```csharp
static (List<DiagnosticResult>, int) PerformSemanticAnalysis(
    List<SyntaxTree> syntaxTrees,
    string? unityProjectPath,
    string? unityEditorPath,
    string baseDir,
    string? rulesFilePath = null)   // 新增参数
{
    var diagnostics = new List<DiagnosticResult>();
    var references = new List<MetadataReference>();

    // ... 原有的 AddCoreReferences / AddUnityEditorReferences 等代码不变 ...

    var compilation = CSharpCompilation.Create(
        "ValidationAssembly",
        syntaxTrees,
        references,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOverflowChecks(true)
            .WithDeterministic(true)
    );

    // ===== 新增：加载自定义分析器 =====
    var analyzers = LoadCustomAnalyzers(rulesFilePath);
    ImmutableArray<Diagnostic> allDiagnostics;

    if (analyzers.Length > 0)
    {
        var compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        allDiagnostics = compilationWithAnalyzers.GetAllDiagnostics();
    }
    else
    {
        allDiagnostics = compilation.GetDiagnostics();
    }
    // ===== 新增结束 =====

    foreach (var diag in allDiagnostics)
    {
        // ... 原有的诊断处理代码不变 ...
    }

    return (diagnostics, references.Count);
}
```

### 8.3 命令行参数扩展

```csharp
// Main 方法中新增
case "-r":
case "--rules":
    if (i + 1 < args.Length)
    {
        rulesFilePath = args[++i];
    }
    break;
```

---

## 9. 完整示例：通用规则分析器

以下是一个可由 JSON 配置驱动的通用分析器，支持 `method-invocation` 和 `member-access` 两种规则类型：

```csharp
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace RoslynValidator.Analyzers
{
    /// <summary>
    /// 通用规则分析器 - 根据 JSON 配置动态检查代码规则
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class GenericRuleAnalyzer : DiagnosticAnalyzer
    {
        // 规则配置（由外部注入）
        private static ImmutableArray<RuleConfig> _rules = ImmutableArray<RuleConfig>.Empty;

        public static void SetRules(ImmutableArray<RuleConfig> rules)
        {
            _rules = rules;
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                var builders = ImmutableArray.CreateBuilder<DiagnosticDescriptor>();
                foreach (var rule in _rules)
                {
                    builders.Add(rule.Descriptor);
                }
                return builders.ToImmutable();
            }
        }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // 按规则类型注册不同的 Action
            bool hasMethodInvocation = _rules.Any(r => r.Type == "method-invocation");
            bool hasMemberAccess = _rules.Any(r => r.Type == "member-access");
            bool hasTypeReference = _rules.Any(r => r.Type == "type-reference");
            bool hasInheritance = _rules.Any(r => r.Type == "inheritance");
            bool hasAttribute = _rules.Any(r => r.Type == "attribute");

            if (hasMethodInvocation)
                context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

            if (hasMemberAccess)
                context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);

            if (hasTypeReference)
                context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);

            if (hasInheritance)
                context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);

            if (hasAttribute)
                context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol is not IMethodSymbol method) return;

            foreach (var rule in _rules.Where(r => r.Type == "method-invocation"))
            {
                if (MatchesMethod(method, rule, ctx.Compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        rule.Descriptor, invocation.GetLocation(), method.Name));
                }
            }
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext ctx)
        {
            var memberAccess = (MemberAccessExpressionSyntax)ctx.Node;
            var symbol = ctx.SemanticModel.GetSymbolInfo(memberAccess).Symbol;
            if (symbol == null) return;

            foreach (var rule in _rules.Where(r => r.Type == "member-access"))
            {
                if (MatchesMember(symbol, rule, ctx.Compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        rule.Descriptor, memberAccess.GetLocation(), symbol.Name));
                }
            }
        }

        private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext ctx)
        {
            var creation = (ObjectCreationExpressionSyntax)ctx.Node;
            var type = ctx.SemanticModel.GetTypeInfo(creation).Type;
            if (type == null) return;

            foreach (var rule in _rules.Where(r => r.Type == "type-reference"))
            {
                if (MatchesType(type, rule, ctx.Compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        rule.Descriptor, creation.GetLocation(), type.Name));
                }
            }
        }

        private static void AnalyzeNamedType(SymbolAnalysisContext ctx)
        {
            var type = (INamedTypeSymbol)ctx.Symbol;
            foreach (var rule in _rules.Where(r => r.Type == "inheritance"))
            {
                if (MatchesInheritance(type, rule, ctx.Compilation))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        rule.Descriptor, type.Locations[0], type.Name));
                }
            }
        }

        private static void AnalyzeField(SymbolAnalysisContext ctx)
        {
            var field = (IFieldSymbol)ctx.Symbol;
            foreach (var rule in _rules.Where(r => r.Type == "attribute"))
            {
                if (MatchesAttributeRule(field, rule))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        rule.Descriptor, field.Locations[0], field.Name));
                }
            }
        }

        // ===== 匹配逻辑 =====

        private static bool MatchesMethod(IMethodSymbol method, RuleConfig rule, Compilation compilation)
        {
            // 检查方法名
            if (!string.IsNullOrEmpty(rule.MemberName) && method.Name != rule.MemberName)
                return false;

            // 检查所属类型
            if (!string.IsNullOrEmpty(rule.ContainingType))
            {
                var expectedType = compilation.GetTypeByMetadataName(rule.ContainingType);
                if (expectedType == null) return false;
                if (!IsOrInheritsFrom(method.ContainingType, expectedType)) return false;
            }

            return true;
        }

        private static bool MatchesMember(ISymbol symbol, RuleConfig rule, Compilation compilation)
        {
            // 检查成员名
            if (!string.IsNullOrEmpty(rule.MemberName) && symbol.Name != rule.MemberName)
                return false;

            // 检查所属类型
            if (!string.IsNullOrEmpty(rule.ContainingType))
            {
                var expectedType = compilation.GetTypeByMetadataName(rule.ContainingType);
                if (expectedType == null) return false;
                if (!IsOrInheritsFrom(symbol.ContainingType, expectedType)) return false;
            }

            return true;
        }

        private static bool MatchesType(ITypeSymbol type, RuleConfig rule, Compilation compilation)
        {
            if (string.IsNullOrEmpty(rule.TargetType)) return false;

            var expectedType = compilation.GetTypeByMetadataName(rule.TargetType);
            if (expectedType == null) return false;
            return SymbolEqualityComparer.Default.Equals(type, expectedType);
        }

        private static bool MatchesInheritance(INamedTypeSymbol type, RuleConfig rule, Compilation compilation)
        {
            // 检查是否继承自指定基类
            if (!string.IsNullOrEmpty(rule.BaseType))
            {
                var baseType = compilation.GetTypeByMetadataName(rule.BaseType);
                if (baseType == null) return false;
                if (!InheritsFrom(type, baseType)) return false;
            }

            // 检查是否缺少必需接口
            if (!string.IsNullOrEmpty(rule.RequiredInterface))
            {
                var iface = compilation.GetTypeByMetadataName(rule.RequiredInterface);
                if (iface == null) return false;
                if (!type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface)))
                    return true; // 缺少必需接口 → 触发规则
            }

            return false;
        }

        private static bool MatchesAttributeRule(IFieldSymbol field, RuleConfig rule)
        {
            // 检查可访问性
            if (!string.IsNullOrEmpty(rule.Accessibility))
            {
                var expectedAccess = rule.Accessibility.ToLower() switch
                {
                    "private" => Accessibility.Private,
                    "protected" => Accessibility.Protected,
                    "internal" => Accessibility.Internal,
                    "public" => Accessibility.Public,
                    _ => (Accessibility?)null
                };
                if (expectedAccess.HasValue && field.DeclaredAccessibility != expectedAccess.Value)
                    return false;
            }

            // 跳过 static/const
            if (field.IsStatic || field.IsConst) return false;

            // 检查缺少必需特性
            if (!string.IsNullOrEmpty(rule.RequiredAttribute))
            {
                bool hasAttr = field.GetAttributes().Any(a =>
                    a.AttributeClass?.Name == rule.RequiredAttribute ||
                    a.AttributeClass?.Name == rule.RequiredAttribute + "Attribute");

                if (!hasAttr) return true; // 缺少特性 → 触发规则
            }

            return false;
        }

        // ===== 工具方法 =====

        private static bool InheritsFrom(INamedTypeSymbol? type, ITypeSymbol baseType)
        {
            while (type != null)
            {
                if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
                type = type.BaseType;
            }
            return false;
        }

        private static bool IsOrInheritsFrom(ITypeSymbol? type, ITypeSymbol expected)
        {
            if (type == null) return false;
            return SymbolEqualityComparer.Default.Equals(type, expected)
                || type is INamedTypeSymbol named && InheritsFrom(named, expected);
        }
    }

    /// <summary>
    /// 规则配置数据类
    /// </summary>
    public class RuleConfig
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string MessageFormat { get; set; } = "{0}";
        public string Category { get; set; } = "Custom";
        public string Severity { get; set; } = "warning";
        public string Type { get; set; } = "";

        // 匹配条件
        public string? ContainingType { get; set; }
        public string? MemberName { get; set; }
        public string? TargetType { get; set; }
        public string? BaseType { get; set; }
        public string? RequiredInterface { get; set; }
        public string? RequiredAttribute { get; set; }
        public string? Accessibility { get; set; }

        private DiagnosticDescriptor? _descriptor;
        public DiagnosticDescriptor Descriptor
        {
            get
            {
                if (_descriptor == null)
                {
                    var severity = Severity.ToLower() switch
                    {
                        "error" => DiagnosticSeverity.Error,
                        "warning" => DiagnosticSeverity.Warning,
                        "info" => DiagnosticSeverity.Info,
                        "hidden" => DiagnosticSeverity.Hidden,
                        _ => DiagnosticSeverity.Warning
                    };

                    _descriptor = new DiagnosticDescriptor(
                        id: Id,
                        title: Title,
                        messageFormat: MessageFormat,
                        category: Category,
                        defaultSeverity: severity,
                        isEnabledByDefault: true);
                }
                return _descriptor;
            }
        }
    }
}
```

---

## 10. 常见问题

### Q1: 分析器中如何判断节点是否在特定方法内？

```csharp
// 向上查找祖先节点
var enclosingMethod = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
if (enclosingMethod == null) return;

// 也可用语义模型
var enclosingSymbol = ctx.SemanticModel.GetEnclosingSymbol(node.SpanStart, ctx.CancellationToken);
if (enclosingSymbol is IMethodSymbol method && method.Name == "Update") { ... }
```

### Q2: 如何只检测特定命名空间下的类？

```csharp
var type = (INamedTypeSymbol)ctx.Symbol;
if (!type.ContainingNamespace.ToDisplayString().StartsWith("MyGame.")) return;
```

### Q3: 如何排除 generated 代码？

```csharp
context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
// 或在回调中检查
if (ctx.Node.SyntaxTree.FilePath.EndsWith(".g.cs") ||
    ctx.Node.SyntaxTree.FilePath.EndsWith(".generated.cs"))
    return;
```

### Q4: 如何处理编译器自身报错的情况？

```csharp
// 分析器可能在没有编译错误的节点上被调用
// 如果依赖 SemanticModel 返回的符号，做好 null 检查即可
var symbol = ctx.SemanticModel.GetSymbolInfo(node).Symbol;
if (symbol == null) return; // 无法解析语义 → 跳过
```

### Q5: GetTypeByMetadataName 返回 null 怎么办？

```csharp
// 原因1: 类型名拼写错误或未引用对应 DLL
var type = compilation.GetTypeByMetadataName("UnityEngine.GameObject");
if (type == null) return; // 类型未找到，跳过此规则

// 原因2: 多个程序集定义了同名类型 → 返回 null
// 使用 GetTypesByMetadataName 获取所有匹配
var allTypes = compilation.References
    .Select(compilation.GetAssemblyOrModuleSymbol)
    .OfType<IAssemblySymbol>()
    .Select(a => a.GetTypeByMetadataName("MyType"))
    .Where(t => t != null);
```

### Q6: 性能优化建议

```csharp
// 1. 在 RegisterCompilationStartAction 中查找类型，避免每次回调都查找
context.RegisterCompilationStartAction(ctx =>
{
    var targetType = ctx.Compilation.GetTypeByMetadataName("UnityEngine.GameObject");
    if (targetType == null) return; // 类型不存在 → 不注册分析器

    ctx.RegisterSyntaxNodeAction(nodeCtx =>
    {
        // 使用闭包中的 targetType，避免重复查找
        ...
    }, SyntaxKind.ObjectCreationExpression);
});

// 2. 尽早 return，减少不必要计算
// 3. 使用 EnableConcurrentExecution() 允许并行分析
// 4. 避免在回调中分配大量对象
```

---

## 附录：规则 ID 编号规范

| 范围 | 用途 |
|------|------|
| UR001-UR009 | Unity API 限制规则（方法调用、成员访问、类型使用） |
| UR010-UR019 | Unity 正确性与性能规则（null 检查、Update 性能、Tag 比较、反射调用、重复访问等） |
| UR020-UR029 | Unity 代码结构规则（顶级语句等） |
| UR100-UR199 | 架构约束规则（基类/接口继承） |
| UR200-UR299 | 性能优化规则 |
| UR300-UR399 | 命名/编码规范规则 |
| UR400-UR499 | 安全性规则 |
| UR500-UR599 | 测试相关规则 |
