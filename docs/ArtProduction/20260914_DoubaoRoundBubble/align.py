"""Only canvas/scale alignment; preserve raw and matte candidates."""
from pathlib import Path
import json
from PIL import Image
root = Path(__file__).resolve().parent
out = root / 'production'
out.mkdir(exist_ok=True)
record = []
for src in (root / 'ready').glob('*.png'):
    im = Image.open(src).convert('RGBA')
    box = im.getchannel('A').getbbox()
    crop = im.crop(box)
    if src.name.startswith('SPR_'):
        # Original mother image visible height=1084; feet y=1213 on 1280 canvas.
        ratio = 1084 / crop.height
        crop = crop.resize((round(crop.width*ratio), 1084), Image.Resampling.LANCZOS)
        canvas = Image.new('RGBA', (1280,1280))
        offset = (636-crop.width//2,1213-crop.height)
    else:
        # Tight square plus symmetric 8px padding, so diameter matches collider.
        side = max(crop.size)+16
        canvas = Image.new('RGBA',(side,side))
        offset = ((side-crop.width)//2,(side-crop.height)//2)
    canvas.paste(crop,offset)
    canvas.save(out/src.name)
    record.append({'file':src.name,'source_bbox':box,'canvas':canvas.size,'offset':offset})
(root/'alignment.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
