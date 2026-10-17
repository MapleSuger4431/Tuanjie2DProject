#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
validate_all.py — 跨平台一键全量验证（2b 步骤1 固化脚本）

自动扫描 GameScripts 目录下【全部】.cs（不必先 glob 再手拼列表，避免"凭印象漏一半"），
按平台选择 RoslynValidator 二进制（win: RoslynValidator.exe / mac,linux: RoslynValidator），
调用做 Full 级验证（--rules -w）。跨平台：Windows / macOS / Linux。

用法：
    {PYTHON} validate_all.py <GameScripts绝对目录> [--game NAME]

    <GameScripts绝对目录>  必填，如 E:/.../context/games/FlappyBird/GameScripts
    --game NAME            可选，仅用于 RoslynValidator 结果文件命名，默认取目录父级名

退出码：0=验证通过(无 error)；1=验证失败(有 error)或运行时错误；2=参数/环境错误。
"""
import os
import sys
import argparse
import hashlib
import platform
import subprocess
import re

# 强制 UTF-8 输出，避免非 UTF-8 控制台（如 Windows GBK）把中文/符号转义成乱码
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except Exception:
        pass

# 本脚本位于 .../tools/RoslynValidator/ 下，bin 就在同目录
TOOL_DIR = os.path.dirname(os.path.abspath(__file__))
BIN_DIR = os.path.join(TOOL_DIR, "bin")


def normalize_machine(machine):
    """Normalize common CPU architecture names to the runtime ID spelling."""
    value = (machine or "").lower()
    if value in ("arm64", "aarch64"):
        return "arm64"
    if value in ("x86_64", "amd64"):
        return "x86_64"
    return value


def native_machine():
    """Return the hardware architecture, including from a Rosetta-translated shell."""
    machine = normalize_machine(platform.machine())
    if platform.system() == "Darwin" and machine == "x86_64":
        try:
            proc = subprocess.run(
                ["sysctl", "-n", "hw.optional.arm64"],
                capture_output=True, text=True, encoding="utf-8", check=False)
            if proc.returncode == 0 and proc.stdout.strip() == "1":
                return "arm64"
        except OSError:
            pass
    return machine


def native_runtime_id():
    """Return the self-contained .NET runtime ID suitable for this host."""
    system = platform.system()
    prefix = "osx" if system == "Darwin" else "linux" if system == "Linux" else None
    machine = native_machine()
    arch = "arm64" if machine == "arm64" else "x64" if machine == "x86_64" else None
    return f"{prefix}-{arch}" if prefix and arch else None


def macho_architecture(path):
    """Read the architecture from a thin 64-bit Mach-O header."""
    try:
        with open(path, "rb") as stream:
            header = stream.read(8)
    except OSError:
        return None
    if len(header) < 8:
        return None
    if header[:4] == b"\xcf\xfa\xed\xfe":
        byteorder = "little"
    elif header[:4] == b"\xfe\xed\xfa\xcf":
        byteorder = "big"
    else:
        return None
    cpu_type = int.from_bytes(header[4:8], byteorder=byteorder)
    return {0x01000007: "x86_64", 0x0100000C: "arm64"}.get(cpu_type)


def check_binary_architecture(exe):
    """Reject incompatible macOS binaries and explain Rosetta-only execution."""
    if platform.system() != "Darwin":
        return True
    host_arch = native_machine()
    binary_arch = macho_architecture(exe)
    if not binary_arch or not host_arch or binary_arch == host_arch:
        return True
    build_rid = native_runtime_id() or "the matching macOS RID"
    build_command = f"cd {TOOL_DIR} && ./build.sh {build_rid}"
    if host_arch == "arm64" and binary_arch == "x86_64":
        print(
            "[WARN] RoslynValidator is x86_64 on Apple Silicon and requires Rosetta 2.\n"
            f"       Build the native binary to remove that dependency: {build_command}",
            file=sys.stderr)
        return True
    print(
        f"[ERROR] RoslynValidator architecture {binary_arch} is incompatible with host {host_arch}.\n"
        f"        Rebuild it for this host: {build_command}",
        file=sys.stderr)
    return False


def find_binary(bin_dir=BIN_DIR, system_name=None):
    """Return the only coherent RoslynValidator binary under bin_dir.

    Release archives must not silently choose between stale build outputs. Identical
    copies are tolerated for backwards compatibility, but divergent copies fail closed.
    """
    binary_name = "RoslynValidator.exe" if (system_name or platform.system()) == "Windows" else "RoslynValidator"
    candidates = sorted(
        os.path.join(root, binary_name)
        for root, _, names in os.walk(bin_dir)
        if binary_name in names
    )
    if not candidates:
        return None

    hashes = {}
    for candidate in candidates:
        with open(candidate, "rb") as stream:
            digest = hashlib.sha256(stream.read()).hexdigest()
        hashes.setdefault(digest, []).append(candidate)
    if len(hashes) != 1:
        details = "; ".join(
            f"{digest[:12]}={','.join(paths)}" for digest, paths in sorted(hashes.items()))
        raise RuntimeError(
            "ROSLYN_BINARY_AMBIGUOUS: divergent RoslynValidator binaries found; "
            "rebuild tools/RoslynValidator so bin contains one current executable. " + details)

    canonical = os.path.join(bin_dir, binary_name)
    return canonical if canonical in candidates else candidates[0]


def print_summary(scanned, namespace_errors, validator_exit, status):
    """Emit the stable machine-readable terminal record for every main() exit."""
    print(
        f"ROSLYN_VALIDATE_SUMMARY scanned={scanned} "
        f"namespace_errors={namespace_errors} validator_exit={validator_exit} status={status}")


class SummaryArgumentParser(argparse.ArgumentParser):
    """Keep argparse help/usage failures inside the same summary contract."""
    def exit(self, status=0, message=None):
        if message:
            self._print_message(message, sys.stderr if status else sys.stdout)
        print_summary(0, 0, "not_run", "PASS" if status == 0 else "ERROR")
        raise SystemExit(status)


def main():
    ap = SummaryArgumentParser(description="跨平台全量运行 RoslynValidator")
    ap.add_argument("base_dir", help="GameScripts 绝对目录")
    ap.add_argument("--game", default=None, help="结果文件命名用；默认取目录父级名")
    ap.add_argument("--expected-namespace", default=None, help="所有 GameScripts 必须使用的唯一根命名空间")
    ap.add_argument("--input-contract", default=None,
                    help="job-plan.compiled.json 或仅含 input_contract 的 JSON；配合 --recursive 装载输入入口清单")
    args = ap.parse_args()

    base_dir = os.path.abspath(args.base_dir)
    if not os.path.isdir(base_dir):
        print(f"[ERROR] base_dir not found: {base_dir}", file=sys.stderr)
        print_summary(0, 0, "not_run", "ERROR")
        return 2

    # 1. 递归扫描全集；identity/显示均使用相对 GameScripts 路径。
    cs_paths = sorted(
        (os.path.join(root, name)
         for root, _, names in os.walk(base_dir)
         for name in names if name.endswith(".cs")),
        key=lambda path: os.path.relpath(path, base_dir).replace("\\", "/"))
    if not cs_paths:
        print(f"[ERROR] no .cs under: {base_dir}", file=sys.stderr)
        print_summary(0, 0, "not_run", "ERROR")
        return 2
    relative_paths = [os.path.relpath(p, base_dir).replace("\\", "/") for p in cs_paths]
    game = args.game or os.path.basename(os.path.dirname(base_dir)) or "Game"

    if args.expected_namespace:
        ns_re = re.compile(r"^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*(?:[;{])", re.MULTILINE)
        namespace_errors = []
        for path, relative_path in zip(cs_paths, relative_paths):
            with open(path, "r", encoding="utf-8-sig") as f:
                declared = sorted(set(ns_re.findall(f.read())))
            if declared != [args.expected_namespace]:
                shown = ", ".join(declared) if declared else "<none>"
                namespace_errors.append(
                    f"{relative_path}: expected namespace '{args.expected_namespace}', found {shown}")
        if namespace_errors:
            print("[ERROR] namespace contract violations:", file=sys.stderr)
            for item in namespace_errors:
                print(f"  - {item}", file=sys.stderr)
            print_summary(len(cs_paths), len(namespace_errors), "not_run", "FAIL")
            return 1

    print(f"[INFO] Scanned {len(cs_paths)} .cs, running full validation (game={game})")

    # 2. 定位二进制
    try:
        exe = find_binary()
    except (OSError, RuntimeError) as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        print_summary(len(cs_paths), 0, "not_run", "ERROR")
        return 2
    if exe is None:
        sysname = platform.system()
        msg = [f"[ERROR] RoslynValidator binary not found (platform {sysname}, looked in {BIN_DIR})"]
        if sysname != "Windows":
            build_rid = native_runtime_id()
            msg.append("  macOS/Linux first-time build required:")
            msg.append(f"    cd {TOOL_DIR} && ./build.sh {build_rid or '<runtime-id>'}")
        print("\n".join(msg), file=sys.stderr)
        print_summary(len(cs_paths), 0, "not_run", "ERROR")
        return 2
    if not check_binary_architecture(exe):
        print_summary(len(cs_paths), 0, "not_run", "ERROR")
        return 2

    # 3. 调用（--rules 启用 Unity 反模式分析器，-w 显示 warnings）
    cmd = [exe, "-g", game, "-b", base_dir.replace("\\", "/"), "--recursive", "--rules", "-w", "--profile", "game-scripts"]
    if args.input_contract:
        cmd.extend(["--input-contract", os.path.abspath(args.input_contract)])
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8")
    except OSError as e:
        print(f"[ERROR] failed to invoke RoslynValidator: {e}", file=sys.stderr)
        build_rid = native_runtime_id()
        if build_rid:
            print(f"        Rebuild for this host: cd {TOOL_DIR} && ./build.sh {build_rid}", file=sys.stderr)
        print_summary(len(cs_paths), 0, "invoke_error", "ERROR")
        return 2
    except Exception as e:
        print(f"[ERROR] RoslynValidator invocation failed: {e}", file=sys.stderr)
        print_summary(len(cs_paths), 0, "invoke_error", "ERROR")
        return 1

    if proc.stdout:
        print(proc.stdout)
    if proc.stderr:
        print(proc.stderr, file=sys.stderr)
    print_summary(len(cs_paths), 0, proc.returncode, "PASS" if proc.returncode == 0 else "FAIL")
    # RoslynValidator: 0=成功, 1=有 error。透传其退出码。
    return proc.returncode


if __name__ == "__main__":
    sys.exit(main())
