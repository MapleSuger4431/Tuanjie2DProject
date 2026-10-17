# RoslynValidator

## Game scripts completeness profile

Use `--profile game-scripts` with `--rules` for generated gameplay scripts. It enables the Error-level completeness/wiring hard gates (UR032-UR036, UR038, UR039, UR048-UR050). Result JSON includes `hardGate.profile`, `hardGate.passed`, and `hardGate.blockingDiagnosticIds`; `validate_all.py` keeps recursive discovery and accepts the same `--input-contract` option.

使用 Roslyn 编译器服务的独立 C# 脚本验证工具。该工具无需运行 Unity Editor 即可对 Unity 项目的 C# 源文件进行全面的语法和语义验证。

> **⚠️ 前置条件：必须先打开 Unity Editor 再运行本工具。** Unity Editor 启动后会在项目 `Library/` 目录下生成 `EditorInstance.json`，工具据此定位并加载 `UnityEngine.*.dll`、`UnityEditor.*.dll` 及 Mono 运行时。若未打开 Unity Editor，工具无法加载 Unity DLL，所有 Unity 类型引用将报 CS0246，自定义分析器（`--rules`）规则也不会触发。

## 特性

- **语法验证**：使用 Roslyn 解析和验证 C# 语法
- **语义分析**：类型检查、方法解析和命名空间验证
- **自定义分析器**：Unity 反模式规则，通过 `--rules` 启用
- **自动配置**：自动读取 Unity Editor 路径、项目路径和预处理器符号
- **Unity 集成**：自动发现并加载 Unity DLL 和项目程序集
- **智能输出**：
  - Console 输出：简化的错误信息（只显示 errors）
  - 文件输出：完整的诊断信息（errors + warnings）
- **直接调用**：使用命令行参数直接验证，无需配置文件
- **跨平台**：在 Windows、Linux 和 macOS 上运行
- **独立运行**：在 Unity 外运行，但**需先打开 Unity Editor 一次**以生成 `Library/EditorInstance.json`
- **自动保存结果**：自动保存验证结果到 `Temp/result_{GameName}.json`
- **详细诊断**：提供详细的错误和警告诊断信息

## 系统要求

- .NET 6.0 SDK 或更高版本（推荐使用 .NET 6.0 LTS）
- 项目目标框架为 `net6.0`，通过 `<RollForward>LatestMajor</RollForward>` 支持 .NET 8 等更高版本运行

## 构建

### 使用 dotnet build

```powershell
cd .codely-cli/extensions/TJUIBuilder/tools/RoslynValidator
dotnet build -c Release
# 产出(dotnet build 中间产物, 依赖 .NET runtime): bin/Release/net6.0/win-x64/
```

### 使用构建脚本

**Windows**：
```batch
build.bat [runtime]
```

默认运行时为 `win-x64`。其他选项：
- `win-x64` - Windows 64位
- `win-x86` - Windows 32位
- `linux-x64` - Linux 64位
- `osx-x64` - macOS 64位
- `osx-arm64` - macOS Apple Silicon

**Linux/macOS**：
```bash
chmod +x build.sh
./build.sh [runtime]
```

## 使用方法

### 基本用法

```powershell
RoslynValidator -g "FlappyBird3D" -s "Player.cs,GameManager.cs" -b "context/FlappyBird3D/GameScripts"
```

### 命令行参数

| 参数 | 简写 | 类型 | 必需 | 说明 |
|------|------|------|------|------|
| `--game-name` | `-g` | string | 是 | 游戏名称，用于生成结果文件名 |
| `--source-files` | `-s` | string | 是 | 要验证的 C# 文件列表（逗号分隔） |
| `--base-directory` | `-b` | string | 是 | 基础目录绝对路径 |
| `--rules` | `-r` | flag | 否 | 启用自定义 Unity 反模式分析器 |
| `--warnings` | `-w` | flag | 否 | 在控制台同时显示 warnings |
| `--script` | - | flag | 否 | EditorScript 模式，用 Roslyn script compilation 校验一次性 Editor 脚本 |
| `--recursive` | - | flag | 否 | 递归发现基础目录下全部脚本，构成全量游戏脚本集 |
| `--profile` | - | string | 否 | 规则档案；`game-scripts` 启用 UR032–UR036、UR038、UR039、UR048–UR050 的 Error 级完整性/接线硬门禁 |
| `--input-contract` | - | string | 否 | compiled Job Plan 路径，配合 `--recursive` 装载输入入口清单 |
| `--result-path` | - | string | 否 | 指定结果 JSON 输出路径，替代默认 `Temp/result_*.json` |
| `--summary-json` | - | flag | 否 | 向 stdout 输出机器可读汇总，供 `validate_all.py` 等包装器消费 |
| `--help` | `-h` | - | 否 | 显示帮助信息 |

### 输出格式

#### Console 输出

**验证成功**：
```
SUCCESS
```

**验证失败**：
```
FAILED - Error Count: 1

ERROR [CS1002]: ; expected
  at ErrorTest.cs(13,18)
```

**带 `-w` 显示 warnings**：
```
FAILED - Error Count: 5, Warning Count: 11

WARNING [UR001]: Discouraged method 'GameObject.Find', prefer serialized references or dependency injection; if needed, call in Awake/Start only
  at Player.cs(16,22)
WARNING [UR010]: Comparing 'Rigidbody' with == null is unreliable, use 'is null' or implicit bool conversion instead
  at Player.cs(36,13)
```

#### 结果文件

**文件路径**：`Temp/result_{GameName}.json`

**内容示例**：
```json
{
  "inputParameters": {
    "gameName": "FlappyBird3D",
    "sourceFiles": ["Player.cs", "GameManager.cs"],
    "baseDirectory": "context/FlappyBird3D/GameScripts"
  },
  "success": true,
  "errorCount": 0,
  "warningCount": 52,
  "filesValidated": 3,
  "referencesLoaded": 169,
  "elapsedMs": 1348,
  "diagnostics": [
    {
      "severity": "warning",
      "id": "CS0168",
      "message": "The variable 'temp' is declared but never used",
      "file": "PlayerController.cs",
      "line": 30,
      "column": 13
    }
  ]
}
```

#### 关键字段说明

| 字段 | 类型 | 描述 |
|------|------|------|
| `inputParameters` | object | 输入参数信息 |
| `inputParameters.gameName` | string | 游戏名称 |
| `inputParameters.sourceFiles` | string[] | 源文件列表 |
| `inputParameters.baseDirectory` | string | 基础目录路径 |
| `success` | bool | 验证是否成功（`true` 表示无错误） |
| `errorCount` | int | 错误数量 |
| `warningCount` | int | 警告数量 |
| `filesValidated` | int | 验证的文件数量 |
| `referencesLoaded` | int | 加载的引用程序集数量 |
| `elapsedMs` | long | 验证耗时（毫秒） |
| `diagnostics` | array | 诊断信息数组（包含 errors 和 warnings） |

## 工作原理

### 自动检测配置

工具会自动从 Unity 项目中读取以下配置：

1. **Unity Editor 路径**：从 `Library/EditorInstance.json` 读取（**需先打开 Unity Editor 生成此文件**，否则 Unity DLL 无法加载）
2. **Unity 项目路径**：从 `.com-unity-codely.json` 读取
3. **预处理器符号**：从 `Library/Bee/artifacts/*.rsp` 读取（如果不可用则使用默认符号）

### 验证流程

1. **解析参数**：从命令行参数读取 GameName, SourceFiles, BaseDirectory
2. **自动检测路径**：读取 Unity Editor 路径、项目路径和预处理器符号
3. **语法验证**：解析所有源文件，检测语法错误
4. **语义分析**：加载 Unity DLL 和项目程序集，进行类型检查
5. **自定义分析**（`--rules`）：启用 Unity 反模式分析器，检测自定义规则（详见 [现有自定义规则](./现有自定义规则.md)）
6. **输出结果**：
   - Console 输出：默认只显示 errors，加 `-w` 同时显示 warnings
   - 文件输出：完整的验证报告（errors + warnings）保存到 `Temp/result_{GameName}.json`

### Unity DLL 发现

工具会自动加载以下引用：

- **UnityEngine.dll** 和所有 **UnityEngine.*.dll** 模块
- **UnityEditor.dll** 和所有 **UnityEditor.*.dll** 模块
- **Library/ScriptAssemblies/*.dll**（编译的项目程序集）
- **Library/PlayerScriptAssemblies/*.dll**（运行时脚本程序集）
- **NUnit 框架**（从 Unity Test Framework 包中加载）
- **核心 .NET 程序集**（System.*.dll）

## 路径格式要求

- 使用正斜杠 `/` 而非反斜杠 `\`
- `--base-directory` 必须使用绝对路径
- `--source-files` 使用相对路径（相对于 `--base-directory`）
- 文件列表用逗号分隔，不要有空格

## 常见错误代码

### 语法错误

| 错误代码 | 描述 | 解决方案 |
|---------|------|---------|
| CS1002 | 缺少分号 | 在语句末尾添加分号 |
| CS1003 | 缺少右括号 | 检查括号匹配 |
| CS1513 | 缺少右大括号 | 检查大括号匹配 |
| CS1519 | 类中无效标记 | 检查类成员定义 |

### 语义错误

| 错误代码 | 描述 | 解决方案 |
|---------|------|---------|
| CS0246 | 类型或命名空间未找到 | 添加 `using` 语句或检查拼写 |
| CS0103 | 名称不存在 | 检查变量名、方法名或添加引用 |
| CS1061 | 类型不包含定义 | 检查成员名称、大小写、访问修饰符 |
| CS0266 | 无法隐式转换类型 | 使用显式转换或检查类型兼容性 |

### 引用环境错误

大量 `CS0518`，或基础 `System.*`、`UnityEngine.*` 类型同时报错，表示验证器没有建立完整引用环境，而不是可忽略的业务代码噪音。当前工具不接受 `unityEditorPath` 配置项；它从项目根目录的 `Library/EditorInstance.json` 自动读取 `app_contents_path`，回退读取 `app_path`。

从 Unity 项目根目录运行工具，确认对应 Editor 已至少打开一次，且上述路径指向现有安装。引用环境恢复前验证必须保持失败；禁止跳过 RoslynValidator 直接部署，后续 Unity 编译不能替代该部署前门禁。

## 退出代码

| 代码 | 说明 |
|------|------|
| 0 | 验证成功（无错误） |
| 1 | 验证失败（有错误）或运行时错误 |

## 示例

### 验证单个文件
```powershell
RoslynValidator -g Test -s Player.cs -b "/path/to/scripts"
```

### 验证多个文件
```powershell
RoslynValidator -g Test -s "Player.cs,GameManager.cs,Enemy.cs" -b "/path/to/scripts"
```

### 使用长参数名
```powershell
RoslynValidator --game-name FlappyBird3D --source-files "Player.cs,GameManager.cs" --base-directory "context/FlappyBird3D/GameScripts"
```

### 启用自定义分析器
```powershell
RoslynValidator.exe -g FlappyBird3D -s "Player.cs,GameManager.cs" -b "D:/project/scripts" --rules
```

### 同时显示 warnings
```powershell
RoslynValidator.exe -g FlappyBird3D -s "Player.cs,GameManager.cs" -b "D:/project/scripts" --rules -w
```

## 注意事项

- ✅ 结果文件始终包含完整的诊断信息（errors + warnings）
- ✅ Console 输出默认只显示 errors，加 `-w` 显示 warnings
- ✅ 结果文件保存到当前工作目录下的 `Temp` 文件夹
- ✅ 自定义分析器规则详见 [现有自定义规则](./现有自定义规则.md)