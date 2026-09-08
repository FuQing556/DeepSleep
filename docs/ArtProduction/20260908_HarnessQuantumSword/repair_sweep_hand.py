"""Locally replace the malformed rear hand without regenerating the character."""
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


ROOT = Path(__file__).resolve().parent
TARGET = ROOT / "cutouts/HA_MeleeSweepCommand_v01.png"
DONOR = Path(r"D:\Unity Work\DeepSleep_Unity6\Assets\_Project\Art\Characters\Harness\SPR_HA_IdleFly_v03.png")
OUTPUT = ROOT / "cutouts/HA_MeleeSweepCommand_v02.png"


target = Image.open(TARGET).convert("RGBA")
target_pixels = np.array(target)

# The old rear hand is over dark hair. Replace only skin and its immediate red
# outline with adjacent hair pixels before placing the corrected hand.
x0, y0, x1, y1 = 500, 500, 615, 625
roi = target_pixels[y0:y1, x0:x1]
rgb = roi[:, :, :3].astype(np.int16)
skin = (
    (roi[:, :, 3] > 0)
    & (rgb[:, :, 0] > 145)
    & (rgb[:, :, 0] > rgb[:, :, 1] + 12)
    & (rgb[:, :, 1] > 75)
    & (rgb[:, :, 2] > 65)
)
skin = np.asarray(Image.fromarray(skin.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(7))) > 0
source_hair = target_pixels[y0:y1, x0 - 85:x1 - 85]
roi[skin] = source_hair[skin]
target_pixels[y0:y1, x0:x1] = roi
target = Image.fromarray(target_pixels)

# Reuse the established pointing hand from the accepted HS idle sprite. Mirroring
# turns it into the correct rear left hand for the horizontal follow-through.
donor = Image.open(DONOR).convert("RGBA").crop((815, 580, 955, 715))
donor_pixels = np.array(donor)
donor_rgb = donor_pixels[:, :, :3].astype(np.int16)
hand = (
    (donor_pixels[:, :, 3] > 0)
    & (donor_rgb[:, :, 0] > 150)
    & (donor_rgb[:, :, 0] > donor_rgb[:, :, 1] + 7)
    & (donor_rgb[:, :, 1] > 80)
    & (donor_rgb[:, :, 2] > 70)
)
hand = np.asarray(Image.fromarray(hand.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(5))) > 0
donor_pixels[~hand] = 0
donor = Image.fromarray(donor_pixels)
bbox = donor.getchannel("A").getbbox()
if bbox is None:
    raise RuntimeError("Could not isolate donor hand")
donor = donor.crop(bbox).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
donor = donor.resize((int(donor.width * 0.88), int(donor.height * 0.88)), Image.Resampling.LANCZOS)
donor = donor.rotate(5, Image.Resampling.BICUBIC, expand=True)

target.alpha_composite(donor, (505, 515))
target.save(OUTPUT)
print(f"saved {OUTPUT} donor={donor.size}")
