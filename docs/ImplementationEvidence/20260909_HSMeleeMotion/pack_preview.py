"""Pack Unity's deterministically stepped camera frames; never alters source sprites."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[3]
source = root / "Library/HSMeleeMotionPreview"
output = Path(__file__).resolve().parent
labels = ["Down", "Up", "Sweep"]
frames = []
sheet = Image.new("RGB", (2400, 840), "#151a27")
for attack, label in enumerate(labels):
    for column, index in enumerate([19, 25, 31, 38, 42]):
        still = Image.open(source / f"{attack}_{index:03}.png").convert("RGB")
        still.thumbnail((480, 270))
        sheet.paste(still, (column * 480, attack * 280 + 10))
        ImageDraw.Draw(sheet).text((column * 480 + 6, attack * 280 + 12),
                                  f"{label}  {(index - 11) / 60:.2f}s", fill="white")
    for index in range(72):
        frame = Image.open(source / f"{attack}_{index:03}.png").convert("RGB")
        frame.thumbnail((800, 450))
        ImageDraw.Draw(frame).text((12, 12), f"HS / {label} / Unity 60 Hz capture", fill="white")
        frames.append(frame.quantize(colors=192))
sheet.save(output / "motion_contact.jpg", quality=94)
frames[0].save(output / "hs_melee_motion.gif", save_all=True, append_images=frames[1:],
               duration=([20, 10, 20] * 72), loop=0, disposal=2, optimize=False)
print(f"Wrote {len(frames)} frames and contact sheet to {output}")
