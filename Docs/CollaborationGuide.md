# 团队协作规范

> 本项目为团结引擎 2D 项目模板。**不使用 Git LFS**，所有美术/音频/字体等大体积资源走 U 盘离线同步。

## 〇、人员与职责
| 姓名 | GitHub账号 | 职责 | 负责文件 |
| --- | --- | --- | --- |
| 枫 | {待填} | 主程 / 仓库管理员 | Main.unity、ProjectSettings、Packages、*.inputactions、Tilemap |
| 云 | {待填} | 程序 | Test_Yun.unity 及相关脚本 |
| 新 | {待填} | 程序 | Test_Xin.unity 及相关脚本 |
| 刘 | {待填} | 策划 | U盘资源管理与资源命名规范 |
| 吕 | {待填} | 策划 | U盘资源管理与资源命名规范 |

## 一、大文件传输规则（U盘离线同步）
1. 所有美术（图片、音频）、字体（.ttf/.otf）、视频等大体积资源**严禁通过 Git 推送**，统一使用 U 盘拷贝。
2. **拷贝时必须连 .meta 文件一起拷贝**。这是 GUID 一致性的唯一保障；漏拷 .meta 会导致所有人的 Prefab 引用断链，且很难恢复。
3. **拷贝前必须关闭 Unity/团结编辑器**，拷贝完成后再打开。
4. 新成员首次 onboarding 顺序：安装团结引擎 1.8.4 → 配置 Git 与 SmartMerge 驱动（运行 tools\setup-git-merge-driver.ps1）→ 拷贝 U 盘资源到对应目录 → 再 clone / 打开工程。
5. 资源目录骨架（clone 后不存在，需要按 U 盘资源建立）：
   `Assets/Art/Images/`、`Assets/Art/Audio/`、`Assets/Art/Models/`、`Assets/Art/Videos/`、`Assets/Art/Fonts/`
6. 图片导入设置（Sprite Mode / Pixels Per Unit / 压缩）由一人定稿，统一用 Preset（.preset 是文本资产，走 Git）下发，避免各人设置不一致。
7. 策划（刘、吕）负责 U 盘的管理与资源命名规范，代码同学（枫、云、新）负责导入 Unity。
8. 2D项目不需要任何3D模型、光照烘焙数据、NavMesh寻路数据。

## 二、Prefab 与场景协作规则
1. **一人一文件铁律**：同一个 `.prefab` 或 `.unity` / `.scene` 文件任何时间只能由一个人修改。
2. 动态创建的 Prefab 必须放在 `Assets/Resources/Prefabs/` 目录下，代码使用 `Resources.Load<GameObject>("Prefabs/XXX")` 加载。
3. 不同功能的变体使用 `Create -> Prefab Variant` 模式进行继承修改，不要直接修改基础 Prefab。
4. 主场景（Main.unity）只能由枫修改，云和新使用 `Test_Yun.unity`、`Test_Xin.unity` 做测试。

## 三、其他高频冲突文件处理规约
1. **ProjectSettings / Packages**：只能由枫一人修改和提交，其他人严禁私自新增包或修改 Tag/Layer。
2. **Tilemap / Palette (.asset)**：只能由枫负责绘制，其他人只读。
3. **材质 (.mat) / 物理材质 (.physicsMaterial2D)**：一人一文件，走 Git。
4. **动画 (.anim, .controller)**：尽量用代码做位移/缩放，少用 Animator。若必须用，走 Git 且一人一文件。
5. **输入映射 (.inputactions)**：由枫统一维护，其他人不许修改。
6. **.meta 文件**：脚本/场景/Prefab/.asset 的 .meta 必须全部走 Git，严禁忽略或删除，否则资源引用全断；`Assets/Art/**` 下资源的 .meta 随 U 盘同步，不进 Git。

## 四、代码加载规范
1. 代码中禁止写死资源拖拽引用，必须使用 `Resources.Load` 字符串路径加载。
2. 加载后使用 `GetComponent<T>()` 获取组件，并做空值检查。

## 五、Git 协作流程（保持简单，适合新手）
### 5.1 总原则
- **只有纯代码（.cs 等）走分支 + PR**。
- **场景（.unity/.scene）、Prefab、材质、Tilemap、ProjectSettings、Packages、Art 资源不走分支**，由负责人在 main 上单线修改、改完立刻推。
- 原因：团结/Unity 的场景与 Prefab 是文本 YAML，两个人分别改**不同**物体，Git 也会判成冲突，而且几乎无法人工合并。走分支只会把"当场的小冲突"推迟并放大成"合并时无解的大冲突"。
- 每个人在改文件前，先确认没有别人在改同一个文件（群里说一句）。

### 5.2 改代码：走分支 + 自己合并 PR
```
git checkout main
git pull                                    # 先拉最新的
git checkout -b feat/名字-功能               # 建自己的分支，例：feat/yun-ui-hp
# ……改代码……
git status                                  # 确认没有把 Library、Art 里的图片带进来
git add .
git commit -m "feat: 云 - 血条UI"
git push -u origin feat/yun-ui-hp
```
然后到 GitHub 网页：`Compare & pull request` → 写一句说明 → `Create pull request` → `Squash and merge` → `Delete branch`。

### 5.3 合并前先跟上主干（有冲突就停下来找人，不要硬解）
```
git checkout main
git pull
git checkout feat/yun-ui-hp
git merge main
git push
```

### 5.4 改场景 / Prefab / 资源（不走分支）
```
# 先确认没人在改同一个文件
git checkout main
git pull
# 打开 Unity 修改，保存并关闭 Unity
git add Assets/Scenes/SampleScene.scene Assets/Scenes/SampleScene.scene.meta
git commit -m "chore: 枫 - 更新场景"
git push
```

### 5.5 场景与资源特例（很重要）
- **新增美术资源不要走分支**：资源不在 Git 里，分支对它无效。由策划/枫直接更新 main 并通知全员同步 U 盘。
- 某分支如果需要用到新资源，必须**先让资源进入 main 并通知全员同步 U 盘**，再在自己的分支里引用；否则别人拉到你的分支就是一片 missing。
- 合并任何"含场景/Prefab 改动"的分支前，必须由枫确认。

### 5.6 三条硬规则
1. 只改自己负责的文件（见〇、三章）；不确定就先问枫。
2. **绝不使用 `git push --force`**，绝不删除别人的文件。
3. 出现冲突或看不懂的报错：**不要乱点、不要强行解决**，立刻截图发群里，由枫处理。

### 5.7 一次性配置（每人只做一次）
- 运行项目里的 `tools\setup-git-merge-driver.ps1`（右键 → 使用 PowerShell 运行），让 Git 能自动合并场景/Prefab 冲突。
- 执行 `git config --global core.autocrlf false`。
- 不需要安装 Git LFS。

### 5.8 分支纪律
- 一个任务一个分支，命名 `feat/名字-功能`。
- 分支**必须在 3 天内合并**，禁止长期挂分支。

### 5.9 Unity 使用注意
- **切分支 / pull 前先关闭 Unity**，操作完再打开（Library 会重新导入，需要等待）。
- 切分支后若出现莫名其妙的报错：关闭 Unity → 删除 `Library` 文件夹 → 重新打开工程让其重建。

## 附录 A：命令速查（可贴到群里）
| 我要做什么 | 命令 |
| --- | --- |
| 更新别人的改动 | `git pull` |
| 看我改了哪些文件 | `git status` |
| 提交我的改动 | `git add .` 然后 `git commit -m "说明"` |
| 上传到 GitHub | `git push`（分支首次用 `git push -u origin 分支名`） |
| 切回主干 | `git checkout main` |
| 出现冲突或报错 | 不要操作，截图发群 |

## 附录 B：术语一句话
- **提交（commit）**：把改动记录到本地。
- **推送（push）**：把本地改动上传到 GitHub。
- **拉取（pull）**：把别人上传的改动下载到本地。
- **分支（branch）**：从主干复制出来的一条独立开发线，互不影响。
- **PR（Pull Request）**：请求把分支的改动合并进主干；本项目默认用 Squash and merge（多个提交压成一条记录）。
- **冲突（conflict）**：两个人改了同一处，需要人决定保留谁。

## 附录 C：GitHub 仓库设置（由枫维护）
- 邀请协作者时权限用 **Write**，不要给 Admin。
- Settings → General → Pull Requests：建议只保留 `Allow squash merging`。
- Settings → General → 建议勾选 `Automatically delete head branches`。
- （可选）分支保护：只勾 `Require a pull request before merging`，**不要勾** `Require approvals`，否则自己不能合并自己的 PR。
- 本仓库不使用 Git LFS。
