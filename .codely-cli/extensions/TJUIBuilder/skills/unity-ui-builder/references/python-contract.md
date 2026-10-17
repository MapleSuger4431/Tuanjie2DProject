# 跨平台 Python 解析

本文档定义 `{PYTHON}` 占位符的解析规则，供 unity-ui-builder 所有模块共用。

> **注记（流水线主干为 C# 编译型工具）**：流水线主干工具位于 `cn.tuanjie.create.ui` 包 `Editor/UIBuildTools`（`execute_csharp_script` 一行调用）。`{PYTHON}` 现仅服务 ui-style-reference 的图像处理脚本与 `setup_env.py`；本文档对其仍然有效。

## `{PYTHON}` 探测规则

Creator 文档中的 `{PYTHON}` 表示经过探测的 Python 3 解释器，不是名为 `python3` 的固定命令。

1. Windows 依次尝试：`python`、`py -3`。
2. macOS/Linux 依次尝试：`python3`、`python`。
3. 每个候选先运行 `--version`，仅接受成功退出且报告 Python 3 的候选；在同一操作期间复用已选解释器，避免依赖装入另一个 Python 环境。
4. 如果候选都不可用，停止并明确要求安装 Python 3；除非扩展确实分发并验证了项目内解释器，否则不得假设 `./python3`、`.\\python3.exe` 或其它私有路径存在。
5. `#!/usr/bin/env python3` 可保留在 `.py` 文件中供 Unix 直接执行；它不改变从文档或进程启动脚本时的解析顺序。

## 命令示例

```text
Windows:      python <script> ...
Windows 备用: py -3 <script> ...
macOS/Linux:  python3 <script> ...
Unix 备用:    python <script> ...
```

## Validator 输出契约（三态门禁）

验证类脚本（validator）除本文件解析规则外，统一遵守三态输出契约（完整规范见各模块 `shared/contracts.md` 的「三态门禁」节）：

1. stdout 打印一份 verdict JSON，必含 `verdict`（`pass`|`review`|`fail`）、`metrics`、`thresholds`、`reasons`、`evidence_files`、`audit_file`；支持 `--json-output`/`--audit` 落盘审计副本。
2. 退出码固定为：`0` = pass、`2` = review、`1` = fail 或脚本错误。禁止 0/1 之外的"hint"语义（旧 validate_shell 的"退出码 1 = 需 AI 确认"已废弃，由 review=2 取代）。
3. 阈值一律经 CLI/JSON 参数传入并在 `thresholds` 中回显，默认值为保守初值，禁止在调用方硬编码。
