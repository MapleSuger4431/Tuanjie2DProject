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
FIXTURES = TOOL / "test-fixtures" / "game-scripts" / "ComponentLookup"
FRAMEWORK_API = (FIXTURES.parent / "Framework" / "FrameworkApi.cs").read_text(encoding="utf-8")


class ComponentLookupNullAnalyzerTests(unittest.TestCase):
    """UR048/UR049/UR050 — component lookups whose result is dereferenced without verification."""

    def run_validator(self, files):
        with tempfile.TemporaryDirectory() as root:
            base = pathlib.Path(root)
            # Fixtures reference framework types by FQN; supply the in-source mirror so the tests
            # do not depend on the host project's compiled package DLL.
            merged = dict(files)
            merged.setdefault("FrameworkApi.cs", FRAMEWORK_API)
            for name, source in merged.items():
                path = base / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(source, encoding="utf-8")
            game = "component_lookup_null"
            result_path = base / "result.json"
            command = [str(BIN), "-g", game, "-b", str(base), "--recursive", "--rules",
                       "--profile", "game-scripts", "--result-path", str(result_path)]
            proc = subprocess.run(
                command,
                cwd=PROJECT_ROOT, text=True, capture_output=True,
                encoding="utf-8", errors="replace")
            result = json.loads(result_path.read_text(encoding="utf-8"))
            return proc, result

    @staticmethod
    def ids(result):
        return [d.get("id") for d in result["diagnostics"]]

    @staticmethod
    def diagnostics_for(result, rule_id):
        return [d for d in result["diagnostics"] if d.get("id") == rule_id]

    @staticmethod
    def fixture_source(category, name):
        return (FIXTURES / category / name).read_text(encoding="utf-8")

    # ── UR048 (Error) — high-confidence origin: Instantiate ──

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_instantiate_dereferenced_unverified(self):
        source = self.fixture_source("Bad", "PooledDereference.cs")
        proc, result = self.run_validator({"PooledDereference.cs": source})
        self.assertNotEqual(0, proc.returncode, proc.stdout + proc.stderr)
        errors = self.diagnostics_for(result, "UR048")
        self.assertEqual(2, len(errors))
        self.assertTrue(all(e["severity"] == "error" for e in errors))
        sources = {e["message"].split(" obtained from ", 1)[1].split(" ", 1)[0] for e in errors}
        self.assertEqual({"Object.Instantiate()"}, sources)

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_guidance_names_component_contract_and_exception(self):
        """Fix advice must lead with the prefab component contract and a loud
        configuration exception — never a bare `return null`-style silent-exit recipe."""
        source = self.fixture_source("Bad", "PooledDereference.cs")
        _, result = self.run_validator({"PooledDereference.cs": source})
        messages = [e["message"] for e in self.diagnostics_for(result, "UR048")]
        self.assertEqual(2, len(messages))
        for message in messages:
            self.assertIn("component contract", message)
            self.assertIn("a configuration exception", message)
            self.assertIn("do not silently return", message)

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_is_reserved_for_high_confidence_origins(self):
        """UR048 must not fire for ordinary cross-object lookups — those stay Warning (UR049)."""
        source = self.fixture_source("Bad", "CrossObjectLookup.cs")
        proc, result = self.run_validator({"CrossObjectLookup.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertNotIn("UR048", self.ids(result))

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_uses_framework_fqns_not_user_lookalike_names(self):
        source = self.fixture_source("Good", "NamespaceLookalikes.cs")
        proc, result = self.run_validator({"NamespaceLookalikes.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertNotIn("UR048", self.ids(result))

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_tracks_two_and_three_local_hops_but_not_four(self):
        source = self.fixture_source("Bad", "LocalPropagation.cs")
        proc, result = self.run_validator({"LocalPropagation.cs": source})
        self.assertNotEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertEqual(2, len(self.diagnostics_for(result, "UR048")))

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur048_verified_lookups_are_silent(self):
        """Verified lookups (RequireComponent / TryGetComponent / ?. / loud guard / narrow exemption)
        must not be reported."""
        source = self.fixture_source("Good", "VerifiedLookups.cs")
        proc, result = self.run_validator({"VerifiedLookups.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertNotIn("UR048", self.ids(result))
        self.assertNotIn("UR049", self.ids(result))
        self.assertNotIn("UR050", self.ids(result))

    # ── UR049 (Warning) — ordinary cross-object lookups ──

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur049_cross_object_lookup_is_warning_not_error(self):
        source = self.fixture_source("Bad", "CrossObjectLookup.cs")
        proc, result = self.run_validator({"CrossObjectLookup.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        warnings = self.diagnostics_for(result, "UR049")
        self.assertEqual(1, len(warnings))
        self.assertEqual("warning", warnings[0]["severity"])
        self.assertIn("cross-object component lookup", warnings[0]["message"])

    # ── UR050 (Warning) — silent guard hides a missing component ──

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur050_silent_guard_downgrades_a_crash_into_a_noop(self):
        source = self.fixture_source("Bad", "PooledDereference.cs")
        _, result = self.run_validator({"PooledDereference.cs": source})
        warnings = self.diagnostics_for(result, "UR050")
        self.assertEqual(1, len(warnings))
        self.assertEqual("warning", warnings[0]["severity"])
        self.assertIn("null guard", warnings[0]["message"])
        self.assertIn("silent functional failure", warnings[0]["message"])

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_ur050_loud_throw_is_not_silent(self):
        """A guard that throws a configuration error surfaces the failure and must not be flagged."""
        source = self.fixture_source("Good", "VerifiedLookups.cs")
        proc, result = self.run_validator({"VerifiedLookups.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertNotIn("UR050", self.ids(result))

    # ── Exemptions — each must produce zero UR048/UR049/UR050 diagnostics ──

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_require_component_guarantees_same_object_lookup(self):
        files = {"RequireComponentExemption.cs": r'''
using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
public class RequireComponentExemption : MonoBehaviour
{
    private void Update()
    {
        GetComponent<Rigidbody2D>().Sleep();
    }
}
'''}
        proc, result = self.run_validator(files)
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertFalse(set(self.ids(result)) & {"UR048", "UR049", "UR050"})

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_try_get_component_success_branch(self):
        fixture = self.fixture_source("Good", "VerifiedLookups.cs")
        self.assertIn("TryGetComponent", fixture)
        proc, result = self.run_validator({"VerifiedLookups.cs": fixture})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertFalse(set(self.ids(result)) & {"UR048", "UR049", "UR050"})

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_null_conditional_and_coalesce_do_not_dereference(self):
        files = {"NullConditional.cs": r'''
using UnityEngine;
public class NullConditional : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;
    private void Update()
    {
        UnityEngine.Object.Instantiate(_prefab).GetComponent<Rigidbody2D>()?.Sleep();
        Rigidbody2D body = UnityEngine.Object.Instantiate(_prefab).GetComponent<Rigidbody2D>();
        Rigidbody2D safe = body == null ? null : body;
        _ = safe;
    }
}
'''}
        proc, result = self.run_validator(files)
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertFalse(set(self.ids(result)) & {"UR048", "UR049", "UR050"})

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_real_coalesce_and_coalesce_assignment(self):
        source = self.fixture_source("Good", "CoalesceVerification.cs")
        proc, result = self.run_validator({"CoalesceVerification.cs": source})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertFalse(set(self.ids(result)) & {"UR048", "UR049", "UR050"})

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_guard_followed_by_throw_surfaces_error(self):
        fixture = self.fixture_source("Good", "VerifiedLookups.cs")
        self.assertIn("throw new InvalidOperationException", fixture)
        proc, result = self.run_validator({"VerifiedLookups.cs": fixture})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertNotIn("UR050", self.ids(result))

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_exemption_reasoned_suppression_marker(self):
        fixture = self.fixture_source("Good", "VerifiedLookups.cs")
        self.assertIn("ComponentLookupExemption: reason=", fixture)
        proc, result = self.run_validator({"VerifiedLookups.cs": fixture})
        self.assertEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertFalse(set(self.ids(result)) & {"UR048", "UR049", "UR050"})

    @unittest.skipUnless(BIN.exists(), "build binary first")
    def test_reasoned_suppression_requires_a_reason(self):
        """`ComponentLookupExemption:` without a non-empty reason must NOT suppress — the marker
        fails closed rather than silently opting out."""
        files = {"BadSuppression.cs": r'''
using UnityEngine;
using UnityEngine;
public class BadSuppression : MonoBehaviour
{
    [SerializeField] private GameObject _prefab;
    private void Update()
    {
        // ComponentLookupExemption: reason=
        UnityEngine.Object.Instantiate(_prefab).GetComponent<Rigidbody2D>().Sleep();
    }
}
'''}
        proc, result = self.run_validator(files)
        self.assertNotEqual(0, proc.returncode, proc.stdout + proc.stderr)
        self.assertIn("UR048", self.ids(result))


if __name__ == "__main__":
    unittest.main()
