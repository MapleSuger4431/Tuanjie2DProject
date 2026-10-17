# RoslynValidator 使用指南

## GameScripts 全量验证

推荐执行 `{PYTHON} validate_all.py <GameScripts绝对目录> --game <名称>`。`{PYTHON}` 遵循 `skills/solo-mode-router/references/creator-project-contract.md` 的跨平台解析合同：Windows 为 `python` → `py -3`，macOS/Linux 为 `python3` → `python`，候选必须通过 `--version` 并确认为 Python 3。包装脚本递归发现嵌套脚本，并自动传入 `--rules --profile game-scripts`。完整性诊断为 Error，失败退出码为 1；`Temp/result_<名称>.json` 的 `hardGate` 给出 profile、是否通过及阻断诊断 ID。

## 快速使用

> **产物路径**：构建脚本（`build.bat` / `build.sh`）用 `dotnet publish --self-contained -o bin` 输出**自包含单文件**到 `bin/` 根目录，无需目标机预装 .NET。Windows 产物为 `bin/RoslynValidator.exe`（仓库已 ship），macOS/Linux 产物为 `bin/RoslynValidator`（无扩展名，需先构建，见下方「首次使用需先构建」）。

**Windows**（自包含 exe，无需预装 .NET）：
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe -g "游戏名称" -s "文件1.cs,文件2.cs" -b "基础目录绝对路径"
```

**macOS / Linux**（自包含单文件，直接执行，无需 `dotnet`；参数与 Windows 完全一致）：
```bash
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator -g "游戏名称" -s "文件1.cs,文件2.cs" -b "基础目录绝对路径"
```

> 下文示例统一以 Windows `.exe` 写法给出；在 macOS/Linux 上把 `.../RoslynValidator.exe` 整体替换为 `.../RoslynValidator`（去掉 `.exe`）即可，其余参数不变。
>
> **macOS/Linux 首次使用需先构建**（仓库默认只 ship Windows 二进制）：
> ```bash
> cd .codely-cli/extensions/TJUIBuilder/tools/RoslynValidator
> ./build.sh linux-x64   # 或 osx-x64 / osx-arm64
> ```
> 构建后产物即为 `bin/RoslynValidator`。
>
> `build.sh` 会识别 Rosetta 转译环境下的 Apple Silicon，未传 runtime 时仍会选择 `osx-arm64`。`validate_all.py` 也会检查 Mach-O 架构：Apple Silicon 上发现 x86_64 产物时提示 Rosetta 依赖，发现不可兼容产物时以环境错误退出。

## 输入参数

| 参数 | 类型 | 必需 | 说明 | 示例 |
|------|------|------|------|------|
| `-g, --game-name` | string | 是 | 游戏名称，用于生成结果文件名 | `"FlappyBird3D"` |
| `-s, --source-files` | string | 是 | 要验证的 C# 文件列表（逗号分隔） | `"Player.cs,GameManager.cs"` |
| `-b, --base-directory` | string | 是 | c# 源文件的基础目录绝对路径 | `"D:/Projects/AI/mwaisamples/context/FlappyBird3D/GameScripts"` |
| `-r, --rules` | flag | 否 | 启用自定义 Unity 反模式分析器 | |
| `-w, --warnings` | flag | 否 | 在控制台同时显示 warnings | |

### `-r, --rules` 说明

启用内置的 Unity 反模式分析器（UR025~UR030），检测常见 Unity 代码问题：占位常量返回、只读不写/只写不读属性、不可达 else-if 分支、`Resources.Load`、对框架单例用 `FindObjectOfType`。多为 Warning（UR025/UR029 为 Error）。`--profile game-scripts` 时追加 UR032~UR039/UR048~UR050 完整性与可靠性门禁。这些规则在**写脚本阶段**即可提前暴露。详见 [现有自定义规则](./现有自定义规则.md)。建议配合 `-w` 查看 Warning。

### `-w, --warnings` 说明

默认控制台只输出 errors，添加 `-w` 后同时输出 warnings。JSON 结果文件始终包含完整的 errors + warnings 信息。

## 输出格式

### Console 输出

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

**注意**：
- Console 输出默认只显示 errors，不显示 warnings
- 加 `-w` 参数可同时显示 warnings
- `SUCCESS` 表示验证成功


## 使用技巧

1. **判断验证是否成功**：
   - 检查 console 输出是否包含 `SUCCESS`
   - 或检查 `errorCount` 是否为 `0`

2. **获取详细错误信息**：
   - Console 输出直接显示每个错误的详情
   - 格式：`ERROR [错误代码]: 错误消息` + `at 文件名(行号,列号)`

## 常见用法

### 验证单个文件
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe -g Test -s Player.cs -b "D:/path/to/scripts"
```

### 验证多个文件
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe -g Test -s "Player.cs,GameManager.cs,Enemy.cs" -b "D:/path/to/scripts"
```

### 启用自定义分析器
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe -g Test -s Player.cs -b "D:/path/to/scripts" --rules
```

### 同时显示 warnings
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe -g Test -s Player.cs -b "D:/path/to/scripts" --rules -w
```

### 使用长参数名
```powershell
.codely-cli/extensions/TJUIBuilder/tools/RoslynValidator/bin/RoslynValidator.exe --game-name FlappyBird3D --source-files "Player.cs,GameManager.cs" --base-directory "D:/Projects/AI/mwaisamples/context/FlappyBird3D/GameScripts" --rules --warnings
```

## 路径格式要求

- `--base-directory` 必须使用绝对路径，使用正斜杠 `/`
- `--source-files` 使用相对路径（相对于 `--base-directory`）
- 文件列表用逗号分隔，不要有空格

## 注意事项

- ✅ 使用 run-shell-command 工具调用：Windows 用 `bin/RoslynValidator.exe`，macOS/Linux 用 `bin/RoslynValidator`（自包含单文件，直接执行、无需 `dotnet`）
- ✅ 使用 `-g`, `-s`, `-b` 参数
- ✅ Console 输出默认只显示 errors，加 `-w` 显示 warnings

## 引用环境错误（CS0518 / 大量 CS0246）

RoslynValidator 当前没有 `unityEditorPath` 命令行或配置参数。它从项目根目录的 `Library/EditorInstance.json` 自动读取 `app_contents_path`（回退到 `app_path`），因此应从 Unity 项目根目录运行，并先打开一次对应的 Unity/Tuanjie Editor。

若出现大量 `CS0518`，或基础 `System.*`、`UnityEngine.*` 类型同时无法解析：

1. 用 `unity_editor.get_state` 核对 `applicationContentsPath`。
2. 检查 `Library/EditorInstance.json` 是否存在，且 `app_contents_path` 指向现有目录。
3. 修复 Editor 路径、安装或验证器引用发现逻辑后，以相同参数重跑。

这类诊断属于阻断性的验证环境失败，**不得忽略，也不得绕过 RoslynValidator 直接部署到 Assets**。Unity 编译是后续独立门禁，不能替代部署前的 Roslyn 全量验证。

## 退出代码

| 代码 | 说明 |
|------|------|
| 0 | 验证成功（无错误） |
| 1 | 验证失败（有错误）或运行时错误 |
