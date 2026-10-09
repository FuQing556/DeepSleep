"""Presentation only: composite existing sprites at equal canvas scale, no pixel edits."""
from pathlib import Path
from PIL import Image, ImageDraw

batch = Path(__file__).resolve().parent
project = batch.parents[2]
sources = [
    ("Approved idle", project / "Assets/_Project/Art/Characters/Claude/SPR_CL_IdleHover.png"),
    ("Old movement", batch / "raw/ClaudeMove_original.png"),
    ("New movement", batch / "ready/SPR_CL_Move.png"),
]
sheet = Image.new("RGB", (960, 350), (40, 48, 62))
draw = ImageDraw.Draw(sheet)
for index, (title, path) in enumerate(sources):
    sprite = Image.open(path).convert("RGBA")
    sprite.thumbnail((310, 310), Image.Resampling.LANCZOS)
    sheet.paste(sprite, (index * 320 + (320 - sprite.width) // 2, 30), sprite)
    draw.text((index * 320 + 15, 10), title, fill="white")
sheet.save(batch / "previews/ClaudeMove_comparison.png")
