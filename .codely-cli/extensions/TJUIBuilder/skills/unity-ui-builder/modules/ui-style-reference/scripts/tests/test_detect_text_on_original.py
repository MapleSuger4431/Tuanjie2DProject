"""Regression tests for detect_text_on_original.py (fallback text detection on the original image).

Pinned contracts:
- reported boxes are tight bboxes of raw-mask pixels (horizontal dilation groups only);
- equidistant-row validation rejects stray bands off the spacing lattice;
- leading solid icon columns glued to a line by dilation are trimmed (and audited);
- root/parent coordinate mapping matches the §8.4.5 formulas exactly;
- key-colour pixels never produce detections; thin vertical rail lines are suppressed;
- zero detection is a legal, reported outcome, never an error.
"""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

try:
    import cv2  # noqa: F401
    import numpy as np  # noqa: F401
    HAS_CV2 = True
except ImportError:
    HAS_CV2 = False


SCRIPT = Path(__file__).resolve().parents[1] / "detect_text_on_original.py"


def _load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def _transform():
    return {
        "key_color": {"rgb": {"r": 0, "g": 255, "b": 0}},
        "source_size": {"width": 512, "height": 320},
        "square_size": 512,
        "scale": 4.0,
        "scaled_content_rect": {"x": 0, "y": 0, "width": 2048, "height": 1280},
        "original_content_rect": {"x": 0, "y": 0, "width": 512, "height": 320},
    }


def _paint(tmp: Path, rows=(), icon=None, stray=None, divider=None, key_blob=None):
    """512x512 source_square: dark 512x320 panel top-left, green key padding below.

    Each row in ``rows`` is a fake text line: two solid 20x16 bars 10 px apart
    (dilate-width 50 bridges them into one line-sized blob).  All colours are
    channel-symmetric so BGR/RGB order does not matter.
    """
    image_path = tmp / "source_square.png"
    transform_path = tmp / "transform.json"
    img = np.zeros((512, 512, 3), dtype=np.uint8)
    img[:, :] = (0, 255, 0)          # BGR: green key colour padding
    img[:320, :] = (40, 40, 40)      # dark panel = original image content
    for y0 in rows:
        img[y0:y0 + 16, 60:80] = (255, 255, 255)
        img[y0:y0 + 16, 90:110] = (255, 255, 255)
    for block in (icon, stray, divider):
        if block:
            x, y, w, h = block
            img[y:y + h, x:x + w] = (255, 255, 255)
    if key_blob:
        x, y, w, h = key_blob
        img[y:y + h, x:x + w] = (0, 255, 0)  # key colour inside the content area
    cv2.imwrite(str(image_path), img)
    transform_path.write_text(json.dumps(_transform()), encoding="utf-8")
    return image_path, transform_path


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class DetectTextOnOriginalRegression(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("detect_text_on_original", SCRIPT)

    def _run(self, tmp: Path, **kwargs):
        paint_args = {k: kwargs.pop(k) for k in
                      ("rows", "icon", "stray", "divider", "key_blob") if k in kwargs}
        image_path, transform_path = _paint(tmp, **paint_args)
        return self.module.detect_text_on_original(
            image_path, transform_path, tmp / "out", **kwargs)

    @staticmethod
    def _pixels(box):
        return (box["x"], box["y"], box["width"], box["height"])

    def test_rows_detected_tight_bbox_icon_trimmed(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._run(Path(tmp), rows=(40, 80, 120), icon=(10, 40, 30, 16))
            self.assertEqual(len(result["components"]), 3)
            orig_boxes = {self._pixels(c["original_image_box"]) for c in result["components"]}
            self.assertEqual(orig_boxes, {(60, 40, 50, 16), (60, 80, 50, 16), (60, 120, 50, 16)})
            root_boxes = {self._pixels(c["root_box"]) for c in result["components"]}
            self.assertEqual(root_boxes, {(240, 160, 200, 64), (240, 320, 200, 64), (240, 480, 200, 64)})
            # The 10 px inter-bar gap was bridged by dilation for grouping only —
            # geometry stays the tight union (50 px, not inflated by dilate-width).
            self.assertTrue(all(c["root_box"]["width"] == 200 for c in result["components"]))
            audit = result["detection_audit"]
            self.assertEqual(audit["selected_channel"], "bright")
            self.assertEqual(audit["spacing"], 40.0)
            self.assertFalse(audit["zero_detect"])
            self.assertEqual(len(audit["trims"]), 1)
            self.assertEqual(audit["trims"][0]["side"], "leading")
            self.assertEqual(audit["trims"][0]["columns"], 30)
            first = result["components"][0]
            self.assertEqual(first["root_box"]["percentage"]["x"], 240 / 2048)
            self.assertEqual(self._pixels(first["relative_parent_box"]),
                             (240, 160, 200, 64))
            self.assertTrue(Path(first["file_path"]).exists())

    def test_equidistant_validation_rejects_off_lattice_stray(self):
        with tempfile.TemporaryDirectory() as tmp:
            # 17x17 blob at y=143: size-legal on its own but its row centre (151)
            # sits 17 px off the 40-px lattice of the three real rows (tol = 8).
            result = self._run(Path(tmp), rows=(40, 80, 120), stray=(200, 143, 17, 17))
            self.assertEqual(len(result["components"]), 3)
            self.assertEqual({c["original_image_box"]["y"] for c in result["components"]},
                             {40, 80, 120})
            audit = result["detection_audit"]
            rejected = [b for b in audit["bands"] if not b["accepted"]]
            self.assertEqual(len(rejected), 1)
            self.assertEqual(rejected[0]["reject_reason"], "off_lattice")
            self.assertEqual(audit["spacing"], 40.0)

    def test_known_rows_prior(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._run(Path(tmp), rows=(40, 80, 120), known_rows=3)
            self.assertEqual(result["detection_audit"]["warnings"], [])
        with tempfile.TemporaryDirectory() as tmp:
            result = self._run(Path(tmp), rows=(40, 80, 120), known_rows=4)
            self.assertTrue(any("known_rows=4" in w
                                for w in result["detection_audit"]["warnings"]))

    def test_parent_mode_mapping_matches_8_4_5_formula(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._run(Path(tmp), rows=(40, 80, 120), mode="parent",
                               crop_rect=(0, 0, 512, 320),
                               parent_root_box=(100, 50, 400, 256),
                               parent_original_box=(0, 0, 512, 320))
            self.assertEqual(len(result["components"]), 3)
            first = result["components"][0]
            root = first["root_box"]
            # x = P.root_box.x + round(P.root_box.w * 60 / 512) etc. (edge-mapped)
            self.assertEqual(self._pixels(root), (147, 82, 39, 13))
            self.assertEqual((root["image_x"], root["image_y"]), (147, 82))
            self.assertAlmostEqual(root["percentage"]["x"], 47 / 400, places=6)
            self.assertEqual(self._pixels(first["original_image_box"]), (60, 40, 50, 16))
            self.assertEqual(self._pixels(first["relative_parent_box"]), (147, 82, 39, 13))

    def test_zero_detect_is_legal_outcome_and_key_colour_excluded(self):
        with tempfile.TemporaryDirectory() as tmp:
            # The green blob would match the bright channel (gray ≈ 150) but is
            # key colour — it must never become a detection.
            result = self._run(Path(tmp), key_blob=(300, 100, 30, 30))
            self.assertEqual(result["components"], [])
            audit = result["detection_audit"]
            self.assertTrue(audit["zero_detect"])
            self.assertIsNone(audit["selected_channel"])

    def test_vertical_rail_line_suppression(self):
        with tempfile.TemporaryDirectory() as tmp:
            # A 4 px divider crossing all rows would merge them into one
            # oversized band (0 detections). Suppression must save the rows.
            result = self._run(Path(tmp), rows=(40, 80, 120), divider=(200, 20, 4, 180))
            self.assertEqual(len(result["components"]), 3)
            self.assertEqual({c["original_image_box"]["y"] for c in result["components"]},
                             {40, 80, 120})
            audit = result["detection_audit"]
            bright = next(ch for ch in audit["channels"] if ch["channel"] == "bright")
            self.assertEqual(bright["suppressed_columns"], 4)

    def test_forced_channel_without_structured_rows(self):
        with tempfile.TemporaryDirectory() as tmp:
            # Forcing the dark channel on a dark-panel fixture finds no
            # text-plausible rows — zero detection, no crash.
            result = self._run(Path(tmp), rows=(40, 80, 120), channels="dark")
            self.assertEqual(result["components"], [])
            self.assertTrue(result["detection_audit"]["zero_detect"])


if __name__ == "__main__":
    unittest.main()
