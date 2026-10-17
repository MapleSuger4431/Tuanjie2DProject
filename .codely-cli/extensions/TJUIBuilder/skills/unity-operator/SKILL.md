---
name: unity-operator
description: Unity Editor 自动化写操作门禁与路由（UI 构建域）。主 agent 和所有 subagent 只要即将用 execute_csharp_script 改变 Editor、项目或运行态状态（GameObject/组件/属性、场景、Prefab/SO、材质、UI 资产、.meta、Package），第一步都必须激活本技能；用户未明确要求、为验证而自发修改也不例外。纯读取/查询免激活。
---

# Unity Operator

> 执行本文路由到的规格中的 `{PYTHON}` 命令前，读取 `../unity-ui-builder/references/python-contract.md` 的"跨平台 Python 解析"合同。

本页只定义门禁、交付物路由和当前任务闭环。激活后仅定向读取与 prompt 交付物匹配的一份规格；只有签名不确定时才查生成卡。

## 激活边界（不可丢失）

- 任何 `execute_csharp_script` 写操作前先激活本技能；主 agent、subagent、临时诊断脚本同规则。
- 纯读不激活 operator：编辑器状态用 `unity_editor.get_state`，场景对象用 `unity_gameobject` 查询动作，资产元数据用 `unity_asset` 查询动作，Console 读取用 `unity_console get`，UI 结构校验用 UIV。从读转写时，写前必须回来激活 operator。

## 交付物 profile 路由

只读命中的规格；确有横切需求时最多补读一页。工具的真实 public 签名统一查对应生成卡，但不要预读生成卡。

| 交付物 profile | 首选 | 必读 |
|---|---|---|
| Panel Prefab / UIRoot Prefab | UIBrick | `../unity-ui-builder/modules/ui-panel-builder/references/ugui/ugui-prefab-construction.md`（构建 runner `BrickPanelBuilder.BuildBrickTrees`） |
| PanelRegistry SO | PanelRegistryBuilder | `assets/generated-api/PanelRegistryBuilder.md` |

框架 API 参考文档：`../unity-ui-builder/modules/ui-panel-builder/references/UIFrame-API.md`（框架/面板/导航类）与 `../unity-ui-builder/modules/ui-panel-builder/references/UIAnimation-API.md`（动效类），以框架源码与该参考文档为准。

UI 图片导入（TextureImporter sprite 设置/mipmap 关闭）的验证由部署型工具 `Create.GameFramework.TJValidateAssets`（`cn.tuanjie.create.ui` 包 `Editor/UIBuildTools`）承载，不在本技能重复定义。

## 当前任务的通用闭环

### Bridge-first Editor 事务

- 当前任务的执行者负责编写、调用与修复自己的 EditorScript，避免跨执行者移交导致上下文丢失。
- Bridge 在 `execute_csharp_script` 执行前编译脚本并返回编译错误。一个 Prefab 的创建、配置、接线、Save 和清理必须在同一次调用内完成；Bridge 保证调用不交错，但失败不自动回滚，脚本仍须幂等和 `try/finally` 清理。
- 发现跨域 C# 缺陷时停止并报告 blocker；禁止在 Prefab 任务内越界修改 GameScript。

1. 读 prompt 和交付物规格，并按上表定向读 API 卡；不要通读文档树。
2. 先按持久化交付物确定脚本粒度。**Prefab 任务默认一个目标 Prefab 对应一个结构完整、尽量幂等、可独立重放的 EditorScript**；当前任务有多个 Prefab 时写多份短脚本并逐个执行/验证，禁止把多个无原子关系的 Prefab 塞进一份复杂脚本。反过来，同一个 Prefab 的创建、组件配置、内部接线和首次 `Save()` 必须留在同一脚本，禁止拆成多段补线。非 Prefab 任务默认一个内聚脚本，仅因独立产物、非幂等边界、超时风险或独立重试边界才拆分。
3. 先把脚本写到 `context/games/{GameName}/EditorScripts/{Action}.cs`，随后由当前任务直接调用 `execute_csharp_script`。Bridge 会先编译再执行；编译失败只修原文件，禁止另建 fix 文件。**每份 EditorScript 开头建议带幂等哨兵**（目标产物已存在且输入未变 → 直接返回现有校验结果并跳过重建）——bridge 实测偶发单次调用双重执行，非幂等脚本会重复副作用。
4. 每份脚本执行后立即消费工具返回报告（构建结果 / Prefab 校验 / Save 报告）作为该产物的**首验**；报告含错误标记即停在当前产物修复，不继续执行当前任务的下一份脚本，也不另写重复只读探针。单次执行日志已自动捕获，不在每段后查 Console。
5. 每份脚本成功后只更新 `EditorScripts/execution-manifest.md`，记录顺序、脚本路径和用途。存量 `execution-log.md` 仅在 manifest 不存在时只读兼容；禁止追加、改写或双写旧 log。
6. 任务末尾仅做一次增量 Console 检查，清除本次执行残留 Error。

## 横切硬红线

- Roslyn 脚本只写顶层语句；禁止类、`Main`、`[MenuItem]`。注释和字符串只写 ASCII 英文。
- UI 框架域用 `using Create.GameFramework;`。业务类型用全限定名，防止同名类型静默解析错误。
- 禁止 `AssetDatabase.Refresh()`、阻塞弹窗、`AppDomain.GetAssemblies()` / `Assembly.LoadFrom()` 扫描、硬编码 `context/games` 模板根。
- 禁止凭记忆猜 API/序列化字段：先查 `assets/generated-api/`（`PanelRegistryBuilder.md` / `DeployedTools.md`——全部一行调用部署型工具的签名与 marker 权威），再查 `../unity-ui-builder/modules/ui-panel-builder/references/UIFrame-API.md` / `UIAnimation-API.md`；工具明确不覆盖时才定向读源码或回落裸 API。
- 资产路径以 `Assets/` 开头；材质先确认渲染管线；对象、组件、字段访问先判空。能用 `BrickPanelBuilder`/`PanelRegistryBuilder`/UIV 就不手写等价裸 API。

## 失败恢复

先读状态再补救，禁止对非幂等脚本盲目整段重跑：

1. 部分成功：用只读查询工具确认完成边界，只修未完成项。
2. 编译或工具失败：读报告与本次捕获日志，就地修改原脚本重跑；前置失败时跳过下游。
3. 超时：先排除弹窗、导入或 Domain Reload；确认状态后再决定重试。多次失败则删除未成功的中转脚本/残留资产并明确报告。
4. 修复后重新消费工具报告；任务收尾仍只做一次增量 Console 检查。
