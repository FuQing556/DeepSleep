"""Read-only source inspection: composite two original tiles without changing them."""
from pathlib import Path
import hashlib
from PIL import Image
import numpy as np

root = Path(__file__).resolve().parent
source = root / 'raw/TEX_CL_RainMist_v01.png'
rain = Image.open(source).convert('RGBA')
w, h = rain.size
out = root / 'previews'
out.mkdir(exist_ok=True)
sheet = Image.new('RGB', (960, 640))
for i, color in enumerate(((245,245,245,255),(12,18,28,255),(90,140,190,255))):
    pair = Image.new('RGBA', (w, h * 2), color)
    for y in (0, h):
        pair.alpha_composite(rain, (0, y))
    sheet.paste(pair.resize((320,640)).convert('RGB'), (i*320,0))
    if i == 1:
        pair.crop((0,h-120,w,h+120)).convert('RGB').save(out / 'RainMist_JoinCloseup.png')
sheet.save(out / 'RainMist_VerticalPair.png')
pixels = np.asarray(rain, dtype=np.float32)
premult = pixels[:, :, :3] * pixels[:, :, 3:4] / 255
edge = np.concatenate((premult, pixels[:, :, 3:4]), axis=2)
seam = np.abs(edge[0] - edge[-1]).mean()
adjacent = np.abs(edge[1:] - edge[:-1]).mean()
print({'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
       'size': rain.size, 'alpha_min': int(pixels[:,:,3].min()),
       'alpha_max': int(pixels[:,:,3].max()),
       'boundary_mean_delta': float(seam), 'internal_mean_delta': float(adjacent)})
