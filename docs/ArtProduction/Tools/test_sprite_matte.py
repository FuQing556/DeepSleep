import unittest
import numpy as np
from PIL import Image, ImageDraw
from sprite_matte import connected_neutral, green_screen, black_emission


class MatteTests(unittest.TestCase):
    def test_neutral_border_removed_but_enclosed_white_preserved(self):
        im = Image.new('RGB', (20, 20), (150, 150, 150))
        draw = ImageDraw.Draw(im)
        draw.rectangle((5, 5, 14, 14), fill=(10, 10, 10))
        draw.rectangle((7, 7, 12, 12), fill=(230, 230, 230))
        result = connected_neutral(im)
        self.assertEqual(result.getpixel((0, 0))[3], 0)
        self.assertEqual(result.getpixel((9, 9)), (230, 230, 230, 255))
        self.assertEqual(im.getpixel((0, 0)), (150, 150, 150))

    def test_inspected_gap_seed_opens_only_selected_region(self):
        im = Image.new('RGB', (20, 20), (150, 150, 150))
        ImageDraw.Draw(im).rectangle((5, 5, 14, 14), outline=(10, 10, 10), width=2)
        result = connected_neutral(im, seeds=[(9, 9)])
        self.assertEqual(result.getpixel((9, 9))[3], 0)
        self.assertEqual(result.getpixel((5, 5))[3], 255)

    def test_invalid_seed_rejected(self):
        with self.assertRaises(ValueError):
            connected_neutral(Image.new('RGB', (2, 2), 'black'), seeds=[(0, 0)])

    def test_green_does_not_recolor_skin_white_or_black(self):
        colors = [(255, 190, 150), (255, 255, 255), (18, 15, 20)]
        for color in colors:
            result = green_screen(Image.new('RGB', (2, 2), color))
            self.assertEqual(result.getpixel((0, 0)), (*color, 255))

    def test_imperfect_green_screen_is_transparent(self):
        result = green_screen(Image.new('RGB', (2, 2), (17, 239, 20)))
        self.assertEqual(result.getpixel((0, 0)), (0, 0, 0, 0))

    def test_emission_reconstructs_on_black(self):
        color = np.array([180, 25, 12])
        result = black_emission(Image.new('RGB', (2, 2), tuple(color)))
        pixel = np.array(result.getpixel((0, 0)), dtype=float)
        self.assertLessEqual(np.max(np.abs(pixel[:3]*pixel[3]/255-color)), 1)
        self.assertEqual(black_emission(Image.new('RGB', (2, 2))).getpixel((0, 0))[3], 0)


if __name__ == '__main__':
    unittest.main()
