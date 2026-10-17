

## Codely Structured Memories

### User

### Feedback
- [2026-10-17 21:49:36] 禁止向 UIModule.Open<T>/Show<T> 传裸字符串 payload：字符串字面量会命中 (string path) 重载，payload 文本被当作资源路径（控制台警告"path 被忽略"，payload 实际为 null，标题/数据静默丢失）。正确写法 UIModule.Open<HelpWindow>((object)"操作说明")——2026-10-17 集成 HelpWindow 时实测发现，官方示例 SampleUISettingWindow 的同款调用其实也踩此坑。Why: 静默失败极难排查，功能看似正常只因预制体预置了相同文字。How to apply: 任何字符串 payload 一律 (object) 强转再传入。
- [2026-10-17 22:51:18] Canvas 根预制体转普通面板根时必须归一化 RectTransform：Canvas 会每帧驱动其根 RectTransform 的 scale/pivot/position（驱动值是临时的、不序列化）；删掉 Canvas 改成面板形态后驱动释放，会暴露底层序列化的脏值（实测 scale=(0,0,0)、pivot=(0,0)），导致 UI 元素数据上"已激活"但世界空间零面积、完全不可见。诊断特征：RectTransform.GetWorldCorners 四角全部相同；对照正常渲染的兄弟 Canvas 排除相机/层配置后，查自身 localScale。修复：localScale=(1,1,1)、pivot=(0.5,0.5)、锚点 0~1+offsets 0。2026-10-17 HelpWindow 窗口→面板转换时实测踩坑。How to apply: 凡把带 Canvas 的预制体改成普通 RectTransform 面板，转换后必须显式重设 localScale/pivot，并用 GetWorldCorners 四角验证世界空间尺寸；仅验证 IsOpen/组件数据不足以证明可见。
- [2026-10-17 23:51:29] [2026-10-18] 禁止用 FindObjectsByType(FindObjectsInactive.Include) 探测 UIModule 面板实例：UIModule 会为每个实例化过的面板预制体在恒停用容器 [UIModule]InactiveTemplates 下保留一份"停用模板"克隆，Include 搜索会把它当实例抓到，而模板从未走 OnInit——按钮 onClick 无监听（Invoke 无效）、activeInHierarchy 恒 false，用它断言"面板未显示/关不掉"全是假象。Why: 2026-10-17 验证主菜单说明弹窗时实测踩坑，第一轮测试点在模板按钮上导致误判弹窗关不掉（游戏代码实际完全正常）。How to apply: 运行时验证面板一律用 UIModule.Get&lt;T&gt;()（注册表、非创建式）或 FindObjectsByType(FindObjectsInactive.Exclude, ...)。
- [2026-10-17 23:51:29] [2026-10-18] SceneModule.LoadSceneSingle/LoadSceneAdditive 的 onCompleted 与 onFailed 都是无参 Action（非 Exception 参数）：AesirFramework 技能 modules-api.md 的示例 `onFailed: e =&gt; Debug.LogError(e)` 照抄会 CS1593 编译失败。失败的具体原因 SceneModule 内部已经 LogError，回调里只写无参处理即可。How to apply: 一律写 onFailed: () =&gt; Debug.LogError($"...失败：{路径}")。

### Project
- [2026-10-17 19:54:46] [2026-10-17] 美术资产同步方案已从 U 盘切换为 Git LFS：.gitattributes 追踪 19 种二进制格式（图片/音频/字体），Assets/Art/ 不再被 gitignore，远程是 GitHub（MapleSuger4431/Tuanjie2DProject）。U 盘方案（Team-Git-Workflow.md 第 7 节 + tools/sync-art.ps1）标注"暂停使用"但保留备用。Why: 用户明确"暂时放弃 U 盘、打开 Git-LFS"；避免未来会话再按 U 盘方案给建议或误判 Art 目录被忽略。How to apply: 涉及美术资源分发/同步时一律走 git lfs pull + 正常提交；队友环境需装 git-lfs。
- [2026-10-17 20:48:36] [2026-10-17] 本项目输入系统现状与代码约定：Player Settings 为 activeInputHandler=2（仅 Input System，包版本 com.unity.inputsystem 1.14.4-t4），旧 Input.* API 运行时抛 InvalidOperationException。写输入代码一律用 #if ENABLE_INPUT_SYSTEM / #elif ENABLE_LEGACY_INPUT_MANAGER / #else false 宏分支做双模式兼容（参照 6 个示例脚本：EventSender 保留 KeyCode 序列化字段+Enum.TryParse 名称映射为 Key，SamplePlaneWarMonoPlayer 用方向键/WASD 对齐旧默认轴）。asmdef 约定：双模式程序集 references 加 "Unity.InputSystem" 且不设 defineConstraints；仅纯 InputSystem 程序集（如 Runestone.AesirModules.InputSystem）才用 defineConstraints=["ENABLE_INPUT_SYSTEM"]。Why: 不照此约定写输入代码会在当前工程运行时直接炸或编译失败。How to apply: 新增/修改任何输入读取代码前先确定程序集属于哪类，再选对应模式。
- [2026-10-17 23:21:52] [2026-10-17] TMPChineseText 扩展已正式入库并推送（commit 36915e3 扩展本体 + 2d8d01d manifest/lock 引用），其 file: 引用不再需要剥离，队友拉取后可正常 UPM 解析。原 4 项 Assets WIP 已于 2026-10-17 由远程全部解决入库：Assets/TextMesh Pro Essential 与 Assets/Codely 中文字体资产已提交（d38f1e3/91bc5ff）、MainScene.scene 已提交（fbd9c1f）、BootSceneBuilder.cs 已删除（bdb8779）；本机 2026-10-17 已合并采用远程版（本机扩展生成的同名资产仅 GUID 不同，已废弃本机版）。另注意：本机 git config 设了 lfs.dialtimeout=3/tlstimeout=3，LFS 推送大对象易 DNS i/o timeout 失败，用 `git -c lfs.dialtimeout=30 -c lfs.tlstimeout=30 push` 可成功（2026-10-17 推送 16MB 字体实测）。Why: 用户明确要求把扩展和 manifest 引用提交入库，原"剥离引用"约定已失效。How to apply: 上述资产均为正常已提交内容、无需避开；manifest/lock 的 TMPChineseText 引用属正常已提交内容；LFS push 超时就用 -c 放宽超时重试。


- [2026-10-17 23:59:20] 测试沙盒约定：一切本地测试内容放 Assets/Test/Sunset（个人测试区；asmdef 已随内容迁至该子目录、覆盖整个 Sunset 子树，他人测试区请建 Test/名字/ 并自带 asmdef。程序集：Sunset.Test 运行时程序集 + Sunset.Test.Editor 编辑器程序集，均 autoReferenced:false，主工程引用不到测试代码；2026-10-17 起 Sunset.Test 显式引用 Runestone.AesirArchitecture + Runestone.AesirModules——测试 UI 直接用 Aesir 框架，依赖单向）。沙盒规则见 Assets/Test/Sunset/README.md：禁用 Resources/StreamingAssets、禁自定义 Tag/Layer、测试场景不进 Build Settings；Assets/Test 走正常 git 提交（已提交于 0877b6e）。How to apply: 测试/原型验证优先在 Assets/Test/Sunset（他人测试区建 Test/名字/）；测试 UI 面板继承 AesirBasePanel、经 UIModule.RegisterPrefab/Show 驱动，场景只放引导脚本+主相机（UIRoot/UIModule/EventSystem 运行时自动创建）；已有 LoginPanel.prefab + HelpPanel.prefab（按项目 Resources 资产管理约定存放于 Assets/Resources/UIPrefabs，经 UIModule path 参数加载：Show&lt;LoginPanel&gt;("UIPrefabs/LoginPanel")、Show&lt;HelpPanel&gt;((object)"操作说明", "UIPrefabs/HelpPanel")，无需序列化引用）+ LoginUI.unity（已预置用户的 Assets/Resources/UIRoot.prefab，引用齐全，UIRoot.Instance 复用不重建）：登录界面四按钮（新游戏/继续游戏/说明内容/退出）位于右下角（用户 2026-10-17 拖定：x=-300，y=410/328/246/164），左侧白模人形占位图 Assets/Test/Sunset/Art/PlaceholderFigure.png（下挂提示"主视觉占位"，后续换 Image.sprite 即可），背景占位待挂图；说明内容按钮弹 HelpPanel——2026-10-17 已按用户要求改为面板形态并由 HelpWindow 更名（AesirBasePanel、RectTransform 根无 Canvas、layer=Popup、蒙版点击/×按钮→HideSelf 双通道关闭），中文经 SandboxUIFonts 运行时挂系统字体。









- [2026-10-17 21:37:31] activeInputHandler 热切换陷阱（已解决，重启生效）：activeInputHandler 是编辑器进程启动时读取的原生输入后端设置。2026-10-17 经 git 合并把 0→2 后未重启，导致编译宏/模块全切（InputSystemUIInputModule 生效）但 InputSystem.devices=0、真实鼠标点击 UI 无响应；重启编辑器后恢复（鼠标键盘设备齐全，虚拟鼠标注入点击端到端验证通过）。本机 2022.3.62t16 实测 2=旧+新双输入并存（旧 Input.* 可读、InputSystem 设备同时存在），与 wiki《旧Input迁移新Input静默失效》"2=仅New"的说法不符，以实测为准。wiki《团结引擎Input-System后端缺陷桥接》的 Refresh+ImportAsset+RequestScriptReload 热生效配方对 git 改动的设置无效。另：对 OS 鼠标设备 QueueStateEvent 注入点击无效（FastMouse 布局拦截，设备值变了但模块收不到事件通知），验证输入管线要用 InputSystem.AddDevice<Mouse> 虚拟鼠标注入。How to apply: activeInputHandler 被 git/外部工具改动后必须重启编辑器；Play 模式发现"设备总数 0 但模块是 InputSystemUIInputModule"就是这个坑；测输入管线端到端用虚拟鼠标，别用 Mouse.current 注入。
- [2026-10-17 23:59:37] 设置按钮测试区（2026-10-17，全部在 Test 沙盒）：Assets/Test/Sunset/Scripts/Settings/ 三脚本（GameSettings 静态设置状态+PlayerPrefs 持久化、键前缀 TestSettings.*；SettingsPanel 继承 AesirBasePanel 弹窗；SettingsTestBoot 测试场景引导：运行时搭左下角"设置"测试按钮+进场景自动开面板）+ Resources/UIPrefabs/SettingsPanel.prefab（13 字段接线）+ Assets/Test/Sunset/Scenes/SettingsTest.unity 独立测试场景（主相机+引导，UIRoot 运行时自建）。功能：分辨率档位 ◀/▶（720p~1440p；Screen.SetResolution 在编辑器被固定 Game View 档位钳制、打包后完整生效，UI 用 ◀▶ 改档位）、音效/音乐双滑条（音效占位应用 AudioListener.volume 且启动即应用 ApplySfxVolume，音乐先持久化待 Aesir AudioModule）、中英文即时切换（面板全部标签跟随刷新，选中按钮高亮）。How to apply: 后续正式设置功能从此区迁移；面板改分辨率/音量后记得调 RefreshVisuals 刷新 UI；新增音量分轨时把 GameSettings.SetSfxVolume/SetMusicVolume 里的占位逻辑换掉。


- [2026-10-18 00:07:25] 场景流程现状（2026-10-18 用户改定）：Bootstrap 经 SceneModule.LoadSceneSingle(ScenePaths.MainScene) 直切主场景 Assets/Scenes/MainScene.scene（空场景，玩法后续再建；Build Settings：Bootstrap index 0、MainMenu 与 MainScene 均已登记）。主菜单已建成并保留为独立资产、暂不在启动链上：MainMenu.scene（MainMenuBoot 引导 + 主相机）+ Resources/UIPrefabs/MainMenuPanel.prefab（Normal 层四按钮：新游戏/继续游戏占位日志、说明内容、退出）+ MenuHelpPanel.prefab（Popup 层操作说明弹窗，×与蒙版双通道关闭）；脚本在 Assets/Scripts/View（Game.View）：MainMenuBoot/MainMenuPanel/MenuHelpPanel；路径常量在 Assets/Scripts/Core 的 ScenePaths（MainMenu/MainScene）与 UIPaths；中文经 GameFonts 运行时挂系统字体。全部资产由 Assets/Editor/MainMenuBuilder.cs 生成（菜单 Tools/主菜单/重建主菜单资产，重跑=覆盖产物；占位图拷贝在 Assets/Art/MainMenu/，不依赖沙盒）。端到端已验证（Bootstrap→MainMenu 链路含四按钮与弹窗双通道、Bootstrap→MainScene 直切；退出按钮未实点——点了会退出 Play）。How to apply: 改版式改 MainMenuBuilder 再重建（勿手改预制体）；要把主菜单接回流程时改 GameBootstrap 目标路径，或从主场景内 Show&lt;MainMenuPanel&gt; 即可。


### Reference
- [2026-10-17 21:23:29] 游戏策划案《边际棋盘 Edge Grid》v1.0 位于 C:\Users\Sunset\Desktop\111.md：20×20 棋盘实时铺俄罗斯方块式拼块、冰/火双属性 AI 按方向偏好（右>下>左>上）贪心寻路到终点；三星=通关+金币+限时90s；块数上限40；特效层（转化/传送门/位移/滑行）连锁上限20步；失败回铺设阶段不停表。
