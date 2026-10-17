# 项目布局与路径约定

本文档定义 unity-ui-builder 的项目目录约定和路径守卫规则。

## 项目布局

| 用途 | 路径 |
|---|---|
| 游戏文档与资源清单 | `context/games/{GameName}/` |
| Unity 游戏工程 | `Assets/Games/{GameName}/` |
| C# 脚本真相源 | `context/games/{GameName}/GameScripts/` |
| UXML/USS 真相源（UI Toolkit 模式） | `context/games/{GameName}/UI/` |
| UGUI Panel Prefab（UGUI 模式） | `Assets/Games/{GameName}/UI/Prefabs/`（二进制资产，直接通过 execute_csharp_script 创建） |

## 文件写入规则

- C#、UXML、USS 以 `context/` 为真相源，并同步部署到 `Assets/`；若调试时先改了 Assets，必须回灌 context。UI 修改后做内容级一致性校验，不能用文件数量相等代替。
- 含中文的 `.cs`、`.md`、`.yaml`、`.uxml`、`.uss` 使用 UTF-8 安全的文件工具；禁止用 PowerShell `Set-Content`、`Out-File` 或 `>` 重写。
- 禁止使用同步 `Resources.Load*`。游戏资产优先使用直接引用；确需运行时加载时只用项目既有的异步方案，且先验证依赖已安装。
- 默认不修改 Package 源码；优先在 `Assets/Games/{GameName}/Scripts/` 通过子类、partial 或扩展方法解决。确实无法扩展时报告阻塞，不静默改包。

## 路径守卫

- 宿主项目须采用 `context/games/{GameName}/` 目录约定。所有写入操作在目标目录不存在时**必须自动创建**（`write_file` 自动创建父目录；`execute_csharp_script` 中用 `Directory.CreateDirectory`）
- ✅ UXML/USS 文件（UI Toolkit 模式） → `context/games/{GameName}/UI/`（先写入 context，后部署到 Assets）
- ✅ UGUI trees.json（UGUI 模式） → `context/games/{GameName}/ui-panel-builder/trees/{InterfaceName}.trees.json`（write_file 构建源，含多棵 brick 树）
- ✅ UGUI Panel Prefab（UGUI 模式） → 直接创建到 `Assets/Games/{GameName}/UI/Prefabs/`（由 `execute_csharp_script` 固定 runner 调用 `BrickPanelBuilder.BuildBrickTrees` 从 trees.json 产出；Prefab 是二进制资产无需 context 中间层）
- ✅ PanelSettings（UI Toolkit 模式） → `Assets/Games/{GameName}/UI/`（ScriptableObject，通过 execute_csharp_script 直接创建）
- ✅ UI Panel 脚本 → `context/games/{GameName}/GameScripts/`（先验证，后部署到 Assets）
- ⛔ 禁止将 `.cs` 文件直接写入 `Assets/` 目录（部署阶段除外）
- ⛔ 禁止将 UXML/USS 文件直接写入 `Assets/` 目录（部署阶段除外）
- ⛔ UGUI 模式不创建 UXML/USS/PanelSettings 文件（这些是 UI Toolkit 模式专属产物）

## Unity 查询门禁

- 查询场景内 GameObject、组件、属性、Tag 或 Layer 须使用 `unity-hierarchy-query`（或等价能力）。
- 查询项目资产、Prefab、材质、纹理、模型或配置须使用 `unity-asset-query`（或等价能力）。
- 禁止手写 `FindObjectsOfType`、`GameObject.Find` 或 `AssetDatabase.FindAssets` 代替上述查询技能。

## Unity 操作规则

- `execute_csharp_script` 禁止 `Thread.Sleep`、忙等、阻塞等待或轮询帧/状态。触发与观察拆成独立调用；跨帧工作使用正式生命周期代码或受支持的异步机制。
- 仅当 Unity 工具明确报告 Bridge 断开或陈旧时调用 `unity_refresh`；禁止把资产 reimport 当作常规刷新。
- Unity 同步工具返回即表示动作完成。编译固定走 `unity_console clear` → `unity_editor.start_compilation_pipeline` → `unity_console get`；其它需归因错误的操作走 clear → action → get。
