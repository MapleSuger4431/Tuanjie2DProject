# Test 沙盒（本地测试项目）

本文件夹是**隔离沙盒**：所有测试脚本、场景、预制体、材质都放这里，不影响主工程。

## 隔离保证

- **独立程序集**：根目录的 `Sunset.Test.asmdef` 让本文件夹内所有运行时脚本编译进独立程序集 `Sunset.Test`；`autoReferenced: false` 表示主工程代码（Assembly-CSharp / Assembly-CSharp-Editor）**无法引用**这里的任何类型，同名类也不会冲突。
- **有限依赖（2026-10-17 起）**：`Sunset.Test` 显式引用 `Runestone.AesirArchitecture` 与 `Runestone.AesirModules` 两个 Aesir 程序集（登录界面直接使用 Aesir UI 框架：`AesirBasePanel` / `UIModule`）。依赖是**单向**的：测试代码可用 Aesir，主工程代码依旧引用不到测试程序集；删除本文件夹仍可彻底移除测试内容，不影响 Aesir 本体。
- **Editor 代码隔离**：编辑器专用脚本放 `Editor/` 子文件夹，编译进 `Sunset.Test.Editor`（仅 Editor 平台），不会泄入全局的 Assembly-CSharp-Editor。

## 使用规则（往这里加内容时遵守）

1. 一切测试内容（场景 / 预制体 / 材质 / 脚本 / 配置资产）只放本文件夹内。
2. **不要**在本文件夹内创建 `Resources` 或 `StreamingAssets` 文件夹——它们是全局合并的，会泄漏到主工程。
3. **不要**使用自定义 Tag / Layer / Sorting Layer——会写入 ProjectSettings，属于全局设置。
4. 测试场景**不要**加进 Build Settings 场景列表。
5. 测试代码需要新的主工程程序集时，在 `Sunset.Test.asmdef` 的 references 里显式添加（当前已引用：`Runestone.AesirArchitecture`、`Runestone.AesirModules`）；依赖保持单向——主工程永远引用不到测试代码。
6. 保持编译通过：Unity 的编译检查是全局的，本文件夹内脚本的**编译错误**仍会让整个工程暂停（无法进入 Play 模式）。这是 Unity 机制，asmdef 无法绕过，所以提交前务必确认本文件夹无编译错误。

## 目录约定

```
Assets/Test/
├── Sunset.Test.asmdef        运行时程序集（本文件夹全部运行时脚本，含 Sunset/ 等子目录）
├── Scenes/                   测试场景
├── Prefabs/                  测试预制体（LoginPanel.prefab 等）
├── Scripts/                  运行时测试脚本（命名空间建议 Sunset.Test.*）
└── Editor/                   仅编辑器脚本（程序集 Sunset.Test.Editor）
```
