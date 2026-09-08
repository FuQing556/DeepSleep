"""Read-only sprite measurements; previews are evidence, not visual approval."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image


def coordinate(value):
    try:
        x, y = (int(part) for part in value.split(','))
        return x, y
    except ValueError as error:
        raise argparse.ArgumentTypeError('Use x,y pixel coordinates') from error


def inspect(source, require_alpha=False, keep=(), clear=()):
    source = Path(source).resolve()
    with Image.open(source) as original:
        mode = original.mode
        has_alpha = 'A' in original.getbands() or 'transparency' in original.info
        rgba = original.convert('RGBA')
    alpha = rgba.getchannel('A')
    histogram = alpha.histogram()
    opaque = histogram[255]
    transparent = histogram[0]
    partial = sum(histogram[1:255])
    failures = []
    if alpha.getbbox() is None:
        failures.append('Image contains no visible pixels')
    if require_alpha and (not has_alpha or not transparent):
        failures.append('Expected transparent background: no true zero-alpha pixels')
    points = []
    for role, coordinates in [('keep', keep), ('clear', clear)]:
        for x, y in coordinates:
            if not (0 <= x < rgba.width and 0 <= y < rgba.height):
                raise ValueError(f'Point outside image: {x},{y}')
            pixel = rgba.getpixel((x, y))
            # These points are chosen by an operator on an opaque interior or
            # confirmed background, not automatically inferred from pixel color.
            passed = pixel[3] >= 250 if role == 'keep' else pixel[3] == 0
            points.append({'role': role, 'xy': [x, y], 'rgba': pixel, 'passed': passed})
            if not passed:
                failures.append(f'{role} point failed at {x},{y}')
    corners = [alpha.getpixel(point) for point in
               [(0, 0), (rgba.width - 1, 0), (0, rgba.height - 1),
                (rgba.width - 1, rgba.height - 1)]]
    stats = {
        'source': str(source),
        'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
        'mode': mode,
        'size': list(rgba.size),
        'has_alpha': has_alpha,
        'alpha_min_max': alpha.getextrema(),
        'transparent_pixels': transparent,
        'partial_alpha_pixels': partial,
        'opaque_pixels': opaque,
        'visible_bbox_alpha_gt_0': alpha.getbbox(),
        'visible_bbox_alpha_gt_8': alpha.point(lambda a: 255 if a > 8 else 0).getbbox(),
        'corner_alpha': corners,
        'checked_points': points,
        'automatic_failures': failures,
        'visual_review': 'REQUIRED: inspect interior holes, edges and semitransparent glow',
    }
    return rgba, stats


def save_evidence(rgba, stats, output_dir):
    output_dir = Path(output_dir).resolve()
    source = Path(stats['source'])
    prefix = source.stem
    colors = {'white': '#f7f7f7', 'dark': '#171a23', 'sky': '#315886'}
    targets = [output_dir / f'{prefix}_{label}.png' for label in colors]
    report = output_dir / f'{prefix}_alpha_report.json'
    for target in [*targets, report]:
        if target == source or target.exists():
            raise FileExistsError(f'Choose a new audit directory; refusing overwrite: {target}')
    output_dir.mkdir(parents=True, exist_ok=True)
    small = rgba.copy()
    small.thumbnail((640, 640), Image.Resampling.LANCZOS)
    for target, color in zip(targets, colors.values()):
        canvas = Image.new('RGBA', small.size, color)
        canvas.alpha_composite(small)
        canvas.convert('RGB').save(target)
    # A script-generated report is not permission to mark the artwork approved.
    report.write_text(json.dumps(stats, ensure_ascii=False, indent=2), encoding='utf-8')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--output-dir', type=Path)
    parser.add_argument('--require-alpha', action='store_true')
    parser.add_argument('--keep', action='append', default=[], type=coordinate)
    parser.add_argument('--clear', action='append', default=[], type=coordinate)
    args = parser.parse_args()
    try:
        rgba, stats = inspect(args.source, args.require_alpha, args.keep, args.clear)
        report = save_evidence(rgba, stats, args.output_dir) if args.output_dir else None
    except (ValueError, OSError) as error:
        parser.exit(2, f'{error}\n')
    print(json.dumps(stats, ensure_ascii=False, indent=2))
    if report:
        print(f'Report: {report}')
    return 1 if stats['automatic_failures'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
