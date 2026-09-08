"""Remove a baked neutral checkerboard without treating enclosed gaps as foreground."""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from sprite_matte import neutral_edge_dematte


def border_background(eligible: np.ndarray) -> np.ndarray:
    padded = np.pad(np.where(eligible, 0, 255).astype(np.uint8), 1)
    flood = Image.fromarray(padded).copy()
    ImageDraw.floodfill(flood, (0, 0), 128, thresh=0)
    return np.asarray(flood)[1:-1, 1:-1] == 128


def seeded_background(eligible: np.ndarray, seeds: list[tuple[int, int]]) -> np.ndarray:
    padded = np.pad(np.where(eligible, 0, 255).astype(np.uint8), 1)
    flood = Image.fromarray(padded).copy()
    for x, y in seeds:
        if not (0 <= x < eligible.shape[1] and 0 <= y < eligible.shape[0]):
            raise ValueError(f"Background seed outside canvas: {(x, y)}")
        if not eligible[y, x]:
            raise ValueError(f"Seed is not neutral checkerboard: {(x, y)}")
        ImageDraw.floodfill(flood, (x + 1, y + 1), 128, thresh=0)
    return np.asarray(flood)[1:-1, 1:-1] == 128


def checkerboard_matte(image: Image.Image, seeds: list[tuple[int, int]]) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    low = rgb.min(2)
    high = rgb.max(2)
    chroma = high - low

    strict_neutral = (low >= 100) & (high <= 255) & (chroma <= 10)
    broad_neutral = (low >= 88) & (high <= 255) & (chroma <= 18)
    background = border_background(strict_neutral) | seeded_background(broad_neutral, seeds)

    rgba = neutral_edge_dematte(image, background)
    rgba[background] = 0
    return Image.fromarray(rgba)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("output")
    parser.add_argument("--seed", action="append", default=[], help="enclosed background point as x,y")
    args = parser.parse_args()
    seeds = [tuple(map(int, item.split(","))) for item in args.seed]
    result = checkerboard_matte(Image.open(args.source), seeds)
    Path(args.output).parent.mkdir(parents=True, exist_ok=True)
    result.save(args.output)


if __name__ == "__main__":
    main()
