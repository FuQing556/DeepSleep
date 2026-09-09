"""Batch-specific matte: opaque bowl and reconstructed blue glow."""
from pathlib import Path
import hashlib
import json
import sys
import numpy as np
from PIL import Image, ImageDraw

BATCH = Path(__file__).resolve().parent
sys.path.insert(0, str(BATCH.parent / 'Tools'))
from sprite_matte import connected_neutral, preview, report
SOURCE = BATCH / 'raw/CANDIDATE_SPR_DS_RiceGuardBowl_v01_raw.png'
OUTPUT = BATCH / 'ready/CANDIDATE_SPR_DS_RiceGuardBowl_v02.png'

def main():
    source = Image.open(SOURCE).convert('RGBA')
    rgb = np.asarray(source, dtype=np.float32)[:, :, :3]
    # Locate the opaque bowl using its closed blue outline.
    located = connected_neutral(source, minimum=85, maximum=248, chroma=120)
    binary = Image.fromarray(np.where(np.asarray(located)[:,:,3] > 0, 0, 255).astype('uint8')).copy()
    ImageDraw.floodfill(binary, (627, 700), 128, thresh=0)
    bowl = np.asarray(binary) == 128
    # Neutral checker contributes almost no blue-minus-red chroma.
    # Original glow alpha is unavailable; this is an approximate reconstruction.
    blue = np.maximum(rgb[:,:,2] - rgb[:,:,0] - 8, 0)
    glow_alpha = np.clip(blue / 160, 0, 1)
    white_core = np.clip((rgb.min(2) - 234) / 20, 0, 1)
    alpha = np.maximum(glow_alpha, white_core)
    # Estimate cyan emission hue independently of the checker luminance. This
    # avoids dark gray halos on bright backgrounds when unmixing is ambiguous.
    hue_ratio = (rgb[:,:,1] - rgb[:,:,0]) / np.maximum(rgb[:,:,2]-rgb[:,:,0], 1)
    color = np.stack((np.full_like(blue,30), np.clip(hue_ratio*255,140,240), np.full_like(blue,255)),axis=2)
    color = color * (1-white_core[:,:,None]) + rgb * white_core[:,:,None]
    color[bowl] = rgb[bowl]
    alpha[bowl] = 1
    out = np.dstack((color, alpha*255)).clip(0,255).round().astype('uint8')
    out[out[:,:,3] <= 3] = 0
    result = Image.fromarray(out)
    result.save(OUTPUT)
    for name, bg in [('white',(245,245,245,255)),('dark',(26,31,43,255)),('sky',(65,139,215,255))]:
        preview(result,bg).save(BATCH / f'previews/v02_{name}.png')
    data = report(result)
    data.update(source_sha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),output=str(OUTPUT),
                method='Bounded opaque bowl; exterior blue chroma emission.',
                limitation='Exterior glow approximately reconstructed, not original alpha.',
                unity_imported=False,user_approved=False)
    (BATCH/'previews/v02_report.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(data,ensure_ascii=False))

if __name__ == '__main__':
    main()
