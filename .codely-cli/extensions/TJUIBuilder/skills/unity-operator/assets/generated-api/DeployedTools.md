# 部署型工具 API 卡（cn.tuanjie.create.ui · Editor/UIBuildTools）

> 本卡是全部一行调用工具的**签名权威**：签名不确定时查本卡，**禁止预读工具源码**（仅 marker FAIL、或文档与报错矛盾需 tie-break 时才读对应工具源码）。
> 调用方式：`execute_csharp_script`（宿主工具名）一行调用。多数工具只写 `context/` JSON（按部署型工具豁免条款免激活 unity-operator）；触碰 `Assets/` 或 Editor 状态的工具仍须先激活 unity-operator（见 unity-ui-builder 主 SKILL 权威映射表）。
> 通用判定：**以 marker 日志为准**（PASS/FAIL + 明细行），返回值仅供脚本内分支。

## 文档与清单构建

| 工具方法 | 签名 | 返回 | marker / 行为 |
|---|---|---|---|
| 文档占位符+章节检查 | `Create.GameFramework.TJDocChecks.CheckDocPlaceholders("{GameName}")` | `bool` | `[DOC_PLACEHOLDER_CHECK] PASS/FAIL`（只读） |
| 图片需求自检 | `Create.GameFramework.TJDocChecks.ValidateImageRequirements("{GameName}")` | `bool` | `[IMAGE_REQ_CHECK] PASS/FAIL`（只读；含 art_text 必填检查） |
| 图片需求清单构建 | `Create.GameFramework.TJBuildImageRequirements.Run("{GameName}")` | `void` | `[IMAGE_REQ_CHECK] PASS/FAIL`；**PASS 才写 image_requirements.json，FAIL 不落盘**；重跑自动继承既有 JSON 的 `prompt`/`generation_path`/`aspect_ratio` |
| Brick 映射草稿 | `Create.GameFramework.TJBrickMappingDraft.Mapping("{GameName}")` | `bool` | `[BRICK_MAP_CHECK]`；写 brick-mapping.md |
| Brick 覆盖表草稿 | `Create.GameFramework.TJBrickMappingDraft.Coverage("{GameName}")` | `bool` | `[BRICK_MAP_CHECK]`；需 UI设计文档 §4/§11 已就绪，任意时点（含首次）可用 |

## 图片生成流水线

| 工具方法 | 签名 | 返回 | marker / 行为 |
|---|---|---|---|
| 路径/尺寸/宽高比/透明判定 | `Create.GameFramework.TJImageGenPipeline.Precompute("{GameName}")` | `bool` | `[PRECOMPUTE_CHECK]` + `[SIZE_CHECK]` 逐行审计 |
| 提交计划/恢复预检 | `Create.GameFramework.TJImageGenPipeline.PlanSubmissions("{GameName}")` | `bool` | `[IMG_RESUME_CHECK]` / `[PLAN_CHECK]` |
| 下载核对+结果组装 | `Create.GameFramework.TJImageGenPipeline.BuildResult("{GameName}")` | `bool` | `[RESULT_CHECK]`；Path C 成员未切片但原始 sheet 在 → 记 `pending_slice` 不算 failed，切片后重跑终检 |
| Path A 直用落位 | `Create.GameFramework.TJImagePostprocess.DeployDirectUse("{GameName}")` | `void` | 无 direct_use 条目为 no-op |
| 图集切片 | `Create.GameFramework.TJImagePostprocess.SliceSpriteSheets("{GameName}")` | `void` | 分组/方向/槽位自动读 JSON；分段数不足 → FAIL 行提示回退 Path D |
| 复合切片 | `Create.GameFramework.TJImagePostprocess.SliceComposites("{GameName}", groups, orientations)` | `void` | 按需（参考图路线） |
| 缩放验证 | `Create.GameFramework.TJImagePostprocess.ScaleVerify("{GameName}")` | `void` | `[Verify]` 逐行 + 总结行；逐资产 TextureImporter |
| 图片完整性校验 | `Create.GameFramework.TJValidateAssets.Images()` | `bool` | `[ImageCheck] ✅/❌`（扫全部 Assets/Games/*/UI/Images） |

## 面板构建与校验

| 工具方法 | 签名 | 返回 | marker / 行为 |
|---|---|---|---|
| UGUI Panel Prefab 校验 | `Create.GameFramework.TJValidateAssets.UguiPrefabs()` | `bool` | `[PrefabCheck] ✅/❌` + 写 uiv-validation.json |
| USS 强制重导入 | `Create.GameFramework.TJValidateAssets.ReimportUss()` | `bool` | `[ReimportUSS]`（仅 UI Toolkit 模式） |
| GameScripts 部署 | `Create.GameFramework.TJGameScriptsDeploy.Run("{GameName}")` | `string` | context↔Assets 双写部署报告（触碰 Assets，须激活 unity-operator） |

## State 文件 typed 更新（禁止手改 JSON）

| 工具方法 | 签名 | 返回 | 说明 |
|---|---|---|---|
| 查看状态 | `Create.GameFramework.TJStateHelper.Show("{stateFile}")` | `bool` | 打印 current_stage/各阶段/tasks |
| 阶段状态 | `Create.GameFramework.TJStateHelper.SetStage("{file}", "{stage}", "{status}"[, next: new[]{"{下一阶段}","{status}"}, current: "{current_stage}"])` | `bool` | 文件必须已存在（由调用方按 schema 创建） |
| 任务状态 | `Create.GameFramework.TJStateHelper.SetTask("{file}", "{stage}", "{name}", taskId: "...", status: "...", outputUrl: "...", resultPath: "...", error: "...")` | `bool` | 后 4 参可选 |

## 回归工具（改 UIBrick 定义 / 动效库后运行）

| 工具方法 | 签名 | 返回 | 判据 |
|---|---|---|---|
| Brick 装配回归 | `Create.GameFramework.TJBrickAssemblyTests.RunAll()` | — | 日志无 `BRICK_ASSEMBLY_TEST_FAILED` |
| Brick 声明面契约回归 | `Create.GameFramework.TJBrickContractTests.RunAll()` | — | 日志无 `BRICK_CONTRACT_TEST_FAILED`（数据驱动扫描全部 17 bricks：Controls+Layout+Container 的全部声明参数：零 `[BrickFactory]` 告警 + 值落点回读；独立于 T35e，见 `Editor/UIBuildTools/BrickContractTests/TODO.md`） |
| Play 交互契约回归 | `Create.GameFramework.TJBrickContractPlayTests` 四步协议：`RunAll()`（武装+进 Play）→ in-flight `return TJBrickContractPlayTests.ArmSequence();` → `return TJBrickContractPlayTests.Verdict();`（返回结果字符串）→ `ExitPlay()` | — | 日志无 `BRICK_CONTRACT_PLAY_TEST_FAILED`（契约测试子功能文件夹 `Editor/UIBuildTools/BrickContractTests/`，用例真源 `PlayContractTestCases.md`；in-flight 驱动必需——暂停/空闲会冻结帧推进行为，见该文档 §1 帧驱动） |
| 动效回归 | `Create.GameFramework.TJAnimationRegressionTests.RunAll()` | — | 日志无 `UI_ANIMATION_TEST_FAILED` |

> 覆盖范围：本卡覆盖 UI 构建主链路（doc-builder / image-generator / panel-builder / scene 阶段）的一行调用工具。`ui-style-reference` / `ui-layout-intent-designer` 模块专属工具（`TJStyleRefTree` / `TJResolveUiTree.Run(gameDir, layout, output, check, force)` / `TJLayoutIntentDraft.Run(...)` / `TJLayoutMarkdown.Run(gameName)`）以各自模块 SKILL.md 内的签名说明为准，不在本卡重复维护。
