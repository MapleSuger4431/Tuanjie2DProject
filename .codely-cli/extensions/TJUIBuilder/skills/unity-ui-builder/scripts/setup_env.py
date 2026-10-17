#!/usr/bin/env python3
"""Unity UI Builder 环境自检与自动安装。

检查 Pillow、opencv-python（含 numpy）是否可用，缺失则自动 pip 安装。
激活 unity-ui-builder 技能时通过 run_shell_command 调用本脚本。

用法:
    {PYTHON} scripts/setup_env.py

{PYTHON} 按 Creator 跨平台合同解析：Windows 为 python → py -3，macOS/Linux 为
python3 → python；候选必须通过 --version 并确认为 Python 3。

退出码: 0=全部就绪, 1=有依赖安装失败或不可用
输出行格式: STATUS|detail（机器可读）
"""
from __future__ import annotations

import importlib
import subprocess
import sys

# module_name → pip package name
REQUIRED = {
    "PIL": "Pillow",
    "cv2": "opencv-python",
    "numpy": "numpy",
}


def main() -> int:
    missing_pkgs: list[str] = []

    for mod, pkg in REQUIRED.items():
        try:
            importlib.import_module(mod)
            print(f"OK|{mod} ({pkg})")
        except ImportError:
            missing_pkgs.append(pkg)
            print(f"MISSING|{mod} ({pkg})")

    if missing_pkgs:
        print(f"INSTALLING|{', '.join(missing_pkgs)}")
        try:
            subprocess.check_call(
                [sys.executable, "-m", "pip", "install", "--user", *missing_pkgs],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.PIPE,
            )
            print(f"INSTALLED|{', '.join(missing_pkgs)}")
        except subprocess.CalledProcessError as e:
            print(f"INSTALL_FAILED|exit={e.returncode}")
            return 1
    else:
        print("ALL_OK|all dependencies already satisfied")

    # Verify after install
    failed: list[str] = []
    for mod, pkg in REQUIRED.items():
        try:
            importlib.import_module(mod)
        except ImportError:
            failed.append(f"{mod} ({pkg})")
            print(f"VERIFY_FAILED|{mod} ({pkg})")

    if failed:
        print(f"ENV_INCOMPLETE|{', '.join(failed)}")
        return 1

    print("ENV_READY|all checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
