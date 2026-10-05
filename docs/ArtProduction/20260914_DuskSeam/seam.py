"""Offset -> generative centre repair -> bounded composite -> inverse offset.
No mirroring; keep the source outside the repaired join.
"""
from pathlib import Path
import sys,json,hashlib
from PIL import Image,ImageChops
import numpy as np
root=Path(__file__).resolve().parent
source=Image.open(root/'raw/DuskCity_source.png').convert('RGB')
w,h=source.size
shift=w//2
offset=ImageChops.offset(source,shift,0)
if sys.argv[1]=='prepare':
    offset.save(root/'raw/DuskCity_offset.png')
    print(source.size)
else:
    edited=Image.open(root/'raw/DuskCity_offset_repaired.png').convert('RGB')
    edited=edited.resize((w,h),Image.Resampling.LANCZOS)
    # Use only the central 44%; feather its outer 8% into untouched original.
    x=np.arange(w,dtype=np.float32)
    d=np.abs(x-w/2)
    alpha=np.clip((w*.22-d)/(w*.08),0,1)
    alpha=alpha*alpha*(3-2*alpha)
    a=alpha[None,:,None]
    combined=np.rint(np.asarray(offset)*(1-a)+np.asarray(edited)*a).astype('uint8')
    result=ImageChops.offset(Image.fromarray(combined),-shift,0)
    pixels=np.array(result)
    # The two boundary columns represent the same periodic sample.
    edge=np.rint((pixels[:,0].astype(float)+pixels[:,-1])/2).astype('uint8')
    pixels[:,0]=edge;pixels[:,-1]=edge
    result=Image.fromarray(pixels)
    (root/'ready').mkdir(exist_ok=True);(root/'previews').mkdir(exist_ok=True)
    result.save(root/'ready/BG_W01_Far_DuskCity_Loop_v02.png')
    triple=Image.new('RGB',(w*3,h))
    for i in range(3):triple.paste(result,(w*i,0))
    triple.save(root/'previews/triple.png')
    triple.resize((2400,round(h*2400/(w*3))),Image.Resampling.LANCZOS).save(root/'previews/triple_small.jpg')
    ImageChops.offset(result,shift,0).save(root/'previews/join_center.png')
    report={'size':[w,h],'mirror_used':False,'source_sha256':hashlib.sha256((root/'raw/DuskCity_source.png').read_bytes()).hexdigest(),
            'exact_edge_max_difference':int(np.abs(pixels[:,0].astype(int)-pixels[:,-1]).max()),
            'mean_interior_adjacent_difference':float(np.abs(pixels[:,1:].astype(float)-pixels[:,:-1]).mean()),
            'repair_width_fraction':.44,'outer_feather_fraction':.08}
    (root/'report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(report)
