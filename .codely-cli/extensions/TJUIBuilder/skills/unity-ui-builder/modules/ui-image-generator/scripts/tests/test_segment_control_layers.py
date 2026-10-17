"""Regression tests for segment_control_layers.py (v2 方案 C — script geometry).

Pinned contracts:
- geometry from pixels: size_ratio is bbox / alpha-visible bbox (NOT the canvas —
  padded canvases must not skew ratios); fills_parent = both axis ratios >= 0.9;
- baked multi-colour components (track+handle drawn over each other) split via
  colour clustering into separate layers;
- single-colour components are marked ``indistinguishable`` — the deterministic
  signal for the §3.1 rule "fill 与 track 视觉完全相同 → 不提取 fill";
- near fragments merge (hand-drawn controls shatter);
- fewer than 2 layers → fallback signal (caller falls back to AI-only analysis,
  marked 未经脚本几何).
"""

import importlib.util
from pathlib import Path
import tempfile
import unittest

try:
    import cv2  # noqa: F401
    import numpy as np  # noqa: F401
    HAS_CV2 = True
except ImportError:
    HAS_CV2 = False

SCRIPT = Path(__file__).resolve().parents[1] / "segment_control_layers.py"


def _load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def _save(path: Path, array: np.ndarray):
    from PIL import Image
    Image.fromarray(array.astype(np.uint8), "RGBA").save(path)


def _transparent(w, h):
    return np.zeros((h, w, 4), np.uint8)


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class SegmentControlLayersTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("segment_control_layers", SCRIPT)

    def test_baked_two_colour_slider_splits(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = _transparent(256, 128)
            # track bar with the handle baked over its top half (one alpha component)
            img[56:72, 32:224] = (60, 120, 200, 255)
            img[52:76, 100:124] = (220, 180, 60, 255)
            path = Path(tmp) / "slider.png"
            _save(path, img)
            result = self.module.segment_control_layers(path)
            self.assertEqual(result["verdict"], "pass")
            self.assertEqual(result["layer_count"], 2)
            layers = {rec["layer_index"]: rec for rec in result["layers"]}
            track, handle = layers[1], layers[2]
            self.assertEqual(track["pixel_count"] > handle["pixel_count"], True)
            # alpha bbox: x 32..223 (w 192), y 52..75 (h 24)
            self.assertEqual(result["alpha_bbox"], {"x": 32, "y": 52, "width": 192, "height": 24})
            self.assertEqual(track["bbox"], {"x": 32, "y": 56, "width": 192, "height": 16})
            self.assertEqual(handle["bbox"], {"x": 100, "y": 52, "width": 24, "height": 24})
            self.assertEqual(track["size_ratio"], {"width": 1.0, "height": round(16 / 24, 4)})
            self.assertEqual(handle["size_ratio"], {"width": 0.125, "height": 1.0})
            self.assertFalse(track["fills_parent"])
            self.assertFalse(handle["fills_parent"])
            self.assertFalse(track["indistinguishable"])
            self.assertFalse(handle["indistinguishable"])

    def test_single_colour_component_falls_back(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = _transparent(256, 128)
            img[56:72, 32:224] = (60, 120, 200, 255)
            path = Path(tmp) / "slider.png"
            _save(path, img)
            result = self.module.segment_control_layers(path)
            self.assertEqual(result["verdict"], "review")
            self.assertTrue(result["fallback"])
            self.assertEqual(result["layer_count"], 1)
            self.assertTrue(result["layers"][0]["indistinguishable"])

    def test_disjoint_layers_pass_without_split(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = _transparent(256, 128)
            img[56:72, 32:132] = (60, 120, 200, 255)
            img[20:44, 180:204] = (200, 60, 60, 255)
            path = Path(tmp) / "control.png"
            _save(path, img)
            result = self.module.segment_control_layers(path)
            self.assertEqual(result["verdict"], "pass")
            self.assertEqual(result["layer_count"], 2)

    def test_fragmented_hand_drawn_merges(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = _transparent(256, 128)
            for x in (32, 55, 78, 101, 124):  # 3 px gaps → merge_gap glues them
                img[56:72, x:x + 20] = (60, 120, 200, 255)
            img[20:44, 180:204] = (200, 60, 60, 255)
            path = Path(tmp) / "control.png"
            _save(path, img)
            result = self.module.segment_control_layers(path)
            self.assertEqual(result["verdict"], "pass")
            self.assertEqual(result["layer_count"], 2)
            dashed = next(rec for rec in result["layers"] if rec["bbox"]["y"] == 56)
            self.assertEqual(dashed["bbox"]["x"], 32)
            self.assertEqual(dashed["bbox"]["width"], 112)  # 32..143 incl. gaps

    def test_padded_canvas_ratios_use_alpha_bbox(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = _transparent(512, 512)
            img[120:136, 160:352] = (60, 120, 200, 255)   # bar 192x16
            img[140:204, 320:384] = (200, 60, 60, 255)    # square 64x64
            path = Path(tmp) / "control.png"
            _save(path, img)
            result = self.module.segment_control_layers(path)
            self.assertEqual(result["verdict"], "pass")
            # alpha bbox: x 160..383 (w 224), y 120..203 (h 84) — NOT the 512 canvas
            self.assertEqual(result["alpha_bbox"], {"x": 160, "y": 120, "width": 224, "height": 84})
            bar = next(rec for rec in result["layers"] if rec["bbox"]["height"] == 16)
            self.assertEqual(bar["size_ratio"], {"width": round(192 / 224, 4),
                                                 "height": round(16 / 84, 4)})


if __name__ == "__main__":
    unittest.main()
