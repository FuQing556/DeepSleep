"""Green-screen batch. Original images are immutable; no resizing of outputs."""
from pathlib import Path
import sys
import json
import hashlib
from PIL import Image

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'Tools'))
from sprite_matte import green_screen, preview, report

reports = []
for stem in ['VFX_DS_RiceGuardCircle_v02', 'SPR_DS_RiceGuardBowlBroken_v02']:
    source = ROOT / 'raw' / (stem + '_green.png')
    output = ROOT / 'ready' / (stem + '.png')
    result = green_screen(Image.open(source), cutoff=3, backdrop_tolerance=26)
    result.save(output)
    for label, color in [('white', (255,255,255,255)), ('dark',(24,24,28,255)), ('sky',(49,83,128,255))]:
        preview(result, color).save(ROOT / 'previews' / (stem+'_'+label+'.png'))
    reports.append(dict(source=str(source), source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), output=str(output), **report(result)))
(ROOT/'green_matte_report.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print(json.dumps(reports,indent=2))
