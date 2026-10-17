# Codely 提示词手册（队友版）

> **用法**：在**项目根目录**打开 Codely，把对应场景的提示词**整段复制**给它；`<尖括号>` 里的内容换成你自己的。
> 这些提示词都基于 `Team-Git-Workflow.md` 的规则 —— 如果 Codely 给出和文档冲突的做法，**以文档为准**。
> **练习/试验代码不要提交进这个仓库**（要练 Git 就在别的目录单独练）。

---

## 0. 第一次加入（只做一次）

### 0.1 克隆仓库（在任意目录的 Codely 里执行）

```
帮我把这个仓库克隆到 <D:\develop\Unity\MyProject\Tuanjie2DProject>：
https://github.com/MapleSuger4431/Tuanjie2DProject.git
克隆完检查目录里有 Assets、ProjectSettings、Packages 三个文件夹，然后告诉我下一步做什么。
```

### 0.2 环境配置（在克隆下来的项目目录里执行）

```
我是新加入的成员。请按 Docs/Team-Git-Workflow.md 第 4 节帮我完成首次配置，逐步汇报：
1. 检查团结引擎是否装了 1.10.4（用 tuanjie.exe editors list-installed）
2. 检查 .codely-cli\extensions\TJGenerators\Packages\cn.tuanjie.ai.generators\package.json 是否存在；不存在就停下，提示我需要先在 Codely 里安装 TJGenerators（团结AI资产生成）扩展
3. 执行 git config --global core.autocrlf false
4. 运行 tools\setup-git-merge-driver.ps1，然后验证 git config --global --get merge.unityyamlmerge.driver 有输出
5. 运行 tools\fix-android-ndk.ps1，确认它报告 NDK 位置正确（要打安卓包必须；不打也顺手跑一次没坏处）
6. 最后执行 git lfs pull 确认美术资源已拉全，再提醒我打开工程
任何一步失败都停下来告诉我，不要跳过。
```

---

## 1. 每天开工

```
开工。帮我：
1. git pull 拉取最新代码
2. git status 看看我本地有没有没提交的改动；有的话列出来问我要怎么处理
3. git log --oneline -5 给我看最近的提交，并用一句话说明别人最近改了什么
```

---

## 2. 开发一个新功能（代码类，走分支 + PR）

### 2.1 开工建分支

```
我要开发 <血条UI>。请按 Docs/Team-Git-Workflow.md 第 5 节：
1. 切到 main 并 git pull 拿最新
2. 从最新 main 建分支 feat/<模块>-<功能> 并切过去
3. 建完告诉我分支名；之后我改代码，改完叫你提交
注意：我只改自己负责的文件；如果我改到 ProjectSettings/Packages 或别人的场景，先拦下我。
```

### 2.2 改完，提交并开 PR

```
<血条UI> 改完了。请：
1. git status 列出所有改动；如果有 Library/、Assets/Art/ 下的文件或跟本次功能无关的文件混进来，先停下来问我
2. 提交，提交信息：feat: <模块> - <功能简述>
3. git push -u origin <分支名>
4. 用 gh pr create 创建 PR（标题同提交信息），把 PR 链接发我；gh 没登录就教我怎么登录
```

### 2.3 确认无误后合并 PR（记住用 Squash）

```
PR #<编号> 确认没问题了。请：
1. 用 gh pr merge <编号> --squash --delete-branch 合并，并删掉远程分支
2. 本地切回 main，git pull 拿到合并后的提交
3. 删掉我本地的旧分支 <分支名>
4. 提醒我：下一个任务要从最新 main 重新建分支，不要复用旧分支
```

> 网页操作备选（不想用命令时）：仓库页面 → `Pull requests` → 点开该 PR → 绿色按钮选 **`Squash and merge`**（不要点 `Create a merge commit`）→ `Delete branch`。

---

## 3. 改场景 / Prefab / 工程设置（不走分支，直接 main）

```
我要改 <Test_Yun.scene>（场景/Prefab 类）。请按 Docs/Team-Git-Workflow.md 第 6 节：
1. 先 git pull 更新 main
2. 我现在去引擎里改，改完保存、关闭引擎后再叫你
```

改完后接着说：

```
改完了。请把 <Assets/Scenes/Test_Yun.scene> 和它的 .meta 一起提交并推送到 main，
提交信息：chore: <我的名字> - <改了什么>。
提交前先 git status 确认没有其他意外改动。
```

---

## 4. 打包（出 APK）

> 打包走**命令行**，脚本已在仓库里：`Assets/Editor/BuildScript.cs`。产物落在 `Builds/Android/`，**不进 Git**。

```
帮我打一个安卓包。请：
1. 先确认团结引擎编辑器已关闭（没关就让我先关 —— 命令行打包和编辑器不能同时占一个工程）
2. 用命令行打包（引擎装着 D:\develop\Unity\TuanjieEngine\<版本>\Editor\Tuanjie.exe，版本以 tuanjie.exe editors list-installed 为准）：
   -batchmode -projectPath "<当前工程路径>" -buildTarget Android -executeMethod BuildScript.BuildAndroid -logFile "<工程父目录>\build-android.log"
3. 用后台跑（完整打包要几分钟到几十分钟），完成后读日志给我结论：成功就说 APK 路径和大小；失败先把原因讲清楚，再问我要不要修
4. 如果报 NDK 相关问题，先运行 tools\fix-android-ndk.ps1 再重试一次
```

> ⚠️ 当前出的是 **32 位（armeabi-v7a）+ Mono** 包，**部分新机型装不上**。要让更多手机能装：Player Settings 里把 Scripting Backend 改成 **IL2CPP**、Target Architectures 勾上 **ARM64**（打包会慢很多）。
## 5. 美术资源同步（Git LFS）

```
帮我同步美术资源（本项目走 Git LFS）：
1. git lfs pull 拉取最新的图片/音频/字体（git pull 通常已自动拉取，这步是兜底）
2. git lfs ls-files 列出当前仓库里的 LFS 文件给我看一眼
3. 如果我本地有新增/修改的美术资源还没提交，列出来问我要不要提交并推送
注意：美术资源经 LFS 随 Git 提交，不要再用 U 盘拷贝——U 盘方案 2026-10-17 起暂停（tools\sync-art.ps1 保留备用）。
```

---

## 6. 出问题了

### 6.1 冲突

```
出冲突了：<把报错文字或截图贴在这里>。请按 Docs/Team-Git-Workflow.md 第 8 节处理：
- 普通文本（.cs 等）冲突：列出冲突文件，逐个问我要保留哪边，改完帮我提交
- 场景/Prefab 冲突：不要手工合并 YAML，先问我要保留哪一版（--ours 或 --theirs），我确认后再执行并提交
- 拿不准就停下来，先把原因讲给我听
```

### 6.2 其他报错

```
<打开工程报错：An error occurred while resolving packages… / push 被拒绝 / 场景里的东西变粉 / Console 报错截图>。
帮我定位：先读 <日志路径或报错内容>，讲清楚原因和处理步骤，征得我同意再动手。
涉及删除文件、git push --force 这类操作必须先问我。
```

---

### 6.3 本地仓库版本不对（还是老版本）

```
帮我检查并修复本地仓库版本。请：
1. 执行 git fetch origin，然后比较 git rev-parse HEAD 与 git rev-parse origin/main —— 两者不相同就说明我本地不是最新
2. 检查这两个文件是否存在：Docs/Git-Basics.md、tools/fix-android-ndk.ps1（旧版本没有它们）
3. 检查 ProjectSettings/ProjectVersion.txt 里 m_TuanjieEditorVersion 是不是 1.10.4（旧版本是 1.8.4）
如果三项都正常，告诉我已是最新就结束。
如果是旧版本（仓库在 2026-10-15 重置过历史，git pull 拉不动）：
4. 先执行 git status；如果有未提交的改动就停下来问我怎么处理，不要自己删
5. 确认没有要保留的改动后，删掉本地项目目录，重新 clone：
   https://github.com/MapleSuger4431/Tuanjie2DProject.git
6. 重新 clone 后按第 0.2 节再做一次环境配置
```
## 7. 仓库管理员（枫）专用

### 7.1 加协作者

```
把 <GitHub用户名> 加为协作者，权限用 Write（不要给 Admin）。
完成后列出当前协作者名单和还没接受邀请的人。
```

### 7.2 仓库体检（每周或赛前一次）

```
帮我做一次仓库体检：
1. git ls-files 检查有没有 Library/Temp/Logs/UserSettings、Assets/Art/ 下的文件被误跟踪
2. 检查有没有超过 20MB 的大文件
3. git log --oneline -10 看最近提交
4. 列出远端所有分支（有没有该删没删的）
发现问题先给我建议，不要直接动。
```

### 7.3 仓库设置（一次性）

```
帮我把仓库 MapleSuger4431/Tuanjie2DProject 的合并设置改成：
只允许 Squash 合并（关掉 merge commit 和 rebase），并且合并后自动删除分支。
改完把当前设置回报给我。
```

---

## 8. 三条最容易犯的错

1. **与项目无关的文件不要提交**：练习代码、个人笔记、IDE/编辑器的个人配置等都不进仓库。提交前先用 `git status` 过一眼要提交的内容，拿不准就先问仓库管理员。
2. **别在已合并的分支上继续改**：Squash 合并后，分支上的原始提交并不在 main 上；复用旧分支会让 Git 重复应用改动、凭空冒冲突。**合并后删分支，从最新 main 重新开。**
3. **提交信息要说清改了什么**：建议格式 `类型: 模块 - 简述`（例：`feat: UI - 血条`），以后翻历史的人一眼能看懂。
