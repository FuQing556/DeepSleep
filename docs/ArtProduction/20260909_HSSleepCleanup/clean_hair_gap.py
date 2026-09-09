"""Remove only the inspected checkerboard enclosed by HS's ahoge. No redraw."""
from pathlib import Path
import sys
import json
import hashlib
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'Tools'))
from sprite_matte import neutral_edge_dematte, report, preview

source = ROOT / 'raw/SPR_HA_DownedSleep_v01.png'
im = Image.open(source).convert('RGBA')
rgb = np.asarray(im)[:, :, :3].astype(np.int16)
roi = np.zeros(rgb.shape[:2], dtype=bool)
roi[78:200, 345:490] = True
eligible = roi & (rgb.min(2) >= 205) & ((rgb.max(2)-rgb.min(2)) <= 25)
flood = Image.fromarray(np.where(eligible, 0, 255).astype(np.uint8)).copy()
ImageDraw.floodfill(flood, (420, 120), 128, thresh=0)
mask = np.asarray(flood) == 128
out = neutral_edge_dematte(im, mask)
out[mask] = 0
result = Image.fromarray(out)
ready = ROOT / 'ready'; ready.mkdir(exist_ok=True)
previews = ROOT / 'previews'; previews.mkdir(exist_ok=True)
result.save(ready / source.name)
for name, color in [('white','#ffffff'),('dark','#171622'),('sky','#294b85')]:
    preview(result, color).save(previews / (name+'.png'))
    preview(result.crop((330,60,510,220)),color,650).save(previews / (name+'_gap.png'))
assert np.array_equal(np.asarray(im)[220:], out[220:]), 'Unrelated body pixels changed'
assert out[120,420,3] == 0
(ROOT/'report.json').write_text(json.dumps({'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
    'roi':[345,78,490,200], 'seed':[420,120], 'removed_pixels':int(mask.sum()),
    'changed_pixels':int(np.any(np.asarray(im)!=out,axis=2).sum()),**report(result)},indent=2),encoding='utf8')
print((ROOT/'report.json').read_text())
