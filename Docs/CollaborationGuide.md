# 团队协作规范（24 小时黑客松版）

> 目标只有一个：**工程不崩、能打包、能提交**。24 小时里规矩越少越有效，所以下面每一条都是为了"少吵架、少崩工程"而存在的。
> 新人请先看 `Onboarding.md`（环境准备 + 照着抄的操作）。
> 本项目**不使用 Git LFS**，美术/音频/字体等大体积资源走 U 盘离线同步。

---

## 一分钟版（背这三条就够）

1. **一人一文件**：同一个场景/Prefab/材质/Tilemap，任何时间只有一个人改。
2. **只改自己负责的文件**，`ProjectSettings`/`Packages` 只有枫能碰。
3. **冲突或报错就停下**，不要乱点、不要强行解决，截图发群。

---

## 〇、人员与职责

> 下表是**示例分工**，赛前按当天人数和模块实际情况再具体分配（前提仍然是"一人一文件"）。

| 姓名 | GitHub账号 | 职责 |
| --- | --- | --- |
| 枫 | {待填} | 主程 / 仓库管理员：主场景、ProjectSettings、Packages、输入映射、Tilemap |
| 云 | {待填} | 程序：`Test_Yun.scene` 及相关脚本 |
| 新 | {待填} | 程序：`Test_Xin.scene` 及相关脚本 |
| 刘 | {待填} | 策划：U 盘资源管理与资源命名规范 |
| 吕 | {待填} | 策划：U 盘资源管理与资源命名规范 |

---

## 一、文件类型与归属总表（最重要，先看这张）

| 文件类型 | 管理方式 | 谁能动 | 关键注意 |
| --- | --- | --- | --- |
| 代码 `.cs` | Git，各自分支开发 | 枫、云、新 | 动手前先 `git pull` |
| Prefab `.prefab` | Git，**一人一文件** | 见分工 | 变体用 `Prefab Variant`，不直接改基础 Prefab；动态加载的放 `Assets/Resources/Prefabs/` |
| 场景 `.scene` | Git，**一人一场景** | 主场景=枫；测试场景=各人 | 团结引擎场景扩展名是 `.scene` |
| `ProjectSettings/` | Git | **仅枫** | 别人要加 Tag/Layer 口头告诉枫 |
| `Packages/` | Git | **仅枫** | 赛前把基础包装好；比赛中尽量不装新包 |
| 美术 / 音频 / 字体 / 视频 | **U 盘拷贝** | 策划管理，代码导入 | 必须连 `.meta` 一起拷、关编辑器再拷 |
| 烘焙 / Lightmap / NavMesh / ReflectionProbe | `.gitignore` 排除 | 谁都不用 | 比赛不做光照烘焙与寻路 |
| ScriptableObject `.asset` | Git，一人一文件 | 见分工 | 数值**优先用表格（CSV/Excel）**；不要多人改同一个 SO |
| 动画 `.anim` / `.controller` | Git，一人一文件 | 见分工 | 2D 优先序列帧或代码做动画，少用 Animator |
| 材质 `.mat` | Git，一人一文件 | 见分工 | 2D 尽量用默认材质，少做自定义材质 |
| Tilemap / Palette `.asset` | Git | **仅枫** | 其他人只读，不碰 |
| 输入映射 `.inputactions` | Git | **仅枫** | 其他人不许私自改按键 |
| 第三方 `.dll` | Git | 仅枫导入 | 二进制、不可 diff、会让仓库变大，数量保持最少 |
| 工程元文件（`.gitignore`/`.gitattributes`/`Docs/`/`tools/`） | Git | **仅枫** | 其他人不要动 |

### `.meta` 怎么办（最容易搞错的一条）

规则是**跟着"资源本体"走**，分两种情况：

- **走 Git 的文件**（`.cs`/`.prefab`/`.scene`/`.asset`/`.shader` 等）→ 它们的 `.meta` **必须进 Git**，严禁删除或忽略；
- **`Assets/Art/` 下 U 盘同步的资源** → 它的 `.meta` **随 U 盘一起拷，不进 Git**。

两种情况共同的一点：**`.meta` 绝不能和资源分家**。meta 丢了或和资源对不上，Unity 里所有引用就会变粉（Missing）。

---

## 二、U 盘资源同步（三条硬约束）

1. **必须连 `.meta` 一起拷贝** —— 这是 GUID 一致性的唯一保障，漏拷 meta 会让所有人的 Prefab 引用断链。
2. **拷贝前必须关闭 Unity/团结编辑器**，拷贝完成后再打开。
3. **顺序：先拷资源，再打开工程**。反过来的话，场景打开时就是一片 missing。

补充：

- 策划（刘、吕）负责 U 盘管理与资源命名规范，程序（枫、云、新）负责导入 Unity。
- 资源目录骨架（clone 后不存在，按 U 盘内容建立）：
  `Assets/Art/Images/`、`Assets/Art/Audio/`、`Assets/Art/Models/`、`Assets/Art/Videos/`、`Assets/Art/Fonts/`
- 图片导入设置（Sprite Mode / Pixels Per Unit / 压缩）由一人定稿，统一用 **Preset**（`.preset` 是文本资产，走 Git）下发，避免各人设置不一致。
- 2D 项目不需要 3D 模型、光照烘焙数据、NavMesh 寻路数据。

---

## 三、Prefab 与代码加载细则

### 3.1 核心思路

**代码负责"找和组装"，Prefab 只负责"长什么样"。**

代码不直接拖拽引用 Prefab（拖拽引用会写死 GUID，Prefab 换个人做就断），而是按**约定好的路径**去加载。这样即使某个 Prefab 还没做好、暂时是空的，代码也不会因为"找不到它"而崩溃。

### 3.2 统一加载方式（本次比赛只用 Resources）

```csharp
// 1) 按路径加载（相对 Resources 目录，不带扩展名）
GameObject prefab = Resources.Load<GameObject>("Prefabs/Enemy_Slime");
if (prefab == null) { Debug.LogError("缺少 Prefab: Prefabs/Enemy_Slime"); return; }

// 2) 实例化
GameObject instance = Instantiate(prefab, position, rotation);

// 3) 动态获取组件（不要假设 Prefab 一定挂了这个脚本）+ 空值检查
var controller = instance.GetComponent<EnemyController>();
if (controller == null) { Debug.LogError("Enemy_Slime 上没有 EnemyController"); }
```

- 所有需要动态创建的 Prefab（敌人、子弹、特效、UI 弹窗等）**必须放在 `Assets/Resources/Prefabs/`** 下。
- 命名前缀约定（文件名即加载路径字符串，全队统一）：
  `Enemy_XXX`、`Bullet_XXX`、`FX_XXX`、`UI_XXX`
- **代价要知道**：`Resources` 里的资源会**无条件全部打进包体**，也会拖慢启动。比赛规模够用；以后项目变大再考虑 Addressables。

### 3.3 一人一文件 + 变体

- 同一个 `.prefab` 任何时间只能一个人改。
- 需要不同功能的版本时，右键基础 Prefab → `Create` → `Prefab Variant`，在**变体**上改，不要直接改基础 Prefab。

### 3.4 我要测试别人负责的 Prefab，怎么办

不要直接改正式 Prefab 再提交。按这个流程：

1. 把那个 Prefab **复制一份**到自己测试场景里改；
2. 测试成功后，把"加了什么组件、调了什么参数"**告诉负责人**；
3. 由**负责人**在正式 Prefab 上统一修改并提交。

### 3.5 主场景极简 + 运行时动态创建（强烈建议）

主场景（`Main.scene`，即最终整合用的场景）**保持极简**，只放一个"场景管理器"空物体，由它运行时按路径动态创建需要的 Prefab。

好处：每个人的物理改动完全隔离在各自的 `Test_XX.scene` 里，Git 几乎不会冲突，主场景也不会天天被几个人同时改。

### 3.6 策划数值表与代码的接口

- 策划的数值表里**写明每个 Prefab 的文件名**（如 `Enemy_Slime`），程序直接把这个字符串当加载路径。
- 数值数据**优先用表格（CSV/Excel）**维护：一人负责一张表（避免多人改同一张），导出后由代码解析。
- 具体格式与存放位置赛前商定（放 `Assets/` 下走 Git 的目录，**不要放 `Assets/Art/`**）。
- 如果你更想用 ScriptableObject：它是文本 `.asset`，可以正常走 Git，但**一人一文件**，并且不要多人同时改同一个 SO。

---

## 四、场景协作细则

### 4.1 一人一场景（强制）

绝对不允许两人同时打开并修改同一个 `.scene`。约定：

- **主场景**：只有枫在整合阶段修改。
- **测试场景**：云用 `Test_Yun.scene`、新用 `Test_Xin.scene`，各自随便折腾。
- 改场景前，先在群里说一句"我在改 XX 场景"。

### 4.2 场景冲突保底策略（不要手工合并 YAML）

真出冲突了，**不要试图手工合并场景文件**（大概率打开就崩）。做法：

1. 决定采用谁的版本，然后强制取用：
   ```
   git checkout --ours   Assets/Scenes/Test_Yun.scene     # 保留我自己的版本
   git checkout --theirs Assets/Scenes/Test_Yun.scene     # 保留对方（远端）的版本
   git add Assets/Scenes/Test_Yun.scene
   git commit -m "fix: 采用云版本的测试场景"
   ```
2. **被覆盖的一方**把自己的改动（Prefab 摆放位置等）重新拖回场景——通常只损失几分钟的摆放时间。
3. 在群里说明"我采用了我自己的版本"。

> 结论：宁可重摆 5 分钟，也不要花 1 小时去手改 YAML。

### 4.3 编辑器与切换

- **切分支 / `git pull` 前先关闭编辑器**，操作完再打开（Library 会重新导入，需要等待）。
- 切分支后出现莫名其妙报错：关闭编辑器 → 删除 `Library` 文件夹 → 重新打开让其重建。

---

## 五、Git 协作流程

### 5.1 总原则

- **只有纯代码（`.cs` 等）走分支 + PR**。
- **场景（`.scene`）、Prefab、材质、Tilemap、ProjectSettings、Packages、Art 资源不走分支**，由负责人在 main 上单线修改、改完立刻推。
- 原因：场景与 Prefab 是文本 YAML，两个人分别改**不同**物体，Git 也会判成冲突，且几乎无法人工合并。走分支只会把"当场的小冲突"推迟并放大成"合并时无解的大冲突"。

### 5.2 改代码：走分支 + 自己合并 PR

```
git checkout main
git pull                                    # 先拉最新的
git checkout -b feat/名字-功能               # 建自己的分支，例：feat/yun-ui-hp
# ……改代码……
git status                                  # 确认没有把 Library、Art 里的图片带进来
git add .
git commit -m "feat: 云 - 血条UI"
git push -u origin feat/名字-功能
```

然后到 GitHub 网页：`Compare & pull request` → 写一句说明 → `Create pull request` → **`Squash and merge`** → `Delete branch`。

### 5.3 合并前先跟上主干（有冲突就停下找人）

```
git checkout main
git pull
git checkout feat/名字-功能
git merge main
git push
```

### 5.4 改场景 / Prefab / 资源（不走分支）

```
# 先确认没人在改同一个文件
git checkout main
git pull
# 打开 Unity 修改，保存后关闭编辑器
git add Assets/Scenes/Test_Yun.scene Assets/Scenes/Test_Yun.scene.meta
git commit -m "chore: 云 - 更新测试场景"
git push
```

### 5.5 场景与资源的特例

- **新增美术资源不要走分支**：资源不在 Git 里，分支对它无效。由策划/枫直接更新 main 并通知全员同步 U 盘。
- 某分支要用到新资源，必须**先让资源进入 main 并通知全员同步 U 盘**，再在自己的分支里引用。
- 合并任何"含场景/Prefab 改动"的分支前，必须由枫确认。

### 5.6 三条硬规则

1. 只改自己负责的文件；不确定就先问枫。
2. **绝不使用 `git push --force`**，绝不删除别人的文件。
3. 出现冲突或看不懂的报错：**不要乱点、不要强行解决**，立刻截图发群，由枫处理。

### 5.7 一次性配置（每人只做一次）

- 运行 `tools\setup-git-merge-driver.ps1`（右键 → 使用 PowerShell 运行），让 Git 能按对象合并场景/Prefab 冲突。
- 执行 `git config --global core.autocrlf false`。
- 不需要安装 Git LFS。

### 5.8 分支纪律

- 一个任务一个分支，命名 `feat/名字-功能`。
- 比赛节奏下分支**当天合并**，不要挂过夜。

---

## 附录 A：命令速查（可贴到群里）

| 我要做什么 | 命令 |
| --- | --- |
| 更新别人的改动 | `git pull` |
| 看我改了哪些文件 | `git status` |
| 提交我的改动 | `git add .` 然后 `git commit -m "说明"` |
| 上传到 GitHub | `git push`（分支首次用 `git push -u origin 分支名`） |
| 切回主干 | `git checkout main` |
| 建分支 | `git checkout -b feat/名字-功能` |
| 场景冲突取自己的版本 | `git checkout --ours <场景文件>` |
| 出现冲突或报错 | 不要操作，截图发群 |

## 附录 B：术语一句话

- **提交（commit）**：把改动记录到本地。
- **推送（push）**：把本地改动上传到 GitHub。
- **拉取（pull）**：把别人上传的改动下载到本地。
- **分支（branch）**：从主干复制出来的一条独立开发线，互不影响。
- **PR（Pull Request）**：请求把分支的改动合并进主干；本项目用 Squash and merge（多个提交压成一条记录）。
- **冲突（conflict）**：两个人改了同一处，需要人决定保留谁。

## 附录 C：GitHub 仓库设置（由枫维护）

- 邀请协作者权限用 **Write**，不要给 Admin。
- Settings → General → Pull Requests：只保留 `Allow squash merging`。
- Settings → General → 勾选 `Automatically delete head branches`。
- **不要**开 "Require a pull request" 分支保护 —— 它会同时堵死"场景/Prefab 直接推 main"这条路。
- 本仓库不使用 Git LFS。
