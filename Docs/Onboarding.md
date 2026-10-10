# 新人上手指南（Onboarding）

> 面向**第一次加入本项目**、或**第一次用 Git** 的同学。照着做完，你就能独立提交东西了。
> 细则（冲突文件归属、紧急处理等）见同目录 `CollaborationGuide.md`。

---

## 0. 一句话了解这个项目

- 这是**团结引擎（Tuanjie）2D 项目模板**，目前是**空工程**：只有一个示例场景 `Assets/Scenes/SampleScene.scene`。
- **代码走 Git；图片/音频/字体等大资源一律走 U 盘**，不在 Git 里。
- 主分支是 `main`。仓库地址：<https://github.com/MapleSuger4431/Tuanjie2DProject>
- 引擎版本固定为 **1.8.4**（内部版本 `2022.3.62t6`），版本不一致会打不开工程。

---

## 1. 需要装什么

| 要装的东西 | 说明 |
| --- | --- |
| 团结引擎 Hub + 引擎 | 引擎必须是 **1.8.4** |
| Git | 较新版本即可；装完确认 `git --version` 能输出 |
| GitHub 账号 | 没有就先注册，把用户名发给枫，他把你们加进仓库（给推送权限） |
| gh CLI | **可选**，用来在命令行登录 GitHub |

---

## 2. 首次配置（每台机器只做一次）

**① 关闭自动换行转换**（否则会出现"内容没变但整个文件都显示改动"的假差异）：

```
git config --global core.autocrlf false
```

**② 注册场景/Prefab 智能合并工具（必须做）**：

> 右键 `tools\setup-git-merge-driver.ps1` → 「使用 PowerShell 运行」

不做的后果：将来合并场景/Prefab 时会直接报 `fatal: unknown merge driver`。

**③（可选）登录 GitHub**：`gh auth login` → 选 GitHub.com → HTTPS → 浏览器授权。

---

## 3. 拿到项目（⚠️ 顺序很重要）

**先把 U 盘资源拷到位，再打开工程。** 顺序颠倒的话，场景里的图片引用会是一片 missing。

```
# 1) 克隆仓库（本仓库是公开的，不授权也能克隆；推送才需要权限）
git clone https://github.com/MapleSuger4431/Tuanjie2DProject.git

# 2) 关闭 Unity/团结编辑器，从 U 盘把资源拷到对应目录
#    Assets/Art/Images/   Assets/Art/Audio/   Assets/Art/Models/
#    Assets/Art/Videos/   Assets/Art/Fonts/
#    ★ 必须连 .meta 文件一起拷！漏拷 .meta 会让所有人的 Prefab 引用断链。
#    ★ 这几目录被 .gitignore 忽略，不会也**不应该**进 Git。

# 3) 拷贝完成后再打开编辑器
```

---

## 4. 项目结构速查：哪些能动、哪些别碰

| 路径 | 是什么 | 你能动吗 |
| --- | --- | --- |
| `Assets/Scenes/` | 场景文件（`.scene`） | 只有 `Test_你的名字.scene` 属于你自己；主场景由枫负责 |
| `Assets/Art/` | 图片/音频/字体（**不进 Git，靠 U 盘**） | 不要用 Git 提交这里的东西 |
| `Assets/Resources/Prefabs/` | 需要通过代码动态加载的 Prefab | 按规范放这里 |
| `Docs/` | 规范与上手文档 | 只读 |
| `tools/setup-git-merge-driver.ps1` | 一次性配置脚本 | 只运行，不修改 |
| `ProjectSettings/`、`Packages/` | 引擎设置与包依赖 | **只有枫能改**，不要碰 |
| `Library/`、`Logs/`、`UserSettings/` | 引擎自动生成 | 已被忽略，不用管 |

---

## 5. 三条铁律（记住这三条就够）

1. **同一个场景/Prefab 文件，任何时间只能一个人改**（动手前在群里说一句）。
2. **只改自己负责的文件**，不确定就先问枫。
3. **遇到冲突或看不懂的报错：不要乱点、不要强行解决**，截图发群，由枫处理。

| 谁 | 负责范围 |
| --- | --- |
| 枫 | 主场景、ProjectSettings、Packages、输入映射、Tilemap |
| 云 | `Test_Yun.scene` 及相关脚本 |
| 新 | `Test_Xin.scene` 及相关脚本 |
| 刘、吕 | U 盘资源管理与资源命名 |

---

## 6. 日常操作（照着抄）

### 6.1 改代码：走分支 + 自己在网页上合并

```
git checkout main
git pull                                    # 先拉取别人的最新改动
git checkout -b feat/你的名字-功能            # 建自己的分支，例：feat/yun-ui-hp
# ……改代码……
git status                                  # 确认没把 Library、图片等带进来
git add .
git commit -m "feat: 云 - 血条UI"
git push -u origin feat/你的名字-功能
```

然后到 GitHub 网页：`Compare & pull request` → 写一句说明 → `Create pull request` → **`Squash and merge`** → `Delete branch`。

### 6.2 改场景 / Prefab：**不走分支**，直接改 main

> 原因：场景/Prefab 是文本 YAML，两个人分别改不同物体也会被判成冲突，几乎无法人工合并。走分支只会把冲突推迟并放大。

```
# 先确认没人在改同一个文件
git checkout main
git pull
# 打开 Unity/团结修改，保存后**关闭编辑器**
git add Assets/Scenes/Test_Yun.scene Assets/Scenes/Test_Yun.scene.meta
git commit -m "chore: 云 - 更新测试场景"
git push
```

### 6.3 合并前先跟上主干

```
git checkout main
git pull
git checkout feat/你的名字-功能
git merge main          # 有冲突就停下来找人，不要硬解
git push
```

---

## 7. 第一次上手自检（约 10 分钟）

全部通过，说明你的环境完全没问题：

1. `git log --oneline` 能看到提交历史。
2. 运行过 `tools\setup-git-merge-driver.ps1`，且 `git config --global --get merge.unityyamlmerge.driver` 有输出。
3. 用团结引擎 1.8.4 打开工程，Console **没有红色报错**。
4. 新建自己的场景 `Assets/Scenes/Test_你的名字.scene`，保存。
5. 按 **6.2** 的流程提交并推送成功。
6. 在 GitHub 网页上能看到你这条提交。

---

## 8. 常见问题速查

| 现象 | 原因与处理 |
| --- | --- |
| `git push` 被拒绝（rejected） | 你在 main 上直接推代码；改用分支（见 6.1） |
| 场景/图片显示 missing、粉色 | U 盘资源没拷到位，或没连 `.meta` 一起拷；关闭编辑器重拷后再打开 |
| 合并时报 `fatal: unknown merge driver` | 没运行 `tools\setup-git-merge-driver.ps1` |
| 改了东西但 `git status` 看不到 | 它被 `.gitignore` 忽略了（如 `Assets/Art/` 下的图），这类文件本来就不走 Git |
| 出现 `CONFLICT` | 停止操作，截图发群，由枫处理 |
| 切分支后 Unity 报奇怪错误 | 关闭编辑器 → 删除 `Library` 文件夹 → 重新打开让其重建 |

---

## 9. 备忘

- 术语（提交/推送/拉取/分支/PR/冲突）见 `CollaborationGuide.md` 附录 B。
- 命令速查表见 `CollaborationGuide.md` 附录 A。
- 说明：`CollaborationGuide.md` 里写的 `Main.unity`/`Test_Yun.unity` 是沿用旧写法，**团结引擎的场景文件扩展名实际是 `.scene`**（例如 `SampleScene.scene`），以本指南为准。
