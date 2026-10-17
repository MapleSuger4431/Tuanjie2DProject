import json
import os
import pathlib
import subprocess
import tempfile
import unittest

TOOL = pathlib.Path(__file__).parent
PROJECT_ROOT = pathlib.Path(__file__).parents[5]
# build.bat publishes RoslynValidator.exe on Windows; POSIX keeps the extensionless binary.
BIN = TOOL / "bin" / ("RoslynValidator.exe" if os.name == "nt" else "RoslynValidator")

BAD = r'''
using System;
class Bad {
  // TODO implement
  int Constant() { return 1; }
  int Guard(int x) { if (x < 0) return 0; return x * 2; }
  void Missing() { throw new NotImplementedException(); }
  // placeholder
  int Value { get { return default; } }
  // TODO generic work
  void RunLater() { }
  // TODO core
  void CalculateScore() { }
  // TODO platform support
  void Save() { throw new NotSupportedException(); }
}
'''

GOOD = r'''
using System;
interface IGood { void Run(); }
abstract class Base { public abstract int Read(); }
class Good {
  int Guard(int x) { if (x < 0) return 0; return x * 2; }
  const int LegalConstant = 3;
  void Update() { }
}
'''

class GameScriptCompletenessTests(unittest.TestCase):
    def run_validator(self, files):
        with tempfile.TemporaryDirectory() as root:
            base = pathlib.Path(root)
            for name, content in files.items():
                path = base / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(content, encoding="utf-8")
            game = "completeness_" + pathlib.Path(next(iter(files))).stem.lower()
            proc = subprocess.run([str(BIN), "-g", game, "-b", str(base), "--recursive", "--rules", "--profile", "game-scripts"], cwd=PROJECT_ROOT, text=True, capture_output=True, encoding="utf-8", errors="replace")
            result = json.loads((PROJECT_ROOT / "Temp" / f"result_{game}.json").read_text(encoding="utf-8"))
            return proc, result

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_bad_stubs_block_and_nested_file_is_scanned(self):
        proc, result = self.run_validator({"Nested/Bad.cs": BAD})
        ids = {d.get("id") for d in result["diagnostics"]}
        self.assertNotEqual(0, proc.returncode)
        self.assertTrue({"UR025", "UR032", "UR033", "UR034", "UR035", "UR036"}.issubset(ids))
        self.assertEqual("game-scripts", result["hardGate"]["profile"])
        self.assertFalse(result["hardGate"]["passed"])
        self.assertIn("UR032", result["hardGate"]["blockingDiagnosticIds"])

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_guard_abstract_interface_and_legal_constant_pass(self):
        proc, result = self.run_validator({
            "Good.cs": GOOD,
            "Generated/Auto.g.cs": "using System; class Auto { void Build() { throw new NotImplementedException(); } }"
        })
        completeness = [d for d in result["diagnostics"] if (d.get("id") or "").startswith("UR03")]
        self.assertEqual([], completeness)
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertTrue(result["hardGate"]["passed"])

if __name__ == "__main__":
    unittest.main()
