# 本项目 Git 使用规范与工作流程

> 场景：**24 小时黑客松**。目标只有三个字：**别崩、能提交、能打包**。
> 前提：你已经了解 Git 的基本概念（不熟先看 `Git-Basics.md`）。
> 本文只讲**规范与流程**，不涉及具体人员和分工（赛前确定后再补）。

---

## 1. 仓库里有什么（哪些走 Git、哪些不走）

| 内容 | 在哪 |
| --- | --- |
| 代码、场景、Prefab、材质、工程设置 | **Git 仓库** |
| 图片、音频、字体、视频 | **U 盘**（不进 Git） |
| `Library/`、`Temp/`、`Logs/`、`UserSettings/` | 引擎自动生成，已被忽略，不用管 |

原因：大文件进 Git 会让仓库爆炸、clone 变慢；而它们本来也不适合做文本合并。

---

## 2. 三条铁律（先记这三条）

1. **一人一文件**：同一个 `.scene` / `.prefab` / `.mat` / Tilemap，任何时间只能由一个人修改。
2. **不碰不属于自己的东西**：`ProjectSettings/`、`Packages/` 只由**仓库管理员**统一改。
3. **冲突或报错就停手**：不要乱点、不要强行解决，截图发群里。

---

## 3. 文件类型与归属规则

> "谁负责"一栏是**角色**，不是具体的人；比赛前按当天人数和模块分配，但必须满足"一人一文件"。

| 文件类型 | 管理方式 | 谁负责 | 关键注意 |
| --- | --- | --- | --- |
| 代码 `.cs` | Git，**走分支** | 代码同学 | 动手前先 `git pull` |
| Prefab `.prefab` | Git，**不走分支** | 每个 Prefab 指定唯一负责人 | 变体用 `Prefab Variant`，不改基础 Prefab |
| 场景 `.scene` | Git，**不走分支** | 主场景＝仓库管理员；测试场景＝各人自己的 | 扩展名是 `.scene` |
| `ProjectSettings/` | Git | **仅仓库管理员** | 别人要加 Tag/Layer 口头告知 |
| `Packages/` | Git | **仅仓库管理员** | 赛前把基础包装齐；比赛中尽量不装新包 |
| 美术 / 音频 / 字体 / 视频 | **U 盘拷贝** | 一人管理文件、一人导入引擎 | 见第 7 节三条硬约束 |
| 烘焙 / Lightmap / NavMesh / ReflectionProbe | `.gitignore` 排除 | 谁都不用 | 比赛不做光照烘焙与寻路 |
| ScriptableObject `.asset` | Git，**不走分支** | 每个文件唯一负责人 | 数值优先用表格（CSV/Excel）维护 |
| 动画 `.anim` / `.controller` | Git，不走分支 | 每个文件唯一负责人 | 2D 优先序列帧/代码，少用 Animator |
| 材质 `.mat` | Git，不走分支 | 每个文件唯一负责人 | 2D 尽量用默认材质 |
| Tilemap / Palette `.asset` | Git | **仅仓库管理员** | 其他人只读 |
| 输入映射 `.inputactions` | Git | **仅仓库管理员** | 其他人不许改 |
| 第三方 `.dll` | Git | 仅仓库管理员导入 | 二进制、不可 diff，数量保持最少 |
| 工程元文件（`.gitignore`/`.gitattributes`/`Docs/`/`tools/`） | Git | **仅仓库管理员** | 其他人不要动 |

### `.meta` 文件怎么办（最容易搞错的一条）

规则是**跟着"资源本体"走**：

- **走 Git 的文件**（`.cs`/`.prefab`/`.scene`/`.asset`/`.shader` 等）→ 它们的 `.meta` **必须进 Git**，严禁删除或忽略；
- **`Assets/Art/` 下的资源**（走 U 盘的那批）→ 它的 `.meta` **随 U 盘一起拷，不进 Git**。

两种情况的共同点：**`.meta` 绝不能和资源分家**。meta 丢了或对不上，引擎里所有引用都会变粉（Missing）。

---

## 4. 一次性环境配置（每人做一次）

1. 安装**团结引擎 1.8.4**（内部版本 `2022.3.62t6`，版本不一致打不开工程）。
2. 关闭自动换行转换，避免跨系统出现"整文件都改了"的假差异：
   ```
   git config --global core.autocrlf false
   ```
3. 注册场景/Prefab 的智能合并工具（**必须做**）：
   > 右键 `tools\setup-git-merge-driver.ps1` → 「使用 PowerShell 运行」

   这个脚本会把团结引擎自带的 SmartMerge 配置到你的 Git 里，让场景/Prefab 冲突时按"对象"合并而不是按文本行硬合并。**不运行的话，一旦真冲突会直接报 `fatal: unknown merge driver`，连合并都做不了。**
4. **不需要**安装 Git LFS。

---

## 5. 日常流程 A：改代码 → 走分支 + PR

```
git checkout main
git pull                                    # 先拿别人的最新改动
git checkout -b feat/模块-功能               # 建自己的分支，例：feat/ui-hpbar
# ……改代码……
git status                                  # 确认没有把 Library、图片带进来
git add .
git commit -m "feat: 血条 UI"
git push -u origin feat/模块-功能
```

然后到 GitHub 网页：`Compare & pull request` → 写一句说明 → `Create pull request` → **`Squash and merge`** → `Delete branch`。

推送前如果落后于主干，先跟上：

```
git checkout main
git pull
git checkout feat/模块-功能
git merge main        # 有冲突就停下来找人
git push
```

---

## 6. 日常流程 B：改场景 / Prefab / 工程设置 → 直接在 main 上单线改

> **为什么不走分支？** 场景/Prefab 是文本 YAML，两个人分别改**不同**物体也会被判成冲突，几乎无法人工合并。走分支只会把"当场的小冲突"推迟并放大成"合并时无解的大冲突"。

```
# 先确认没人在改同一个文件（群里说一句）
git checkout main
git pull
# 打开引擎修改，保存后关闭引擎
git add Assets/Scenes/Test_xxx.scene Assets/Scenes/Test_xxx.scene.meta
git commit -m "chore: 更新测试场景"
git push
```

改了 `ProjectSettings/`、`Packages/`、`.inputactions`、Tilemap 的同学，同样直接推 main。

---

## 7. U 盘资源同步（三条硬约束）

1. **必须连 `.meta` 一起拷贝** —— 这是 GUID 一致性的唯一保障；漏拷 meta 会让所有人的 Prefab 引用断链。
2. **拷贝前必须关闭引擎**，拷贝完成后再打开。
3. **顺序：先拷资源，再打开工程**。反过来打开时就是一片 missing。

补充：

- 新增美术资源**不要走分支**（资源不在 Git 里，分支对它没意义）：由管理资源的人直接更新并通知全员同步 U 盘。
- 某个分支要用到新资源，必须**先让资源进入 main 并通知全员同步**，再在自己的分支里引用。
- 资源目录骨架：`Assets/Art/Images|Audio|Models|Videos|Fonts/`（clone 后是空的，靠 U 盘填充）。

---

## 8. 冲突怎么处理

### 8.1 普通文本文档（`.cs` 等）冲突

1. 打开冲突文件，找到 `<<<<<<<` / `=======` / `>>>>>>>` 标记；
2. 手工改成想要的结果，删掉标记；
3. `git add <文件>` → `git commit`。
4. 拿不准就**停下来找人**，不要瞎猜。

### 8.2 场景 / Prefab 冲突（保底策略）

**不要试图手工合并场景文件**（大概率打开就崩）。做法：

```
git checkout --ours   Assets/Scenes/Test_xxx.scene    # 保留自己这边的版本
# 或
git checkout --theirs Assets/Scenes/Test_xxx.scene    # 保留远程/对方那边的版本

git add Assets/Scenes/Test_xxx.scene
git commit -m "fix: 采用我这一版的测试场景"
```

然后**被覆盖的一方**把改动（Prefab 摆放等）重新拖回场景，通常只损失几分钟；并在群里说明"我采用了哪一版"。

> 结论：宁可重摆 5 分钟，也不要花 1 小时去手改 YAML。

---

## 9. 场景与 Prefab 的协作约定（与 Git 直接相关）

- **一人一文件**：同一场景/Prefab 任何时间只有一个人改，动手前群里说一声。
- **主场景保持极简**：只放一个"场景管理器"，其余物体运行时按路径动态创建；各人在 `Test_<你的名字>.scene` 里干活。这样所有人的物理改动彼此隔离，几乎不会冲突。
- **Prefab 变体**：需要不同版本时，右键基础 Prefab → `Create` → `Prefab Variant`，在变体上改，不要直接改基础 Prefab。
- **动态创建的 Prefab 统一放 `Assets/Resources/Prefabs/`**，代码按字符串路径加载（文件名即路径），命名前缀统一：`Enemy_` / `Bullet_` / `FX_` / `UI_`。
  - 好处：别人负责的 Prefab 还没做好时，你的代码也不会崩。
  - 代价：`Resources` 里的资源会**全部打进包体**，比赛规模够用。
- **要测试别人负责的 Prefab**：复制一份到自己场景改 → 验证通过后把"改了什么"告诉负责人 → 由**负责人**在正式 Prefab 上统一修改提交，**不要直接改正式 Prefab**。
- 切分支 / `git pull` 前**先关闭引擎**；切换后若报奇怪错误：关引擎 → 删 `Library` 文件夹 → 重新打开重建。

---

## 10. 常见问题速查

| 现象 | 原因与处理 |
| --- | --- |
| `git push` 被拒绝（rejected） | 你在 main 上直接推代码 / 落后于远程；先 `git pull`，代码改动改用分支 |
| 场景/图片显示 missing、粉色 | U 盘资源没拷到位，或没连 `.meta` 一起拷；关引擎重拷后再打开 |
| 合并报 `fatal: unknown merge driver` | 没运行 `tools\setup-git-merge-driver.ps1` |
| 改了东西但 `git status` 看不到 | 它被 `.gitignore` 忽略了（如 `Assets/Art/` 下的图），本来就不走 Git |
| 出现 `CONFLICT` | 停手，按第 8 节处理，或截图发群 |
| 切分支后引擎报奇怪错误 | 关引擎 → 删 `Library` → 重新打开 |
| 想把某文件移出 Git 跟踪 | `git rm --cached <文件>`（文件本身保留在磁盘上） |

---

## 附录：命令速查（可贴到群里）

| 我要做什么 | 命令 |
| --- | --- |
| 更新别人的改动 | `git pull` |
| 看当前状态/改了哪些文件 | `git status` |
| 暂存并提交 | `git add .` → `git commit -m "说明"` |
| 上传到 GitHub | `git push`（新分支首次用 `git push -u origin 分支名`） |
| 建分支并切过去 | `git checkout -b feat/模块-功能` |
| 切回主干 | `git checkout main` |
| 看提交历史 | `git log --oneline` |
| 场景冲突取自己这一版 | `git checkout --ours <文件>` |
| 把文件移出跟踪但保留在磁盘 | `git rm --cached <文件>` |
