"""Regression tests for preflight_check.py label_geometry gate.

The gate catches the mounting defect classes observed in the wild
(2026-09-03/04 ChongZhi E2E):

1. Wrong-parent percent: a label's ``relative_parent_box`` percent computed
   against a large pre-split parent, then projected onto the much smaller
   split child (purchase-button text compressed 236px -> 93px).
2. Wrong base for panel-level labels: percent recomputed against the square
   2048x2048 canvas instead of content_rect (y off by ~0.56x).
3. Four top-level scalars missing / out of sync with ``relative_parent_box``
   (board import silently falls back to stale geometry).
4. Zero-sized root_box.

Internally consistent but *wrong* AI-estimated boxes are out of scope here;
that class is prevented by the §8.4.5 rule (geometry must come from OpenCV).
"""

import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "preflight_check.py"


def _load_module():
    spec = importlib.util.spec_from_file_location("preflight_check", SCRIPT)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def _rb(x, y, w, h, cw=2048, ch=1152):
    pct = {"x": x / cw * 100.0, "y": y / ch * 100.0,
           "width": w / cw * 100.0, "height": h / ch * 100.0}
    return {"x": x, "y": y, "width": w, "height": h,
            "percent": pct, "percentage": {k: v / 100.0 for k, v in pct.items()}}


def _label(nid, root_box, rpb_pct, parent=None, scalars=True, state="valid"):
    sem = {"render_mode": "text", "source": "uss_only", "state": state}
    node = {
        "node_id": nid,
        "parent_id": parent,
        "root_box": root_box,
        "relative_parent_box": {
            "x": 0, "y": 0, "width": root_box["width"], "height": root_box["height"],
            "percent": rpb_pct, "percentage": {k: v / 100.0 for k, v in rpb_pct.items()},
        },
        "semantic": sem,
    }
    if scalars:
        node["relative_parent_x_pct"] = rpb_pct["x"]
        node["relative_parent_y_pct"] = rpb_pct["y"]
        node["relative_parent_w_pct"] = rpb_pct["width"]
        node["relative_parent_h_pct"] = rpb_pct["height"]
    return node


def _tree(nodes):
    return {"schema": "ui-component-tree/v1",
            "content_rect": {"x": 0, "y": 0, "width": 2048, "height": 1152},
            "nodes": nodes}


def _run_gate(tree):
    module = _load_module()
    return module._label_geometry_errors(tree)


def _pct(x, y, w, h, bx, by, bw, bh):
    return {"x": (x - bx) / bw * 100.0, "y": (y - by) / bh * 100.0,
            "width": w / bw * 100.0, "height": h / bh * 100.0}


def _pct_of_box(box, base_w, base_h):
    return {"x": box["x"] / base_w * 100.0, "y": box["y"] / base_h * 100.0,
            "width": box["width"] / base_w * 100.0, "height": box["height"] / base_h * 100.0}


class LabelGeometryGate(unittest.TestCase):
    def test_good_tree_passes(self):
        # root composite P (809,279,1191,674); child C slot-percent mounted; two labels:
        # depth-1 label on P, depth-2 label on C (parent chain projection), panel-level label.
        p_rb = _rb(809, 279, 1191, 674)
        c_rpb = {"x": 1.67, "y": 0.7, "width": 95.7, "height": 16.7}
        comp_p = {"node_id": "component-002", "parent_id": None, "root_box": p_rb,
                  "relative_parent_box": {"percent": _pct_of_box(p_rb, 2048, 1152)},
                  "semantic": {"component_type": "container", "state": "valid"}}
        comp_c = {"node_id": "component-002-001", "parent_id": "component-002",
                  "root_box": _rb(829, 287, 1140, 113),
                  "relative_parent_box": {"percent": c_rpb},
                  "semantic": {"component_type": "button", "state": "valid"}}
        # depth-1 label on P: box (864,324,195,31) inside p_rb
        l1_rb = _rb(864, 324, 195, 31)
        l1_pct = _pct(864, 324, 195, 31, 809, 279, 1191, 674)
        l1 = _label("text-label-005", l1_rb, l1_pct, parent="component-002")
        # depth-2 label on C: C abs = P ⊕ c_rpb = (828.9, 283.7, 1141.9, 112.6)
        # label box (870,300,90,20) inside C abs
        c_abs = (809 + 1191 * 1.67 / 100, 279 + 674 * 0.7 / 100,
                 1191 * 95.7 / 100, 674 * 16.7 / 100)
        l2_rb = _rb(870, 300, 90, 20)
        l2_pct = _pct(870, 300, 90, 20, *c_abs)
        l2 = _label("text-label-020", l2_rb, l2_pct, parent="component-002-001")
        # panel-level label: rpb == root percent
        l3_rb = _rb(1763, 115, 238, 24)
        l3 = _label("text-label-003", l3_rb, _pct_of_box(l3_rb, 2048, 1152), parent=None)
        errs = _run_gate(_tree([comp_p, comp_c, l1, l2, l3]))
        self.assertEqual(errs, [], f"expected clean, got: {errs}")

    def test_wrong_parent_percent_fails(self):
        # Label truly at (1649,1014,252,39) but percent computed against the big
        # pre-split bar (1078px wide) while mounted on the small button (367px).
        comp = {"node_id": "component-003-003", "parent_id": None,
                "root_box": _rb(1482, 919, 367, 97),
                "relative_parent_box": {"percent": _pct_of_box(_rb(1482, 919, 367, 97), 2048, 1152)},
                "semantic": {"component_type": "button", "state": "valid"}}
        stale = _pct(1649, 1014, 252, 39, 1470, 925, 1078, 105)  # vs old bar
        label = _label("text-label-019", _rb(1649, 1014, 252, 39), stale,
                       parent="component-003-003")
        errs = _run_gate(_tree([comp, label]))
        self.assertTrue(any("mount reconstruction mismatch" in e for e in errs), errs)
        self.assertTrue(any("text-label-019" in e for e in errs), errs)

    def test_panel_level_square_canvas_base_fails(self):
        # Panel-level label: root percent uses content_rect (y=9.98) but rpb
        # percent was recomputed against the 2048x2048 square (y=5.61).
        rb = _rb(1763, 115, 238, 24)
        square_pct = {"x": 86.08, "y": 115 / 2048 * 100.0,
                      "width": 11.62, "height": 24 / 2048 * 100.0}
        label = _label("text-label-003", rb, square_pct, parent=None)
        errs = _run_gate(_tree([label]))
        self.assertTrue(any("panel-level" in e and "content_rect" in e for e in errs), errs)

    def test_missing_four_scalars_fails(self):
        rb = _rb(1763, 115, 238, 24)
        label = _label("text-label-003", rb, _pct_of_box(rb, 2048, 1152),
                       parent=None, scalars=False)
        errs = _run_gate(_tree([label]))
        self.assertTrue(any("four-scalar" in e and "missing" in e for e in errs), errs)

    def test_desynced_four_scalars_fails(self):
        rb = _rb(1763, 115, 238, 24)
        pct = _pct_of_box(rb, 2048, 1152)
        label = _label("text-label-003", rb, pct, parent=None)
        label["relative_parent_w_pct"] = pct["width"] + 5.0
        errs = _run_gate(_tree([label]))
        self.assertTrue(any("out of sync" in e for e in errs), errs)

    def test_zero_sized_root_box_fails(self):
        label = _label("text-label-000", _rb(100, 100, 0, 0), {"x": 5, "y": 9, "width": 0, "height": 0})
        errs = _run_gate(_tree([label]))
        self.assertTrue(any("zero-sized" in e for e in errs), errs)

    def test_icon_only_panel_passes(self):
        # Zero text labels is legitimate (icon-only panel): the gate must pass,
        # not fail with a "nothing to check / tree mis-built" error.
        icon = {"node_id": "component-001", "parent_id": None,
                "root_box": _rb(100, 100, 400, 300),
                "relative_parent_box": {"percent": _pct_of_box(_rb(100, 100, 400, 300), 2048, 1152)},
                "semantic": {"component_type": "icon", "state": "valid"}}
        for tree in (_tree([]), _tree([icon])):
            errs = _run_gate(tree)
            self.assertEqual(errs, [], f"icon-only tree must pass, got: {errs}")

    def test_icon_only_cli_exit_code_zero(self):
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "tree.json"
            p.write_text(json.dumps(_tree([])), encoding="utf-8")
            r = subprocess.run(
                [sys.executable, str(SCRIPT), "label_geometry", str(p)],
                capture_output=True, text=True,
            )
            self.assertEqual(r.returncode, 0,
                             f"rc={r.returncode} out={r.stdout} err={r.stderr}")
            self.assertIn("icon-only", r.stdout)
            self.assertIn("pass", r.stdout)

    def test_malformed_null_semantic_does_not_crash(self):
        # JSON ``"semantic": null`` used to raise AttributeError ("NoneType"
        # object has no attribute "get") — the gate must skip such nodes and
        # report, never crash. Validators handle malformed input, not throw.
        bad = {"node_id": "n-broken", "parent_id": None, "root_box": None,
               "semantic": None}
        rb = _rb(1763, 115, 238, 24)
        good = _label("text-label-003", rb, _pct_of_box(rb, 2048, 1152), parent=None)
        for tree in (_tree([bad]), _tree([bad, good])):
            errs = _run_gate(tree)
            self.assertEqual(errs, [], f"malformed semantic must be skipped, got: {errs}")

    def test_label_without_node_id_reported_as_unnamed(self):
        # A text label missing node_id must be reported under <unnamed>,
        # not crash with KeyError.
        anon = {"parent_id": None,
                "root_box": _rb(10, 10, 50, 20),
                "relative_parent_box": {"percent": _pct_of_box(_rb(10, 10, 50, 20), 2048, 1152)},
                "semantic": {"render_mode": "text", "source": "uss_only", "state": "valid"}}
        errs = _run_gate(_tree([anon]))
        self.assertTrue(errs, "expected four-scalar errors for the unnamed label")
        self.assertTrue(all("<unnamed>" in e for e in errs), errs)

    def test_bom_prefixed_tree_loads(self):
        # Trees saved by some Windows tools carry a UTF-8 BOM; the CLI must
        # read them (utf-8-sig) instead of raising JSONDecodeError.
        with tempfile.TemporaryDirectory() as td:
            p = Path(td) / "tree.json"
            p.write_text(json.dumps(_tree([])), encoding="utf-8-sig")
            r = subprocess.run(
                [sys.executable, str(SCRIPT), "label_geometry", str(p)],
                capture_output=True, text=True,
            )
            self.assertEqual(r.returncode, 0,
                             f"rc={r.returncode} out={r.stdout} err={r.stderr}")
            self.assertIn("icon-only", r.stdout)

    def test_cli_exit_codes(self):
        rb = _rb(1504, 37, 91, 27)
        good_pct = _pct_of_box(rb, 2048, 1152)
        good = _tree([_label("text-label-001", rb, good_pct, parent=None)])
        bad_pct = dict(good_pct, y=good_pct["y"] - 1.4)
        bad = _tree([_label("text-label-001", rb, bad_pct, parent=None)])
        with tempfile.TemporaryDirectory() as td:
            for name, tree, want in (("good", good, 0), ("bad", bad, 1)):
                p = Path(td) / f"{name}.json"
                p.write_text(json.dumps(tree), encoding="utf-8")
                r = subprocess.run(
                    [sys.executable, str(SCRIPT), "label_geometry", str(p)],
                    capture_output=True, text=True,
                )
                self.assertEqual(r.returncode, want,
                                 f"{name}: rc={r.returncode} out={r.stdout} err={r.stderr}")

    def test_real_chongzhi_tree_if_present(self):
        # If the live ChongZhi tree exists, the gate must pass on it (regression
        # anchor for the fixed tree; skipped otherwise).
        live = None
        probe = Path(__file__).resolve()
        for parent in probe.parents:
            if (parent / "context" / "games" / "ChongZhi").is_dir():
                live = parent / ("context/games/ChongZhi/UIReferences/Processing/"
                                 "RechargePanel/component-tree.json")
                break
        if live is None or not live.is_file():
            self.skipTest("live ChongZhi tree not present")
        module = _load_module()
        errs = module._label_geometry_errors(json.loads(live.read_text(encoding="utf-8")))
        self.assertEqual(errs, [], f"live tree should be clean, got: {errs}")


if __name__ == "__main__":
    unittest.main()
