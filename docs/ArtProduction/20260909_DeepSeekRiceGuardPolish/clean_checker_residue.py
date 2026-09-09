"""Remove neutral checker residue after connected-background matting.

This recipe is specific to the two blue/white Rice Guard VFX generated in this
batch. It does not alter the source files and must not be reused for characters.
"""
from pathlib import Path

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parent


def clean(name: str, clear_center_radius: int = 0) -> None:
    path = ROOT / "ready" / name
    image = Image.open(ROOT / 'raw' / (Path(name).stem + '_generated.png')).convert("RGBA")
    rgba = np.array(image)
    rgb = rgba[:, :, :3].astype(np.int16)
    low = rgb.min(axis=2)
    high = rgb.max(axis=2)
    chroma = high - low

    # The baked checker is neutral mid-gray. Genuine rice highlights are much
    # brighter; DS blue/cyan glow has enough chroma to survive this mask.
    # The generator baked two warm-neutral checker values underneath the
    # anti-aliased glow.  Their blended edge is not perfectly neutral, so the
    # earlier <= 13 chroma cut left a dirty gray fringe on a sky background.
    # Genuine DS energy is strongly blue/cyan; extending the neutral cut to 30
    # removes that fringe without touching the saturated glow. Very bright rice
    # and white linework are protected by the high <= 228 ceiling.
    residue = (low >= 45) & (high <= 228) & (chroma <= 30)
    rgba[residue] = 0

    # Feather only the narrow near-neutral transition instead of eroding the
    # saturated blue glow or opaque bowl/rice artwork.
    transition = (
        (rgba[:, :, 3] > 0)
        & (low >= 45)
        & (high <= 238)
        & (chroma > 30)
        & (chroma < 55)
    )
    factor = np.clip((chroma - 30) / 25.0, 0.0, 1.0)
    rgba[transition, 3] = np.minimum(
        rgba[transition, 3],
        np.round(255 * factor[transition]).astype(np.uint8),
    )

    if clear_center_radius > 0:
        yy, xx = np.ogrid[:image.height, :image.width]
        cx = (image.width - 1) * 0.5
        cy = (image.height - 1) * 0.5
        center = (xx - cx) ** 2 + (yy - cy) ** 2 < clear_center_radius ** 2
        rgba[center] = 0

    # Rebuild from the immutable source, separating opaque ceramic from glow.
    from PIL import ImageFilter
    source = np.array(image).astype(np.float32)
    rgb = source[:,:,:3]
    low, high = rgb.min(2), rgb.max(2)
    chroma = high-low
    blue = np.maximum(rgb[:,:,2]-rgb[:,:,0],0)
    alpha = np.clip((blue-25)/90,0,1)
    white = low > 239
    dark = (high < 120) & (chroma > 9)
    if 'Broken' in name:
        gold = (rgb[:,:,0] > rgb[:,:,2]+25) & (rgb[:,:,0] > 105)
        protected = np.array(Image.fromarray((dark*255).astype('uint8')).filter(ImageFilter.MaxFilter(9))) > 0
        opaque = white | gold | (protected & (high < 180))
    else:
        opaque = white | dark
    from PIL import ImageDraw
    # Close narrow outline gaps, then protect enclosed material interiors.
    core = Image.fromarray(((alpha > 0.72) | opaque).astype('uint8')*255)
    core = core.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))
    filled = core.copy()
    ImageDraw.floodfill(filled,(0,0),128)
    interior = np.array(filled) != 128
    alpha[interior] = 1
    # Start from connected background removal; only dematte its boundary band.
    # Interior ceramic/rice RGB and alpha remain protected.
    import sys
    sys.path.insert(0,str(ROOT.parent/'Tools'))
    from sprite_matte import connected_neutral
    base = connected_neutral(image, minimum=75, maximum=248, chroma=14,
        seeds=[(627,627)] if clear_center_radius else [], edge_dematte=False)
    base_rgba = np.array(base)
    base_alpha = base_rgba[:,:,3].astype(np.float32)/255
    eroded = np.array(Image.fromarray(base_rgba[:,:,3]).filter(ImageFilter.MinFilter(31))) > 0
    boundary = (base_alpha > 0) & ~eroded
    alpha = base_alpha.copy()
    weak = boundary & (low > 65) & (high < 239) & (blue > 0)
    alpha[weak] *= np.clip((blue[weak]-28)/65,0,1)
    neutral = boundary & (low > 65) & (high < 239) & (chroma < 28)
    alpha[neutral] = 0
    edge = (alpha > 0) & (alpha < 1)
    # Approximate decontamination of gray checker mixed into blue glow.
    rgb[edge] = np.clip((rgb[edge]-low[edge,None]*(1-alpha[edge,None]))/alpha[edge,None],0,255)
    rgba = np.dstack((rgb,alpha*255)).clip(0,255).astype('uint8')
    rgba[rgba[:, :, 3] == 0, :3] = 0
    Image.fromarray(rgba, "RGBA").save(path)
    preview_dir = ROOT / 'previews' / 'final_matte'
    preview_dir.mkdir(parents=True,exist_ok=True)
    for label, color in [('sky',(49,83,128)),('dark',(24,24,28)),('white',(255,255,255))]:
        bg = Image.new('RGBA',image.size,color+(255,))
        bg.alpha_composite(Image.fromarray(rgba))
        bg.thumbnail((700,700))
        bg.convert('RGB').save(preview_dir/(Path(name).stem+'_'+label+'.png'))


clean("VFX_DS_RiceGuardCircle_v01.png", clear_center_radius=305)
clean("SPR_DS_RiceGuardBowlBroken_v01.png")
