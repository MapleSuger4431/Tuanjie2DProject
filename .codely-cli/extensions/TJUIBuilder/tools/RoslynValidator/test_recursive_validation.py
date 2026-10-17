import importlib.util
import os
import pathlib
import subprocess
import tempfile
import unittest
from unittest import mock


MODULE_PATH = pathlib.Path(__file__).with_name("validate_all.py")
PROJECT_ROOT = pathlib.Path(__file__).parents[5]
SPEC = importlib.util.spec_from_file_location("validate_all_under_test", MODULE_PATH)
VALIDATE_ALL = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATE_ALL)


class RecursiveValidationTests(unittest.TestCase):
    def test_source_uses_recursive_cli_and_stable_summary(self):
        source = MODULE_PATH.read_text(encoding="utf-8")
        self.assertIn("os.walk(base_dir)", source)
        self.assertIn('"--recursive"', source)
        self.assertIn("ROSLYN_VALIDATE_SUMMARY", source)
        self.assertGreaterEqual(source.count("print_summary("), 7)

    def test_nested_same_names_are_distinct(self):
        with tempfile.TemporaryDirectory() as root:
            for folder in ("Core", "UI"):
                path = pathlib.Path(root, folder, "Shared.cs")
                path.parent.mkdir()
                path.write_text(f"namespace Game {{ class {folder}Shared {{}} }}", encoding="utf-8")
            rel = sorted(
                os.path.relpath(os.path.join(current, name), root).replace("\\", "/")
                for current, _, names in os.walk(root)
                for name in names if name.endswith(".cs"))
            self.assertEqual(["Core/Shared.cs", "UI/Shared.cs"], rel)

    def test_windows_and_macos_separators_normalize(self):
        self.assertEqual("UI/HUD.cs", "UI\\HUD.cs".replace("\\", "/"))
        self.assertEqual("UI/HUD.cs", "UI/HUD.cs".replace("\\", "/"))

    def test_macho_architecture_detects_x64_and_arm64(self):
        headers = {
            "x86_64": b"\xcf\xfa\xed\xfe\x07\x00\x00\x01",
            "arm64": b"\xcf\xfa\xed\xfe\x0c\x00\x00\x01",
        }
        with tempfile.TemporaryDirectory() as root:
            for expected, header in headers.items():
                path = pathlib.Path(root, expected)
                path.write_bytes(header)
                with self.subTest(expected=expected):
                    self.assertEqual(expected, VALIDATE_ALL.macho_architecture(path))

    @mock.patch.object(VALIDATE_ALL.platform, "system", return_value="Darwin")
    @mock.patch.object(VALIDATE_ALL.platform, "machine", return_value="x86_64")
    @mock.patch.object(VALIDATE_ALL.subprocess, "run")
    def test_native_machine_detects_apple_silicon_under_rosetta(self, run, _machine, _system):
        run.return_value = subprocess.CompletedProcess([], 0, stdout="1\n", stderr="")
        self.assertEqual("arm64", VALIDATE_ALL.native_machine())
        self.assertEqual("osx-arm64", VALIDATE_ALL.native_runtime_id())

    def test_build_script_detects_rosetta_host(self):
        source = MODULE_PATH.with_name("build.sh").read_text(encoding="utf-8")
        self.assertIn("hw.optional.arm64", source)
        self.assertIn('ARCH="arm64"', source)

    def test_build_scripts_remove_intermediate_release_outputs(self):
        shell_source = MODULE_PATH.with_name("build.sh").read_text(encoding="utf-8")
        batch_source = MODULE_PATH.with_name("build.bat").read_text(encoding="utf-8")
        self.assertIn('rm -rf "${OUTPUT_DIR}/Release"', shell_source)
        self.assertIn('rmdir /s /q "%OUTPUT_DIR%\\Release"', batch_source)

    def test_binary_selection_accepts_one_or_identical_copies(self):
        with tempfile.TemporaryDirectory() as root:
            bin_dir = pathlib.Path(root)
            canonical = bin_dir / "RoslynValidator.exe"
            canonical.write_bytes(b"current")
            self.assertEqual(
                str(canonical), VALIDATE_ALL.find_binary(str(bin_dir), "Windows"))

            duplicate = bin_dir / "Release/net6.0/win-x64/RoslynValidator.exe"
            duplicate.parent.mkdir(parents=True)
            duplicate.write_bytes(b"current")
            self.assertEqual(
                str(canonical), VALIDATE_ALL.find_binary(str(bin_dir), "Windows"))

    def test_binary_selection_rejects_divergent_copies(self):
        with tempfile.TemporaryDirectory() as root:
            bin_dir = pathlib.Path(root)
            (bin_dir / "RoslynValidator.exe").write_bytes(b"stale")
            duplicate = bin_dir / "Release/net6.0/win-x64/RoslynValidator.exe"
            duplicate.parent.mkdir(parents=True)
            duplicate.write_bytes(b"current")
            with self.assertRaisesRegex(RuntimeError, "ROSLYN_BINARY_AMBIGUOUS"):
                VALIDATE_ALL.find_binary(str(bin_dir), "Windows")

    def test_package_precompiled_references_report_load_failures(self):
        source = MODULE_PATH.with_name("Program.cs").read_text(encoding="utf-8")
        section = source.split("static void AddPackagePrecompiledReferences", 1)[1].split(
            "static bool IsManagedAssembly", 1)[0]
        self.assertIn("TryAddReference(references, dll, diagnostics);", section)
        self.assertNotIn("TryAddReference(references, dll);", section)

    # Extension copies may ship different skill sets; cover whichever deploy scripts exist.
    # csharp-code-writer's deploy_scripts.cs was ported into the package as TJGameScriptsDeploy.cs.
    DEPLOY_SCRIPTS = [
        PROJECT_ROOT / ".codely-cli/extensions/TJUIBuilder/skills/solo-mode-router/references/scripts/r4_deploy_scripts.cs",
        PROJECT_ROOT / ".codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui/Editor/UIBuildTools/TJGameScriptsDeploy.cs",
    ]

    @unittest.skipUnless(any(path.exists() for path in DEPLOY_SCRIPTS),
                         "no deploy script references present in this extension copy")
    def test_deploy_scripts_cover_recursive_extra_content_and_case(self):
        scripts = [path for path in self.DEPLOY_SCRIPTS if path.exists()]
        for path in scripts:
            source = path.read_text(encoding="utf-8")
            with self.subTest(path=path.name):
                self.assertIn("SearchOption.AllDirectories", source)
                self.assertIn("Extra", source) if path.name.startswith("r4") else self.assertIn("额外脚本", source)
                self.assertIn("Content mismatch", source) if path.name.startswith("r4") else self.assertIn("内容不一致", source)
                self.assertIn("Case collision", source) if path.name.startswith("r4") else self.assertIn("大小写冲突", source)
                self.assertIn("SCRIPT_DEPLOY_SUMMARY", source)
                self.assertNotIn("File.Delete", source)


if __name__ == "__main__":
    unittest.main()
