"""Crop transparent margins without changing visible pixels."""
from __future__ import annotations

import argparse
from pathlib import Path

try:
    from PIL import Image
except ModuleNotFoundError as exc:
    raise SystemExit(
        "Missing image dependencies. Run: "
        "python -m pip install -r docs/ArtProduction/Tools/requirements.txt"
    ) from exc


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--padding", type=int, default=0)
    args = parser.parse_args()

    image = Image.open(args.source).convert("RGBA")
    bounds = image.getchannel("A").getbbox()
    if bounds is None:
        raise SystemExit("Source contains no visible pixels")

    padding = max(0, args.padding)
    left = max(0, bounds[0] - padding)
    top = max(0, bounds[1] - padding)
    right = min(image.width, bounds[2] + padding)
    bottom = min(image.height, bounds[3] + padding)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    image.crop((left, top, right, bottom)).save(args.output)
    print(f"{image.size} -> {(right-left, bottom-top)}; crop={(left, top, right, bottom)}")


if __name__ == "__main__":
    main()
