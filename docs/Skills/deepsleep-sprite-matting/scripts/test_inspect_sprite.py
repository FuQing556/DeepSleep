"""Small behavioral checks for inspection; no production assets are changed."""
import hashlib
import tempfile
import unittest
from pathlib import Path

from PIL import Image

from inspect_sprite import inspect, save_evidence


class SpriteInspectionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def source(self, name, image):
        path = self.root / name
        image.save(path)
        return path

    def test_fake_transparency_rejected(self):
        for mode in ['RGB', 'RGBA']:
            path = self.source(mode + '.png', Image.new(mode, (8, 8), 'gray'))
            _, result = inspect(path, require_alpha=True)
            self.assertTrue(result['automatic_failures'])

    def test_all_transparent_rejected(self):
        path = self.source('empty.png', Image.new('RGBA', (8, 8), (0, 0, 0, 0)))
        self.assertTrue(inspect(path, require_alpha=True)[1]['automatic_failures'])

    def test_semtransparent_vfx_without_opaque_pixels_allowed(self):
        glow = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
        glow.putpixel((4, 4), (255, 0, 0, 128))
        path = self.source('glow.png', glow)
        _, result = inspect(path, require_alpha=True, clear=[(0, 0)])
        self.assertFalse(result['automatic_failures'])
        self.assertEqual(result['opaque_pixels'], 0)

    def test_evidence_preserves_input_and_checks_white_foreground(self):
        figure = Image.new('RGBA', (8, 8), (0, 0, 0, 0))
        figure.putpixel((4, 4), (255, 255, 255, 255))
        path = self.source('white_cloth.png', figure)
        before = hashlib.sha256(path.read_bytes()).hexdigest()
        rgba, result = inspect(path, True, keep=[(4, 4)], clear=[(0, 0)])
        self.assertFalse(result['automatic_failures'])
        save_evidence(rgba, result, self.root / 'audit')
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), before)
        self.assertEqual(len(list((self.root / 'audit').iterdir())), 4)
        with self.assertRaises(FileExistsError):
            save_evidence(rgba, result, self.root / 'audit')

    def test_bad_points_report_or_raise(self):
        path = self.source('blank.png', Image.new('RGBA', (8, 8), (0, 0, 0, 0)))
        self.assertTrue(inspect(path, keep=[(4, 4)])[1]['automatic_failures'])
        with self.assertRaises(ValueError):
            inspect(path, clear=[(9, 9)])


if __name__ == '__main__':
    unittest.main()
