---
name: csharp-code-writer
description: Create UI 框架 C# 脚本的编写与修改技能——编写 UIGameInstaller、Panel 控制器（UGUIPanelBase/UIPanelBase 子类）、HUD 控制器、游戏↔UI 胶水脚本（状态切换/事件订阅/数据绑定），或后续多轮对话里改 bug/调参/加功能。统一走「context↔Assets 双写 → 部署 → RoslynValidator 编译验证 → Play 冒烟」闭环：只改 context/GameScripts 而不部署到 Assets 等于改动不生效；编译过 ≠ 改对了（UI 遮挡、面板不显示、绑定空引用这类编译和 Play 都不报错的问题，唯有进 Play 冒烟能抓）。
---

# C# Code Writer

> 执行本文及子 references 中的 `{PYTHON}` 命令前，读取 `../unity-ui-builder/references/python-contract.md` 的"跨平台 Python 解析"合同。

> **本技能被刻意拆成三个阶段，每个阶段的详细约束放在独立文件，进入该阶段时才 `read_file`。**
> 目的：起草时上下文里只有"当前阶段"的少量约束，后面阶段的几十条检查规则那一刻不在场——从结构上根除"读完文档迟迟不敢写第一行"的决策瘫痪，以及"一次生成反复权衡把请求撑爆"（服务端 301s 流式超时不可调，超时=任务 partial、零产出）。
>
> ```
> [先在这里] 判断场景 + 守住 3 条边界
>      │
>      ├─ 阶段1 骨架   → read references/阶段1-骨架.md → write_file 落空壳（骨架前禁读 .cs 源码）
>      ├─ 阶段2 补全   → read references/阶段2-补全.md → 逐个 replace 填方法体（此时才查签名）
>      └─ 阶段3 验证   → read references/阶段3-验证.md → 检查清单+RoslynValidator（改bug场景再部署+Play冒烟）
> ```
>
> ⛔ **一次只读一个阶段文件、只做那个阶段的事，不要提前把三份都读进来**。骨架前连续 3~4 轮只读即立即起草；**骨架已经落盘后，阶段 2 连续 2 轮只查不 `replace`，或重复搜索同一框架类型，即判定停滞：以 API 参考文档（UIFrame-API.md / UIAnimation-API.md）当前结果为准，立即填下一个方法。**

> ## 写入工具失败恢复（所有阶段）
>
> `write_file` / `replace` 返回 timeout/error/unknown 时，先读回目标确认实际状态；完整则继续，部分或缺失则保留 checkpoint、缩小为 ≤60 行重试一次，仍失败再读回并以 ≤30 行重试第二次。`replace` 失败必须刷新锚点，禁止复用旧内容；禁止原样重发巨型文件。两次失败后才可 partial，并报告已落盘方法与剩余方法。模型流若在工具调用发出前被 301s 截断无法自恢复，所以阶段 1 的首个写入必须尽早发生。

---

## 三条边界（贯穿全程，每次操作自检）

1. **路径**：`.cs` 只写到 `context/games/{GameName}/GameScripts/*.cs`；**只有阶段 3 部署时才进 `Assets/`**。发现自己即将写 `Assets/` 下 `.cs` → 立即停、改回 `context/`。
2. **编译**：本技能只跑 RoslynValidator。当前 prompt 若明确要求"只写 GameScripts、不部署"，**禁止调 `unity_editor.start_compilation_pipeline`/`request_compile`**；完整 Unity 编译留给后续全量部署任务。仅改 bug 场景在阶段 3 部署后才用它触发 Reload；同步工具返回即编译完成，以返回内嵌的 `console.entries` 为准，禁止在其前后再单独调用 `unity_console clear`/`get` 破坏诊断边界，且仅 Bridge 实际报告断开/陈旧时调用 `unity_refresh` 后重试。
3. **源码阅读**：本技能只写 UI 框架相关脚本。**骨架阶段（阶段1）一律不读任何 API 卡或 `.cs` 源码**；按架构文档标题/语义读取业务接口、基类与事件契约，不依赖固定章节号。阶段 2 才查签名，权威参考文档固定为 `../unity-ui-builder/modules/ui-panel-builder/references/UIFrame-API.md`（框架/面板/导航类）与 `../unity-ui-builder/modules/ui-panel-builder/references/UIAnimation-API.md`（动效类），以框架源码与该参考文档为准；参考文档缺失单一类型时才定向窄读源码声明。禁止全文读取完整参考文档。⛔ 该相对路径从本技能根（`extensions/TJUIBuilder/skills/csharp-code-writer/`）解析到 extension 下权威文档；grep 命中 `skill-workspaces/**/skill-snapshot/**` 的同名文档均为过期 eval 快照，一律忽略。

> **配置前提**：本工程 AssetDatabase 自动 refresh 已关闭，故在 Unity 外用 RoslynValidator 做 Full 验证，严格按"创建→验证→部署"流程。

---

## 先判断场景（决定跑到哪一步、要不要部署）

本技能有三类场景，只根据当前 task prompt 对号入座：

| 场景 | 判断条件 | 跑到哪 |
|------|---------|--------|
| **分批写脚本（首轮开发）** | prompt 明确列出本批脚本，并要求只写 `GameScripts`、不部署 | 阶段1 → 阶段2 → 阶段3 的 RoslynValidator 即止。其它范围外类型依赖须精确归因并留给全量任务。**跳过部署与 Play 冒烟** |
| **全局编译部署** | prompt 明确要求把 `GameScripts` 全部产出一次性部署、编译并验证类型 | **不走三阶段起草流程**，改按 `references/2b-全局编译部署.md` 唯一真相源执行 |
| **改 bug / 调参 / 加功能** | 后续多轮对话里改已有 UI 脚本 | 走完整三阶段，且阶段 3 继续部署+等 Reload+Play 冒烟（改完必须落地并确认改对了）。⚠️ 用本技能自己的部署工具 `Create.GameFramework.TJGameScriptsDeploy.Run("{GameName}")`（包 `Editor/UIBuildTools`） |

> 分批写脚本时，当前任务只处理 prompt 列出的文件；不要根据计划或工程缺口扩大范围。全局部署由单独任务统一完成。

**分批写脚本场景硬约束**：严禁把脚本写进 `Assets/`（不完整脚本会触发编译错误）；严禁为修 CS0246 去改当前 prompt 范围外的脚本或加通用临时 stub。错误归属与共享类型归属见 `references/检查清单.md` G 节。

---

## 进入阶段（除全局编译部署外，其余场景一律从阶段 1 开始，按顺序推进）

1. **阶段 1 · 骨架先落盘** → `read_file` 读 `references/阶段1-骨架.md`。
   - 契约 pass 按标题/语义读取架构文档 → 定 4 件事 → 按模板 `write_file` 落业务 API 空壳；框架 override 留到阶段 2。**骨架落盘前禁读任何 `.cs` 源码。**
2. **阶段 2 · 补全方法体** → `read_file` 读 `references/阶段2-补全.md`。
   - 先按 API 参考文档（UIFrame-API.md / UIAnimation-API.md）对本批框架契约做一次有界核对，再逐个/逐组 `replace` 填方法体；参考文档仅可单类型定向 grep，不得边猜签名边反复搜源码。
3. **阶段 3 · 验证（+改 bug 场景的部署+冒烟）** → `read_file` 读 `references/阶段3-验证.md`。
   - 读检查清单 A/B/C 修补 → RoslynValidator（≤10 次）→ 写脚本场景到此止；改 bug 场景继续部署+Reload+Play 冒烟。

---

## 与 `unity-operator` 的配合

编写依赖编辑器/资源操作的脚本时，必须与 `unity-operator` 生成的对应脚本、资源配置保持严格一致。

---

## Success Criteria

- ✅ 任务清单中所有脚本已创建（或已记录失败）、且完全实现（无空框架/占位符）
- ✅ 所有脚本经 Full 级 RoslynValidator 验证（最多重试 10 次；仍失败的已记录错误与重试次数）
- ✅ 代码结构符合架构文档，UI 框架 API 签名与 `../unity-ui-builder/modules/ui-panel-builder/references/UIFrame-API.md` / `UIAnimation-API.md` 一致
- ✅ 初稿完成后已读检查清单并按 A/B/C 修补
- ✅ **写脚本场景**：脚本留 `context/.../GameScripts/`、**不部署**；Roslyn 错误逐项精确归因，未泛化抑制 CS0103。**改 bug 场景**：已部署且 `Assets/.../Scripts/` 与 `context/.../GameScripts/` 的 `.cs` 文件名集合完全一致（`Compare-Object` 差异为空、数量相等），并已过 Play 冒烟
