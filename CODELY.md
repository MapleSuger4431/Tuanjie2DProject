

## Codely Structured Memories

### User

### Feedback

### Project
- [2026-10-17 19:54:46] [2026-10-17] 美术资产同步方案已从 U 盘切换为 Git LFS：.gitattributes 追踪 19 种二进制格式（图片/音频/字体），Assets/Art/ 不再被 gitignore，远程是 GitHub（MapleSuger4431/Tuanjie2DProject）。U 盘方案（Team-Git-Workflow.md 第 7 节 + tools/sync-art.ps1）标注"暂停使用"但保留备用。Why: 用户明确"暂时放弃 U 盘、打开 Git-LFS"；避免未来会话再按 U 盘方案给建议或误判 Art 目录被忽略。How to apply: 涉及美术资源分发/同步时一律走 git lfs pull + 正常提交；队友环境需装 git-lfs。
- [2026-10-17 20:48:36] [2026-10-17] 本项目输入系统现状与代码约定：Player Settings 为 activeInputHandler=2（仅 Input System，包版本 com.unity.inputsystem 1.14.4-t4），旧 Input.* API 运行时抛 InvalidOperationException。写输入代码一律用 #if ENABLE_INPUT_SYSTEM / #elif ENABLE_LEGACY_INPUT_MANAGER / #else false 宏分支做双模式兼容（参照 6 个示例脚本：EventSender 保留 KeyCode 序列化字段+Enum.TryParse 名称映射为 Key，SamplePlaneWarMonoPlayer 用方向键/WASD 对齐旧默认轴）。asmdef 约定：双模式程序集 references 加 "Unity.InputSystem" 且不设 defineConstraints；仅纯 InputSystem 程序集（如 Runestone.AesirModules.InputSystem）才用 defineConstraints=["ENABLE_INPUT_SYSTEM"]。Why: 不照此约定写输入代码会在当前工程运行时直接炸或编译失败。How to apply: 新增/修改任何输入读取代码前先确定程序集属于哪类，再选对应模式。
- [2026-10-17 21:02:45] [2026-10-17] TMPChineseText 扩展已正式入库并推送（commit 36915e3 扩展本体 + 2d8d01d manifest/lock 引用），其 file: 引用不再需要剥离，队友拉取后可正常 UPM 解析。仍未提交、勿随手提交的 WIP：Assets/TextMesh Pro/、Assets/Codely/、Assets/Scenes/MainScene.scene、Assets/Editor/BootSceneBuilder.cs 的删除。另注意：本机 git config 设了 lfs.dialtimeout=3/tlstimeout=3，LFS 推送大对象易 DNS i/o timeout 失败，用 `git -c lfs.dialtimeout=30 -c lfs.tlstimeout=30 push` 可成功（2026-10-17 推送 16MB 字体实测）。Why: 用户明确要求把扩展和 manifest 引用提交入库，原"剥离引用"约定已失效。How to apply: 本项目提交时只避开上述 4 项 Assets WIP；manifest/lock 的 TMPChineseText 引用属正常已提交内容；LFS push 超时就用 -c 放宽超时重试。


### Reference

