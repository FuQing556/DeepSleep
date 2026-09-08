"""Local, non-generative background extraction. Never changes the input file."""
from __future__ import annotations

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def connected_neutral(image, minimum=85, maximum=240, chroma=20, seeds=(), edge_dematte=False):
    """Flood only neutral backdrop connected to the border / inspected gap seeds.

    Unlike deleting every gray/white pixel, this preserves enclosed white clothes.
    Thresholds and gap seeds belong to the individual source, not global defaults.
    """
    rgb = np.asarray(image.convert('RGB')).astype(np.int16)
    lo, hi = rgb.min(2), rgb.max(2)
    eligible = (lo >= minimum) & (hi <= maximum) & (hi - lo <= chroma)
    # One-pixel eligible frame joins every border segment without special cases.
    padded = np.pad(np.where(eligible, 0, 255).astype(np.uint8), 1)
    # fromarray can share a read-only buffer; floodfill needs an owned image.
    flood = Image.fromarray(padded).copy()
    ImageDraw.floodfill(flood, (0, 0), 128, thresh=0)
    for x, y in seeds:
        if not (0 <= x < image.width and 0 <= y < image.height):
            raise ValueError(f'Background seed outside canvas: {(x, y)}')
        if not eligible[y, x]:
            raise ValueError(f'Background seed is not neutral backdrop: {(x, y)}')
        ImageDraw.floodfill(flood, (x + 1, y + 1), 128, thresh=0)
    background = np.asarray(flood)[1:-1, 1:-1] == 128
    rgba = np.array(image.convert('RGBA'))
    if edge_dematte:
        rgba = neutral_edge_dematte(image, background)
    rgba[background] = 0
    return Image.fromarray(rgba)


def neutral_edge_dematte(image, background):
    """Unmix only the one-pixel outer rim; do not blur/erode the whole drawing."""
    rgb = np.asarray(image.convert('RGB'), dtype=np.float32)
    bg = np.pad(background, 1)
    colors = np.pad(rgb, ((1, 1), (1, 1), (0, 0)), mode='edge')
    total = np.zeros_like(rgb)
    count = np.zeros(background.shape, dtype=np.float32)
    h, w = background.shape
    for dy in range(3):
        for dx in range(3):
            if dx == dy == 1:
                continue
            neighbor = bg[dy:dy+h, dx:dx+w]
            total += colors[dy:dy+h, dx:dx+w] * neighbor[:, :, None]
            count += neighbor
    backdrop = total / np.maximum(count[:, :, None], 1)
    # The drawing has a dark outer outline. Nearby darker samples estimate its
    # foreground color without interpreting clothing highlights as background.
    foreground = np.asarray(image.convert('RGB').filter(ImageFilter.MinFilter(3)), dtype=np.float32)
    direction = foreground - backdrop
    alpha = np.sum((rgb - backdrop) * direction, 2) / np.maximum(np.sum(direction**2, 2), 1)
    alpha = np.clip(alpha, 0, 1)
    edge = (~background) & (count > 0) & (alpha < 0.995)
    clean = (rgb - backdrop * (1-alpha[:, :, None])) / np.maximum(alpha[:, :, None], 1/255)
    out = np.array(image.convert('RGBA'))
    out[edge, :3] = np.round(np.clip(clean[edge], 0, 255)).astype(np.uint8)
    out[edge, 3] = np.round(alpha[edge] * out[edge, 3]).astype(np.uint8)
    return out


def green_screen(image, cutoff=3, backdrop_tolerance=26, emission_edges=False):
    """Extract green backdrop and unmix its color from semitransparent edges.

    Intended ONLY for artwork without green subject colors. White highlights and
    black surfaces stay opaque; red halos retain soft alpha instead of green rims.
    """
    rgba = np.asarray(image.convert('RGBA'), dtype=np.float32) / 255
    rgb = rgba[:, :, :3]
    excess = np.maximum(0, rgb[:, :, 1] - np.minimum(rgb[:, :, 0], rgb[:, :, 2]))
    # Skin naturally has more green than blue. Never despill it (or any other
    # interior color) unless green is also dominant over RED.
    green_dominant = rgb[:, :, 1] > np.maximum(rgb[:, :, 0], rgb[:, :, 2]) + 0.015
    if not emission_edges:
        excess = np.where(green_dominant, excess, 0)
    alpha = 1 - excess
    foreground = rgb.copy()
    foreground[:, :, 1] -= excess
    foreground /= np.maximum(alpha[:, :, None], 1 / 255)
    out = np.zeros(rgba.shape, dtype=np.uint8)
    out[:, :, :3] = np.round(np.clip(foreground, 0, 1) * 255).astype(np.uint8)
    out[:, :, 3] = np.round(alpha * rgba[:, :, 3] * 255).astype(np.uint8)
    plain_backdrop = (np.maximum(rgb[:, :, 0], rgb[:, :, 2]) < backdrop_tolerance/255) & (rgb[:, :, 1] > 0.6)
    out[plain_backdrop] = 0
    out[out[:, :, 3] <= cutoff] = 0
    return Image.fromarray(out)


def black_emission(image, cutoff=3):
    """Convert emission painted over black into straight-alpha RGB + soft alpha.

    Only for light effects, NEVER black-clothed characters / solid black weapons.
    RGB * alpha reconstructs original emission on black to rounding precision.
    """
    rgb = np.asarray(image.convert('RGB'), dtype=np.float32) / 255
    alpha = rgb.max(2)
    straight = rgb / np.maximum(alpha[:, :, None], 1 / 255)
    out = np.dstack((straight, alpha))
    out = np.round(np.clip(out, 0, 1) * 255).astype(np.uint8)
    out[out[:, :, 3] <= cutoff] = 0
    return Image.fromarray(out)


def report(image):
    alpha = np.asarray(image.getchannel('A'))
    return {
        'size': list(image.size), 'mode': image.mode,
        'visible_bbox': image.getchannel('A').getbbox(),
        'transparent_pixels': int((alpha == 0).sum()),
        'partial_alpha_pixels': int(((alpha > 0) & (alpha < 255)).sum()),
        'opaque_pixels': int((alpha == 255).sum()),
        'clear_corners': bool(all(alpha[y, x] == 0 for x, y in
            [(0, 0), (image.width-1, 0), (0, image.height-1), (image.width-1, image.height-1)])),
    }


def preview(image, background, max_side=650):
    scaled = image.copy()
    scaled.thumbnail((max_side, max_side), Image.Resampling.LANCZOS)
    canvas = Image.new('RGBA', scaled.size, background)
    canvas.alpha_composite(scaled)
    return canvas.convert('RGB')
