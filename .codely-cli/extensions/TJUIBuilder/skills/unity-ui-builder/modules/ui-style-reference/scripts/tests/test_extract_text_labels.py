"""Regression tests for extract_text_labels.py geometry.

Dilation is a grouping device only: reported boxes must be the tight bounding
boxes of the label's foreground pixels on the ORIGINAL (non-dilated) mask.
Using the dilated connected-component stats directly would inflate every box
by up to (dilate_kernel_width - 1) px horizontally and shift x left by roughly
half that amount, silently corrupting every downstream layout percentage.
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


SCRIPT = Path(__file__).resolve().parents[1] / "extract_text_labels.py"


def _load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class TightBboxRegression(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("extract_text_labels", SCRIPT)

    def _extract(self, tmp: Path):
        image_path = tmp / "text_only.png"
        transform_path = tmp / "transform.json"
        img = np.zeros((288, 512, 3), dtype=np.uint8)
        img[:, :] = (0, 255, 0)  # BGR: pure green key colour
        # One line of two "characters" 30 px apart (gap < dilate 50 -> merges)
        img[50:70, 100:120] = (255, 255, 255)
        img[50:70, 150:180] = (255, 255, 255)
        # One isolated block
        img[200:220, 300:340] = (255, 255, 255)
        # Two blocks 100 px apart (gap > dilate 49 -> stays separate)
        img[260:280, 100:116] = (255, 255, 255)
        img[260:280, 216:232] = (255, 255, 255)
        cv2.imwrite(str(image_path), img)
        transform = {
            "key_color": {"rgb": {"r": 0, "g": 255, "b": 0}},
            "source_size": {"width": 1024, "height": 576},
            "square_size": 1024,
            "scale": 0.5,
            "scaled_content_rect": {"x": 0, "y": 0, "width": 512, "height": 288},
        }
        transform_path.write_text(json.dumps(transform), encoding="utf-8")
        return self.module.extract_text_labels(
            image_path, transform_path, tmp / "out",
            min_area=100, min_dimension=16, dilate_kernel_width=50,
        )

    def test_bbox_is_tight_on_original_mask(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._extract(Path(tmp))
            boxes = {
                (c["root_box"]["x"], c["root_box"]["y"],
                 c["root_box"]["width"], c["root_box"]["height"])
                for c in result["components"]
            }
            self.assertEqual(boxes, {
                (100, 50, 80, 20),    # merged line, tight union of both blocks
                (300, 200, 40, 20),   # isolated block, exact
                (100, 260, 16, 20),   # far-apart blocks stay separate and tight
                (216, 260, 16, 20),
            })

    def test_original_image_box_maps_to_source_space(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._extract(Path(tmp))
            merged = next(c for c in result["components"] if c["root_box"]["y"] == 50)
            ob = merged["original_image_box"]
            self.assertEqual((ob["x"], ob["y"], ob["width"], ob["height"]),
                             (200, 100, 160, 40))

    def test_relative_box_and_crop_match_tight_geometry(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = self._extract(Path(tmp))
            merged = next(c for c in result["components"] if c["root_box"]["y"] == 50)
            self.assertEqual(
                (merged["relative_parent_box"]["x"], merged["relative_parent_box"]["y"],
                 merged["relative_parent_box"]["width"], merged["relative_parent_box"]["height"]),
                (100, 50, 80, 20),
            )
            crop = cv2.imread(str(Path(tmp) / "out" / "component_001.png"), cv2.IMREAD_GRAYSCALE)
            self.assertIsNotNone(crop)
            self.assertEqual(crop.shape, (20, 80))


if __name__ == "__main__":
    unittest.main()
