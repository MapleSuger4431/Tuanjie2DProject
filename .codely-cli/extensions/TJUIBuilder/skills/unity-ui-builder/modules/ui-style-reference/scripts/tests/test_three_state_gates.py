"""Regression tests for the three-state validator gates (v2 方案 B1-B6).

Pinned contracts (shared/contracts.md 三态门禁):
- verdict JSON on stdout-shaped return values; exit codes 0 = pass, 2 = review, 1 = fail;
- validate_shell: over-clean detection via enclosed-hole ratio (formerly a blind spot);
- validate_background: large content blocks (character art) excluded; scattered small
  edge clusters counted; complex images route to review, never to auto-retry;
- validate_text_only: low key coverage / large uniform block → fail (mechanical retry);
  large non-uniform block → review;
- validate_ui_only_reference: largest-contour gate (doc'd check, now implemented);
  plate-residue and over-keying size-band comparison against the 3a baseline → review;
- validate_output_page: per-slot fail/review; plate-not-removed (uniform block covering
  >= 55% of the slot) → mechanical fail; empty-slot / low-retention → review;
- find_missing_components: downgrade-rerun candidates vs baseline (IoU/containment
  filters), tiny-box flagging.
"""

import argparse
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

SCRIPTS = Path(__file__).resolve().parents[1]


def _load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def _green_transform(content_rect=None):
    return {
        "key_color": {"rgb": {"r": 0, "g": 255, "b": 0}},
        "background_mode": "key_color",
        "scaled_content_rect": content_rect or
            {"x": 0, "y": 0, "width": 2048, "height": 2048},
    }


def _save_rgba(path: Path, array: np.ndarray):
    from PIL import Image
    Image.fromarray(array.astype(np.uint8), "RGBA" if array.shape[2] == 4 else "RGB").save(path)


def _shell_args(image, **overrides):
    base = {"shell_image": image, "transform": None, "pass_variance": 50.0,
            "max_enclosed_hole_ratio": 0.10, "audit": None}
    base.update(overrides)
    return argparse.Namespace(**base)


def _background_args(image, **overrides):
    base = {"background_image": image, "max_edge_density": 0.04,
            "large_blob_ratio": 0.04, "small_max_side_ratio": 0.08,
            "max_small_count": 5, "max_small_area_ratio": 0.01, "audit": None}
    base.update(overrides)
    return argparse.Namespace(**base)


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class ValidateShellGate(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("preflight_check", SCRIPTS / "preflight_check.py")

    def test_pass_uniform_plate(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.full((256, 256, 4), (150, 150, 150, 255), np.uint8)
            path = Path(tmp) / "shell.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_shell(_shell_args(path))
            self.assertEqual(code, 0)

    def test_review_decorated_plate(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.full((256, 256, 4), (150, 150, 150, 255), np.uint8)
            img[32:48, :] = (200, 50, 50, 255)
            img[64:80, :] = (50, 200, 50, 255)
            img[96:112, :] = (50, 50, 200, 255)
            path = Path(tmp) / "shell.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_shell(_shell_args(path))
            self.assertEqual(code, 2)  # structural decoration → visual arbitration

    def test_review_over_clean_hollow(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.zeros((256, 256, 4), np.uint8)
            img[:, :] = (0, 0, 0, 0)
            img[:16, :] = (150, 150, 150, 255)   # ring top
            img[-16:, :] = (150, 150, 150, 255)   # ring bottom
            img[:, :16] = (150, 150, 150, 255)    # ring left
            img[:, -16:] = (150, 150, 150, 255)   # ring right
            path = Path(tmp) / "shell.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_shell(_shell_args(path))
            self.assertEqual(code, 2)  # enclosed hole = plate swallowed by key colour

    def test_pass_open_border_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.zeros((256, 256, 4), np.uint8)
            img[:8, :] = (150, 150, 150, 255)    # open top bar — no enclosed hole
            path = Path(tmp) / "shell.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_shell(_shell_args(path))
            self.assertEqual(code, 0)

    def test_review_rgb_without_transform(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.full((256, 256, 3), (150, 150, 150), np.uint8)
            path = Path(tmp) / "shell.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_shell(_shell_args(path))
            self.assertEqual(code, 2)

    def test_review_rgb_over_clean_with_transform(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            img = np.full((256, 256, 3), (0, 255, 0), np.uint8)  # key interior
            img[:16, :] = (255, 255, 255)
            img[-16:, :] = (255, 255, 255)
            img[:, :16] = (255, 255, 255)
            img[:, -16:] = (255, 255, 255)
            path = root / "shell.png"
            _save_rgba(path, img)
            (root / "transform.json").write_text(json.dumps(_green_transform()))
            code = self.module.cmd_validate_shell(
                _shell_args(path, transform=root / "transform.json"))
            self.assertEqual(code, 2)

    def test_fail_missing_image(self):
        code = self.module.cmd_validate_shell(_shell_args(Path("nope.png")))
        self.assertEqual(code, 1)


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class ValidateBackgroundGate(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("preflight_check", SCRIPTS / "preflight_check.py")

    @staticmethod
    def _gradient(size=256):
        img = np.zeros((size, size, 3), np.uint8)
        rows = np.linspace(40, 200, size).astype(np.uint8)
        img[:, :, 0] = rows[:, None].T
        img[:, :, 1] = rows[:, None].T
        img[:, :, 2] = rows[:, None].T
        return img

    def test_pass_gradient_with_character_art(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._gradient()
            img[60:160, 40:140] = (30, 90, 160)  # large character block (excluded)
            path = Path(tmp) / "bg.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_background(_background_args(path))
            self.assertEqual(code, 0)

    def test_review_scattered_ui_residue(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._gradient()
            for x, y in ((10, 10), (60, 10), (110, 10), (160, 10), (210, 10), (10, 220)):
                img[y:y + 16, x:x + 16] = (230, 60, 60)
            path = Path(tmp) / "bg.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_background(_background_args(path))
            self.assertEqual(code, 2)

    def test_review_complex_pattern(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._gradient()
            cell = 8
            for y in range(0, 256, cell * 2):
                for x in range(0, 256, cell):
                    if (x // cell + y // cell) % 2 == 0:
                        img[y:y + cell, x:x + cell] = (20, 20, 40)
            path = Path(tmp) / "bg.png"
            _save_rgba(path, img)
            code = self.module.cmd_validate_background(_background_args(path))
            self.assertEqual(code, 2)  # dense pattern → mechanical judgement impossible


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class ValidateTextOnlyGate(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("validate_text_only", SCRIPTS / "validate_text_only.py")

    def _run(self, tmp, image):
        transform = Path(tmp) / "transform.json"
        transform.write_text(json.dumps(_green_transform()))
        return self.module.validate_text_only(image, transform)

    def test_pass_small_text_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.zeros((256, 256, 4), np.uint8)
            img[:, :] = (0, 255, 0, 255)
            for y, x in ((40, 40), (80, 40), (120, 40)):
                img[y:y + 8, x:x + 24] = (255, 255, 255, 255)
            path = Path(tmp) / "text_only.png"
            _save_rgba(path, img)
            result = self._run(tmp, path)
            self.assertEqual(result["verdict"], "pass")

    def test_fail_low_key_coverage(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.full((256, 256, 4), (255, 255, 255, 255), np.uint8)
            img[:40, :40] = (0, 255, 0, 255)  # tiny key corner → screenshot returned
            path = Path(tmp) / "text_only.png"
            _save_rgba(path, img)
            result = self._run(tmp, path)
            self.assertEqual(result["verdict"], "fail")

    def test_fail_large_uniform_block(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.zeros((256, 256, 4), np.uint8)
            img[:, :] = (0, 255, 0, 255)
            img[78:178, 78:178] = (200, 40, 40, 255)  # solid plate left in
            path = Path(tmp) / "text_only.png"
            _save_rgba(path, img)
            result = self._run(tmp, path)
            self.assertEqual(result["verdict"], "fail")

    def test_review_large_nonuniform_block(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = np.zeros((256, 256, 4), np.uint8)
            img[:, :] = (0, 255, 0, 255)
            rng = np.random.RandomState(42)
            img[78:178, 78:178, :3] = rng.randint(0, 255, (100, 100, 3))
            img[78:178, 78:178, 3] = 255
            path = Path(tmp) / "text_only.png"
            _save_rgba(path, img)
            result = self._run(tmp, path)
            self.assertEqual(result["verdict"], "review")


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class ValidateUiOnlyGate(unittest.TestCase):
    """B5: three-state extension of validate_ui_only_reference (doc'd 95% largest-contour
    gate now implemented; plate-residue and size-band over-keying checks added)."""

    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("validate_ui_only_reference",
                                  SCRIPTS / "validate_ui_only_reference.py")

    CONTENT = {"x": 0, "y": 0, "width": 256, "height": 192}

    @staticmethod
    def _canvas():
        img = np.zeros((256, 256, 4), np.uint8)
        img[:, :] = (0, 255, 0, 255)  # key colour everywhere (content bg + padding)
        return img

    @staticmethod
    def _add_noise_block(img, x, y, w, h, seed):
        """Non-uniform block: two-colour stripes (deterministic, std > 12).

        Random noise is NOT used — a fraction of random RGB pixels matches the
        green key colour inside the HSV tolerance and shatters the block into
        fragments. Stripes of (200,60,60)/(60,60,200) stay far from key green
        while keeping a high colour std (no uniform-block flag).
        """
        block = np.zeros((h, w, 3), np.uint8)
        block[:] = (200, 60, 60)
        block[::2, :] = (60, 60, 200)
        img[y:y + h, x:x + w, :3] = block
        img[y:y + h, x:x + w, 3] = 255

    def _validate(self, tmp, image, baseline=None):
        transform = Path(tmp) / "transform.json"
        transform.write_text(json.dumps(_green_transform(dict(self.CONTENT))))
        return self.module.validate(image, transform, target_size=256,
                                    baseline=baseline)

    def test_pass_with_baseline(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._canvas()
            for x, y in ((20, 20), (200, 20), (20, 120)):
                img[y:y + 30, x:x + 30] = (255, 255, 255, 255)
            self._add_noise_block(img, 100, 60, 80, 50, 1)
            self._add_noise_block(img, 100, 130, 40, 40, 2)
            image = Path(tmp) / "ui_only.png"
            _save_rgba(image, img)

            baseline_img = self._canvas()
            baseline_img[10:160, 10:160] = (120, 160, 90, 255)  # character art — large block
            for x, y in ((20, 20), (200, 20), (20, 120)):
                baseline_img[y:y + 30, x:x + 30] = (255, 255, 255, 255)
            self._add_noise_block(baseline_img, 100, 60, 80, 50, 1)
            self._add_noise_block(baseline_img, 100, 130, 40, 40, 2)
            baseline = Path(tmp) / "text_removed.png"
            _save_rgba(baseline, baseline_img)

            result = self._validate(tmp, image, baseline=baseline)
            self.assertEqual(result["verdict"], "pass")
            self.assertTrue(result["valid"])

    def test_review_over_keying_size_band(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._canvas()
            for x, y in ((20, 20), (200, 20), (20, 120)):
                img[y:y + 30, x:x + 30] = (255, 255, 255, 255)
            self._add_noise_block(img, 100, 60, 80, 50, 1)
            self._add_noise_block(img, 100, 130, 40, 40, 2)
            image = Path(tmp) / "ui_only.png"
            _save_rgba(image, img)

            baseline_img = self._canvas()
            for x, y in ((20, 20), (200, 20), (20, 120)):
                baseline_img[y:y + 30, x:x + 30] = (255, 255, 255, 255)
            self._add_noise_block(baseline_img, 100, 60, 80, 50, 1)
            self._add_noise_block(baseline_img, 100, 130, 40, 40, 2)
            baseline_img[130:180, 150:200] = (255, 255, 255, 255)  # band element lost in 3b
            baseline = Path(tmp) / "text_removed.png"
            _save_rgba(baseline, baseline_img)

            result = self._validate(tmp, image, baseline=baseline)
            self.assertEqual(result["verdict"], "review")
            self.assertEqual(len(result["over_removal_suspects"]), 1)

    def test_review_plate_residue_uniform_block(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._canvas()
            self._add_noise_block(img, 100, 60, 80, 50, 1)
            img[120:180, 160:240] = (240, 240, 240, 255)  # uniform plate residue
            image = Path(tmp) / "ui_only.png"
            _save_rgba(image, img)
            result = self._validate(tmp, image)
            self.assertEqual(result["verdict"], "review")
            self.assertTrue(any("uniform" in reason for reason in result["review_reasons"]))

    def test_fail_largest_contour_covers_content(self):
        with tempfile.TemporaryDirectory() as tmp:
            img = self._canvas()
            self._add_noise_block(img, 2, 2, 252, 188, 3)  # covers ~96.4% of content
            image = Path(tmp) / "ui_only.png"
            _save_rgba(image, img)
            result = self._validate(tmp, image)
            self.assertEqual(result["verdict"], "fail")


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class ValidateOutputPageGate(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("validate_output_page", SCRIPTS / "validate_output_page.py")

    def _setup(self, tmp, page_img, slot_content_painter):
        root = Path(tmp)
        input_img = np.zeros((64, 64, 4), np.uint8)
        input_img[:, :] = (0, 255, 0, 255)
        input_img[12:52, 12:52] = (255, 255, 255, 255)
        input_path = root / "inputA.png"
        _save_rgba(input_path, input_img)

        page = np.zeros((512, 512, 4), np.uint8)
        page[:, :] = (0, 255, 0, 255)
        slot_content_painter(page)
        page_path = root / "output-page-0.png"
        _save_rgba(page_path, page)

        manifest = {
            "key_color": {"rgb": {"r": 0, "g": 255, "b": 0}},
            "page_size": {"width": 512, "height": 512},
            "items": [{
                "node_id": "A",
                "slot_rect": {"x": 16, "y": 16, "width": 224, "height": 224},
                "content_rect": {"x": 32, "y": 32, "width": 192, "height": 192},
                "source_path": str(input_path),
                "source_size": {"width": 64, "height": 64},
                "source_to_atlas_scale": 3.0,
            }],
        }
        manifest_path = root / "atlas-manifest-0.json"
        manifest_path.write_text(json.dumps(manifest))
        return page_path, manifest_path

    def test_pass_clean_page(self):
        with tempfile.TemporaryDirectory() as tmp:
            def paint(page):
                page[60:120, 60:120] = (255, 255, 255, 255)
                page[140:180, 140:180] = (255, 200, 100, 255)
            page, manifest = self._setup(tmp, None, paint)
            result = self.module.validate_output_page(page, manifest)
            self.assertEqual(result["verdict"], "pass")
            self.assertEqual(result["slots"][0]["verdict"], "pass")

    def test_fail_plate_not_removed(self):
        with tempfile.TemporaryDirectory() as tmp:
            def paint(page):
                page[38:218, 38:218] = (220, 180, 120, 255)  # uniform plate fills the slot
            page, manifest = self._setup(tmp, None, paint)
            result = self.module.validate_output_page(page, manifest)
            self.assertEqual(result["verdict"], "fail")
            self.assertTrue(any("plate NOT removed" in reason for reason in result["reasons"]))

    def test_review_empty_slot(self):
        with tempfile.TemporaryDirectory() as tmp:
            page, manifest = self._setup(tmp, None, lambda page: None)
            result = self.module.validate_output_page(page, manifest)
            self.assertEqual(result["verdict"], "review")


@unittest.skipUnless(HAS_CV2, "opencv-python / numpy are required for this test")
class FindMissingComponentsGate(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.module = _load_module("find_missing_components",
                                  SCRIPTS / "find_missing_components.py")

    @staticmethod
    def _manifest(tmp, boxes, name="components.json"):
        path = Path(tmp) / name
        path.write_text(json.dumps({
            "content_rect": {"x": 0, "y": 0, "width": 256, "height": 192},
            "components": [
                {"node_id": f"component-{i + 1:03d}",
                 "root_box": {"x": x, "y": y, "width": w, "height": h}}
                for i, (x, y, w, h) in enumerate(boxes)
            ],
        }))
        return path

    def test_pass_identical_extraction(self):
        with tempfile.TemporaryDirectory() as tmp:
            boxes = [(100, 100, 60, 60), (200, 100, 60, 60), (100, 200, 60, 60)]
            result = self.module.find_missing(self._manifest(tmp, boxes, "baseline.json"),
                                             self._manifest(tmp, boxes, "downgraded.json"))
            self.assertEqual(result["verdict"], "pass")

    def test_review_new_candidate_and_explanations(self):
        with tempfile.TemporaryDirectory() as tmp:
            baseline = self._manifest(tmp, [(100, 100, 60, 60), (200, 100, 60, 60)],
                                      "baseline.json")
            downgraded = self._manifest(tmp, [
                (100, 100, 60, 60),        # identical → explained
                (200, 100, 60, 60),        # identical → explained
                (110, 110, 30, 20),        # contained in baseline → explained
                (10, 10, 12, 12),          # below min candidate side → noise
                (300, 300, 30, 30),        # genuinely missed element
            ], "downgraded.json")
            result = self.module.find_missing(baseline, downgraded)
            self.assertEqual(result["verdict"], "review")
            self.assertEqual(len(result["candidate_boxes"]), 1)
            self.assertEqual(result["candidate_boxes"][0]["root_box"],
                             {"x": 300, "y": 300, "width": 30, "height": 30})

    def test_review_tiny_baseline_box(self):
        with tempfile.TemporaryDirectory() as tmp:
            baseline = self._manifest(tmp, [(100, 100, 10, 10)], "baseline.json")
            downgraded = self._manifest(tmp, [(100, 100, 10, 10)], "downgraded.json")
            result = self.module.find_missing(baseline, downgraded)
            self.assertEqual(result["verdict"], "review")
            self.assertEqual(len(result["tiny_boxes"]), 1)

    def test_review_oversized_overmerge_suspect(self):
        with tempfile.TemporaryDirectory() as tmp:
            # 170x120 = 20400 px = 41.5% of the 256x192 content area, not full-frame
            baseline = self._manifest(tmp, [(10, 10, 170, 120)], "baseline.json")
            downgraded = self._manifest(tmp, [(10, 10, 170, 120)], "downgraded.json")
            result = self.module.find_missing(baseline, downgraded)
            self.assertEqual(result["verdict"], "review")
            self.assertEqual(len(result["oversized_boxes"]), 1)

    def test_full_frame_container_not_flagged_oversized(self):
        with tempfile.TemporaryDirectory() as tmp:
            baseline = self._manifest(tmp, [(0, 0, 256, 192)], "baseline.json")
            downgraded = self._manifest(tmp, [(0, 0, 256, 192)], "downgraded.json")
            result = self.module.find_missing(baseline, downgraded)
            self.assertEqual(result["verdict"], "pass")


if __name__ == "__main__":
    unittest.main()
