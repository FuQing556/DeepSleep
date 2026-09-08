"""Reproduce cutouts from untouched sources and explicitly inspected background gaps."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'Tools'))
from sprite_matte import connected_neutral, neutral_edge_dematte, green_screen, report


def extract(image, gaps):
    # Start with the existing, conservative perimeter flood. Gap floods are
    # bounded to reviewed rectangles so a seed cannot reach the face or apron.
    outer = connected_neutral(image, minimum=100, maximum=255, chroma=12)
    background = np.asarray(outer.getchannel('A')) == 0
    for entry in gaps:
        box, seed = entry[:2]
        maximum = entry[2] if len(entry) > 2 else 255
        crop = image.crop(box).convert('RGB')
        rgb = np.asarray(crop).astype(np.int16)
        eligible = (rgb.min(2) >= 85) & (rgb.max(2) <= maximum) & (rgb.max(2)-rgb.min(2) <= 18)
        x, y = seed[0]-box[0], seed[1]-box[1]
        if not eligible[y, x]:
            raise ValueError(f'Not an inspected background pixel: {seed}')
        flood = Image.fromarray(np.where(eligible, 0, 255).astype(np.uint8)).copy()
        ImageDraw.floodfill(flood, (x, y), 128)
        background[box[1]:box[3], box[0]:box[2]] |= np.asarray(flood) == 128
    rgba = neutral_edge_dematte(image, background)
    rgba[background] = 0
    return Image.fromarray(rgba)


ASSETS = [
    ('HA_MeleeIdle_v01.png', [
        ((775, 30, 941, 164), (855, 80), 215),
        ((265, 288, 455, 393), (350, 340)),
    ]),
    ('HA_MeleeDownCommand_v01.png', [
        ((393, 63, 491, 176), (444, 112)),
        ((963, 211, 1135, 359), (1051, 270)),
        ((243, 355, 580, 597), (350, 515)),
        ((383, 373, 459, 448), (419, 407)),
        ((434, 450, 529, 565), (475, 525)),
        ((972, 682, 1111, 809), (1060, 769)),
        ((1079, 652, 1155, 820), (1120, 765)),
        ((430, 428, 490, 488), (452, 448)),
    ]),
    ('HA_MeleeUpCommand_v02.png', [
        ((725, 36, 931, 189), (820, 80)),
        ((245, 603, 520, 862), (350, 820)),
        ((337, 605, 409, 655), (375, 634)),
        ((480, 636, 525, 711), (505, 665)),
        ((529, 650, 578, 716), (550, 685)),
        ((961, 596, 1053, 681), (986, 630)),
    ]),
    ('HA_MeleeSweepCommand_v03.png', None),
]


def main():
    out = ROOT / 'ready'
    out.mkdir(exist_ok=True)
    results = []
    cells = []
    for name, gaps in ASSETS:
        source = ROOT / 'raw' / name
        original = Image.open(source)
        image = green_screen(original, backdrop_tolerance=45) if gaps is None else extract(original, gaps)
        image.save(out / name)
        stats = report(image)
        results.append(dict(file=name, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                            method='green_screen' if gaps is None else 'bounded_neutral_flood',
                            gap_rectangles=gaps, **stats))
        for label, color in [('dark', '#181c25'), ('light', '#eeeaf0')]:
            thumb = image.copy()
            thumb.thumbnail((500, 500), Image.Resampling.LANCZOS)
            bg = Image.new('RGBA', (500, 530), color)
            bg.alpha_composite(thumb, ((500-thumb.width)//2, 25))
            ImageDraw.Draw(bg).text((12, 8), name.removesuffix('.png'), fill='#aaaaaa' if label=='dark' else '#333333')
            bg.convert('RGB').save(ROOT / 'previews' / f'REVIEW_{name[:-4]}_{label}.jpg', quality=96)
            if label == 'dark':
                cells.append(bg)
    sheet = Image.new('RGBA', (1000, 1060))
    for i, cell in enumerate(cells):
        sheet.paste(cell, ((i%2)*500, (i//2)*530))
    sheet.convert('RGB').save(ROOT / 'previews' / 'REVIEW_Characters.jpg', quality=96)
    (out/'character_report.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(results, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
