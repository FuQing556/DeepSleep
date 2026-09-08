"""Keep the opaque crystal/core and reconstruct red emission over real alpha."""
from pathlib import Path
import hashlib
import json
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent/'Tools'))
from sprite_matte import report


def extract_crystal_effect(image):
    rgb = np.asarray(image.convert('RGB'), dtype=np.float32)/255
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    signal = np.clip(r-np.maximum(g, b), 0, 1)
    # Crystal surfaces are dark or strongly red; the checker itself is neutral.
    core = ((signal > 0.67) | (rgb.max(2)<0.35)).astype(np.uint8)*255
    mask = Image.fromarray(core).filter(ImageFilter.MaxFilter(9))
    # Preserve white cores and spark centers enclosed by red outlines.
    padded = np.pad(np.asarray(mask), 1)
    flood = Image.fromarray(padded).copy()
    ImageDraw.floodfill(flood, (0, 0), 128)
    closed = np.where(np.asarray(flood)[1:-1, 1:-1] == 128, 0, 255).astype(np.uint8)
    solid = Image.fromarray(closed).filter(ImageFilter.MinFilter(9))
    solid = solid.filter(ImageFilter.GaussianBlur(0.75))
    coverage = np.asarray(solid, dtype=np.float32)/255
    # A neutral checker contributes equally to RGB. Subtracting that neutral
    # component isolates its red halo. This reconstructs the soft emission;
    # it does not claim to recover the original, unavailable alpha losslessly.
    halo = np.clip((signal-0.025)/0.975, 0, 1)
    alpha = coverage+(1-coverage)*halo
    premult = rgb*coverage[:, :, None]
    premult[:, :, 0] += (1-coverage)*halo
    straight = premult/np.maximum(alpha[:, :, None], 1/255)
    result = np.dstack((straight, alpha))
    result = np.round(np.clip(result, 0, 1)*255).astype(np.uint8)
    result[result[:, :, 3]<4] = 0
    return Image.fromarray(result)


def main():
    out = ROOT/'ready'
    out.mkdir(exist_ok=True)
    files = ['HA_QuantumSword_v01.png', 'HA_QuantumSlashDown_v01.png',
             'HA_QuantumSlashUp_v01.png', 'HA_QuantumSlashSweep_v01.png']
    rows = []
    results = []
    for name in files:
        source = ROOT/'raw'/name
        original = Image.open(source)
        has_alpha = original.mode=='RGBA' and original.getchannel('A').getextrema()[0]==0
        image = original.copy() if has_alpha else extract_crystal_effect(original)
        image.save(out/name)
        results.append(dict(file=name, source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                            method='preserve_alpha' if has_alpha else 'crystal_core_and_red_emission', **report(image)))
        thumb = image.copy()
        thumb.thumbnail((650, 500), Image.Resampling.LANCZOS)
        row = Image.new('RGBA', (1300, 530))
        for i, color in enumerate(['#151a26', '#e8e8f0']):
            cell = Image.new('RGBA', (650, 530), color)
            cell.alpha_composite(thumb, ((650-thumb.width)//2, 25+(500-thumb.height)//2))
            ImageDraw.Draw(cell).text((12, 8), name, fill='#888888')
            row.paste(cell, (650*i, 0))
        rows.append(row)
    sheet = Image.new('RGBA', (1300, 2120))
    for i, row in enumerate(rows):
        sheet.paste(row, (0, 530*i))
    sheet.convert('RGB').save(ROOT/'previews/REVIEW_Effects.jpg', quality=96)
    (out/'effects_report.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    for r in results:
        print(r['file'], r['size'], r['clear_corners'])


if __name__=='__main__':
    main()
