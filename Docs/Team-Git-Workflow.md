# 本项目 Git 使用规范与工作流程

> 场景：**24 小时黑客松**。目标只有三个字：**别崩、能提交、能打包**。
> 前提：你已经了解 Git 的基本概念（不熟先看 `Git-Basics.md`）。
> 本文只讲**规范与流程**，不涉及具体人员和分工（赛前确定后再补）。
> **不会敲命令？** 所有日常场景都有现成提示词，见 `Codely-Prompts.md` —— 复制给 Codely 就能执行。

**从哪开始（三步）**：① `git clone` 本仓库 → ② 照下面 **第 4 节** 做一次性配置（装团结引擎 1.10.4、装 Codely 的 TJGenerators 扩展、跑 `tools\` 下两个脚本）→ ③ 日常操作照 `Codely-Prompts.md` 把提示词整段复制给 Codely 执行，**不用自己记命令**。

---

## 1. 仓库里有什么（哪些走 Git、哪些不走）

| 内容 | 在哪 |
| --- | --- |
| 代码、场景、Prefab、材质、工程设置 | **Git 仓库** |
| 图片、音频、字体、视频 | **Git LFS**（随仓库走，真实文件存 LFS 服务器） |
| `Library/`、`Temp/`、`Logs/`、`UserSettings/` | 引擎自动生成，已被忽略，不用管 |
| `.codely-cli/`（Codely 扩展目录） | 本地生成、**不进 Git**，但**必须存在**（见第 4 节） |

原因：美术/音频是二进制大文件，直接进 Git 会让仓库爆炸、clone 变慢，也无法做文本合并——所以交给 Git LFS：仓库里只存指针，真实文件由 LFS 服务器分发，`git clone` / `git pull` 时自动下载。

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
| 美术 / 音频 / 字体 / 视频 | **Git LFS，随仓库提交** | 一人管理文件、一人导入引擎 | 二进制不可合并，同一份资源仍由一人改 |
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
- **`Assets/Art/` 下的美术/音频/字体资源**（走 Git LFS 的那批）→ 资源本体经 LFS 进 Git，它的 `.meta` 是普通文本、**正常进 Git**。

两种情况的共同点：**`.meta` 绝不能和资源分家**。meta 丢了或对不上，引擎里所有引用都会变粉（Missing）。

---

## 4. 第一次加入：环境准备与拉取（每人只做一次）

按顺序做完下面 7 步，你就能正常开发了。

### 4.1 拿到权限

- 有自己的 GitHub 账号，并**接受仓库邀请**。
- 注意：仓库是 PUBLIC，**不邀请也能 clone**，但**推不上去**（提交时会失败）—— 别等到要提交时才发现。

### 4.2 装软件

| 装什么 | 要求 |
| --- | --- |
| 团结引擎 Hub + 引擎 | 必须是 **1.10.4**（内部版本 `2022.3.62t16`，版本不一致打不开工程） |
| Git | 任意较新版本 |
| Git LFS | **必须装**（Windows 版 Git 自带；macOS `brew install git-lfs`），装完执行一次 `git lfs install` |
| **Codely + TJGenerators（团结AI资产生成）扩展** | **必须装**，理由见 4.3 |

### 4.3 验证 Codely 扩展（最关键的一步）

在项目目录下执行，必须返回 `True`：

```
Test-Path ".\.codely-cli\extensions\TJGenerators\Packages\cn.tuanjie.ai.generators\package.json"
```

原因：`Packages/manifest.json` 里有一行 `file:../.codely-cli/...` 指向这个扩展提供的 Unity 包，而 `.codely-cli/` **不进 Git，只由 Codely 生成**。文件不存在 → 包解析失败 → **工程打不开**。

### 4.4 三条一次性 Git 配置

**① 关闭自动换行转换**（避免跨系统出现"整文件都显示改了"的假差异）：

```
git config --global core.autocrlf false
```

> 依据 Unity 官方社区文章[《Mac 和 Windows 使用 Git 合作开发 Unity 项目》](https://developer.unity.cn/projects/69b64ca2edbc2a5fdf540311)：Windows 设 `true`、Mac 设 `input` 也可行；但本项目 `.gitattributes` 已用 `* text=auto eol=lf` 强制仓库统一 LF，比依赖各人 autocrlf 更确定，所以全员统一设 `false` 即可，两端行为一致。

**② 开启 Windows 长路径支持**（Windows 默认有 260 字符 MAX_PATH 限制，Unity 工程嵌套路径容易超限）：

```
git config --global core.longpaths true
```

**③ 注册场景/Prefab 的智能合并工具**：右键 `tools\setup-git-merge-driver.ps1` → 「使用 PowerShell 运行」

这个脚本把团结引擎自带的 SmartMerge 配到你的 Git 里，让场景/Prefab 冲突时按"对象"合并而不是按文本行硬合并。**不运行的话，一旦真冲突会直接报 `fatal: unknown merge driver`，连合并都做不了。**

> 该 `.ps1` 必须保持 **UTF-8 with BOM** 保存：Windows PowerShell 5.1 读无 BOM 的 UTF-8 会按 GBK 解码，中文变乱码并直接语法报错。所以**不要用编辑器随手重存它**，一定要重存就选"UTF-8 with BOM"。

> **必须安装 Git LFS**：美术/音频/字体都走 LFS，不装的话 clone 下来图片音频全是几百字节的指针文件、引擎里全部 missing。Windows 的 Git for Windows 已自带 LFS；macOS 用 `brew install git-lfs`。装完后执行一次 `git lfs install` 即可（每台机器一次）。

> **要打安卓包的同学**：再右键运行一次 `tools\fix-android-ndk.ps1` —— 团结 CLI 装 android 模块时常见两种毛病（NDK 没真装上 / NDK 装深了一层），两者都表现为 `Android NDK not found`；这个脚本会检查并自动修复。

### 4.5 克隆

```
git clone https://github.com/MapleSuger4431/Tuanjie2DProject.git
```

首次打开时 `Library/` 会重新生成（需要等几分钟），这是正常的。

### 4.6 拉取 LFS 资源（打开工程之前确认）

装好 git-lfs 后，`git clone` 会自动把 LFS 资源一起拉下来。确认方式：

```
git lfs pull
```

输出 "Nothing to pull" 或看到下载进度都正常，之后再打开工程。日常 `git pull` 也会自动拉 LFS 文件；如果某次图片/音频在引擎里显示 missing，先跑一次 `git lfs pull`。

### 4.7 打开并自检

- 用团结引擎 **1.10.4** 打开工程，等首次导入完成；
- Console **没有红色报错**；
- `git log --oneline` 能看到提交历史；
- `git config --global --get merge.unityyamlmerge.driver` 有输出。

> 提示（TextMeshPro 中文）：TMP 默认显示中文会变方块。可以手动 `Window → TextMeshPro → Import TMP Essential Resources` 后再生成中文字体资源，也可以直接让 Codely 用 **TMPChineseFont** 技能帮你配好。

### 4.8 文本文件的编码约定（不注意会出乱码）

| 文件 | 必须的编码 | 说明 |
| --- | --- | --- |
| `.ps1` | **UTF-8 with BOM** | Windows PowerShell 5.1 读无 BOM 的 UTF-8 会按 GBK 解码 → 中文变乱码、脚本直接语法报错 |
| `.bat` / `.cmd` | **纯英文内容**（或 ANSI/GBK） | cmd 按系统代码页解析；**不要给 .bat 加 BOM**，否则 cmd 会把 BOM 当命令、第一行就报错 |
| `.md` / `.cs` / `.scene` / `.prefab` / `.json` | UTF-8（有无 BOM 都行） | GitHub、VS Code、引擎都能正常识别 |

两条要点：

1. **不要用记事本/编辑器随手"另存" `tools\setup-git-merge-driver.ps1`**。确实要重存时，编码必须选 **UTF-8 with BOM**，否则队友运行时会报 `The string is missing the terminator` 之类的语法错误。
2. 如果某台机器上 `git log` 里的中文提交信息显示成乱码，先在终端执行 `chcp 65001`（切到 UTF-8 代码页）再看。

**跨平台文件名约定**（依据 Unity 官方社区文章）：

- 文件名避免使用 `#`、`:`、`*`、`?`、`<`、`>`、`|` 等特殊字符（Windows 不允许，Mac 允许，混用会导致 Windows 队友 checkout 失败）。
- 不使用符号链接（Mac 可创建，Windows 需要管理员权限，Git 两端行为不一致）。
- 改文件名大小写必须走 `git mv`，否则一端可能看不到变更（两系统默认都不区分大小写）。

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

> ⚠️ **暂停使用（2026-10-17 起）**：美术资源已改走 **Git LFS** 随仓库分发，U 盘方案暂时停用。本节内容与 `tools\sync-art.ps1` 保留作为备用参考，需要恢复时直接用。

1. **必须连 `.meta` 一起拷贝** —— 这是 GUID 一致性的唯一保障；漏拷 meta 会让所有人的 Prefab 引用断链。
2. **拷贝前必须关闭引擎**，拷贝完成后再打开。
3. **顺序：先拷资源，再打开工程**。反过来打开时就是一片 missing。

**推荐用脚本做**（上面三条它会自动检查）：

```
# 推送（工程 -> U 盘）
.\tools\sync-art.ps1 -Direction Push -UsbRoot "F:\2DProject_资产"

# 拉取（U 盘 -> 工程）
.\tools\sync-art.ps1 -Direction Pull -UsbRoot "F:\2DProject_资产"
```

也可以直接**右键 `tools\sync-art.ps1` → 使用 PowerShell 运行**，按提示输入方向。它会：① 发现引擎还开着 → 直接拦住；② 发现某个资源缺 `.meta` → 报错拦住并指出是哪个文件；③ 同步完列出文件清单。`UsbRoot` 换成你自己的 U 盘路径即可。

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
| 场景/图片显示 missing、粉色 | LFS 资源没拉下来：先 `git lfs pull`，仍不行就关引擎重开 |
| 合并报 `fatal: unknown merge driver` | 没运行 `tools\setup-git-merge-driver.ps1` |
| 打包报 `Android NDK not found`，且 NDK 目录是空的 | 模块没真正装上 → `tuanjie.exe install-modules <引擎版本> --module android-ndk` |
| 打包报 `Android NDK not found`，但 NDK 目录里有内容 | NDK 被装深了一层（`NDK\android-ndk-rXX\`）→ 右键运行 `tools\fix-android-ndk.ps1` 自动修复 |
| 打包报 `Package Name has not been set up correctly` | 包名不合法（每段不能以数字开头）→ 在 Player Settings 里设成 `com.xxx.yyy` 形式 |
| 本地仓库还是旧内容（引擎版本写 1.8.4、没有 `Docs/Git-Basics.md`） | 仓库在 2026-10-15 重置过历史，旧 clone 的 `git pull` 拉不动 → **删掉本地目录重新 `git clone`**（不是 pull） |
| 拿到的工程打不开，或少了 `ProjectSettings`、`Packages`、`tools` | 那是从 U 盘拷的**残缺快照** → **工程一律用 `git clone` 获取**（美术资源也随仓库经 LFS 分发） |
| 在 U 盘目录里跑 `git` 报 `dubious ownership ... does not record ownership` | U 盘文件系统不记录文件所有权 → **不要直接操作 U 盘上的 git 仓库**，先整个拷到本地磁盘再跑命令 |
| 打开工程报 `cn.tuanjie.ai.generators ... cannot be found` | 没装 Codely 的 TJGenerators 扩展（见 4.3） |
| 打开工程报某个 Unity 包 `cannot be found` | 该版本团结引擎不支持；改用编辑器 `Window → Package Manager` 安装（UI 只列可用版本） |
| 改了东西但 `git status` 看不到 | 它被 `.gitignore` 忽略了（如 `.fbx`/`.mp4` 等 2D 项目明确不用的格式），本来就不走 Git |
| 出现 `CONFLICT` | 停手，按第 8 节处理，或截图发群 |
| 切分支后引擎报奇怪错误 | 关引擎 → 删 `Library` → 重新打开 |
| 想把某文件移出 Git 跟踪 | `git rm --cached <文件>`（文件本身保留在磁盘上） |

---

## 附录：命令速查（可贴到群里）

| 我要做什么 | 命令 |
| --- | --- |
| 更新别人的改动 | `git pull` |
| 拉取 LFS 大文件（图片/音频） | `git lfs pull` |
| 看当前状态/改了哪些文件 | `git status` |
| 暂存并提交 | `git add .` → `git commit -m "说明"` |
| 上传到 GitHub | `git push`（新分支首次用 `git push -u origin 分支名`） |
| 建分支并切过去 | `git checkout -b feat/模块-功能` |
| 切回主干 | `git checkout main` |
| 看提交历史 | `git log --oneline` |
| 场景冲突取自己这一版 | `git checkout --ours <文件>` |
| 把文件移出跟踪但保留在磁盘 | `git rm --cached <文件>` |
