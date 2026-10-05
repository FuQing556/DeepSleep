"""Native-alpha audit for this batch only; no recoloring, matting, or resampling assets."""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


BATCH = Path(__file__).resolve().parent
PROJECT = BATCH.parents[2]
INSPECTOR = Path.home() / '.codex/skills/deepsleep-sprite-matting/scripts/inspect_sprite.py'
REJECTED = {'ICO_BUFF_DataCompression.png', 'ICO_BUFF_RiceFan.png', 'ICO_BUFF_RiceStorm.png'}
DEFAULT = [
    'SPR_UI_DS_TitleLogo.png', 'SPR_UI_HA_TitleLogo.png',
    'BG_UI_DS_MainMenu.png', 'BG_UI_HA_MainMenu.png',
    'ICO_BUFF_FaultToleranceExpansion.png',
    'ICO_BUFF_DataCompression_v2.png', 'ICO_BUFF_RiceFan_v2.png', 'ICO_BUFF_RiceStorm_v2.png',
    'ICO_BUFF_RiceGuidance.png', 'ICO_BUFF_RiceSplash.png', 'ICO_BUFF_RiceGuard.png',
    'ICO_BUFF_QuantumSword.png', 'ICO_BUFF_TerminalAmplifier.png', 'ICO_BUFF_TerminalArray.png',
    'ICO_BUFF_TerminalBurst.png', 'ICO_BUFF_TerminalChain.png',
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def copy_if_new(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and digest(source) != digest(destination):
        raise RuntimeError(f'Refusing to overwrite different existing pixels: {destination}')
    if not destination.exists():
        shutil.copy2(source, destination)


def contact_sheets():
    files = sorted((BATCH / 'ready').glob('*.png'))
    colors = {'white': '#f7f7f7', 'dark': '#171a23', 'sky': '#315886'}
    for label, color in colors.items():
        canvas = Image.new('RGB', (1050, 205 * len(files)), color)
        draw = ImageDraw.Draw(canvas)
        ink = '#18232d' if label == 'white' else '#ffffff'
        for index, path in enumerate(files):
            draw.text((12, index * 205 + 8), path.name, fill=ink)
            with Image.open(path) as source:
                small = source.convert('RGBA')
            small.thumbnail((980, 170), Image.Resampling.LANCZOS)
            canvas.paste(small, ((1050 - small.width) // 2, index * 205 + 28), small)
        canvas.save(BATCH / 'previews' / f'contact_{label}.png')
    buffs = [path for path in files if path.name.startswith('ICO_BUFF_')]
    for label, color in colors.items():
        canvas = Image.new('RGB', (1040, 230 * max((len(buffs) + 3) // 4, 1)), color)
        draw = ImageDraw.Draw(canvas)
        ink = '#18232d' if label == 'white' else '#ffffff'
        for index, path in enumerate(buffs):
            x, y = (index % 4) * 260, (index // 4) * 230
            with Image.open(path) as source:
                small = source.convert('RGBA').resize((180, 180), Image.Resampling.LANCZOS)
            canvas.paste(small, (x + 40, y + 8), small)
            draw.text((x + 10, y + 200), path.stem.removeprefix('ICO_BUFF_'), fill=ink)
        canvas.save(BATCH / 'previews' / f'buffs_contact_{label}.png')
    small_canvas = Image.new('RGB', (680, 100 * max(len(buffs), 1)), '#171a23')
    draw = ImageDraw.Draw(small_canvas)
    for index, path in enumerate(buffs):
        with Image.open(path) as source:
            small = source.convert('RGBA').resize((40, 40), Image.Resampling.LANCZOS)
        for x, color in [(12, '#f7f7f7'), (72, '#171a23'), (132, '#315886')]:
            small_canvas.paste(color, (x, index * 100 + 28, x + 40, index * 100 + 68))
            small_canvas.paste(small, (x, index * 100 + 28), small)
        draw.text((188, index * 100 + 43), path.stem, fill='white')
    small_canvas.save(BATCH / 'previews' / 'buffs_40px_white_dark_sky.png')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sources', nargs='+', default=DEFAULT)
    parser.add_argument('--copy-assets', action='store_true')
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('inspect_sprite', INSPECTOR)
    inspector = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inspector)
    (BATCH / 'ready').mkdir(exist_ok=True)
    (BATCH / 'previews').mkdir(exist_ok=True)
    report_path = BATCH / 'production_manifest.json'
    report = json.loads(report_path.read_text(encoding='utf-8')) if report_path.exists() else {
        'batch': BATCH.name,
        'method': 'Native alpha preserved. Only titles crop alpha>1 bounding box plus 16px; all retained RGBA bytes unchanged.',
        'user_individual_asset_approval': False,
        'unity_imported_by_this_script': False,
        'rejected_sources': [], 'assets': {},
    }
    for rejected_name in sorted(REJECTED):
        path = BATCH / 'raw' / rejected_name
        if path.exists() and not any(item['file'] == rejected_name for item in report['rejected_sources']):
            report['rejected_sources'].append({'file': rejected_name, 'sha256': digest(path),
                'reason': 'User rejected triangular nori rice-ball representation; not the project rice-grain projectile.',
                'retained_raw_only': True})
    for filename in args.sources:
        if Path(filename).name != filename or filename in REJECTED:
            raise RuntimeError(f'Source prohibited: {filename}')
        source = BATCH / 'raw' / filename
        before = digest(source)
        rgba, source_stats = inspector.inspect(source, require_alpha=not filename.startswith('BG_'))
        if source_stats['automatic_failures']:
            raise RuntimeError(f'Alpha check failed: {filename}: {source_stats["automatic_failures"]}')
        canonical = filename.replace('_v2.png', '.png')
        ready = BATCH / 'ready' / canonical
        crop = (0, 0, rgba.width, rgba.height)
        omitted_alpha1 = 0
        if canonical.startswith('SPR_UI_') and 'TitleLogo' in canonical:
            alpha = rgba.getchannel('A')
            content = alpha.point(lambda a: 255 if a > 1 else 0).getbbox()
            if content is None:
                raise RuntimeError(f'Empty title: {filename}')
            crop = (max(0, content[0] - 16), max(0, content[1] - 16),
                    min(rgba.width, content[2] + 16), min(rgba.height, content[3] + 16))
            final = rgba.crop(crop)
            omitted_alpha1 = alpha.histogram()[1] - final.getchannel('A').histogram()[1]
            assert sum(alpha.histogram()[2:]) == sum(final.getchannel('A').histogram()[2:])
            if ready.exists():
                with Image.open(ready) as previous:
                    assert previous.convert('RGBA').tobytes() == final.tobytes() and previous.size == final.size
            else:
                final.save(ready)
        else:
            if canonical.startswith('ICO_BUFF_') and rgba.width != rgba.height:
                raise RuntimeError(f'Buff must retain square canvas: {filename} {rgba.size}')
            copy_if_new(source, ready)
        output_rgba, output_stats = inspector.inspect(ready, require_alpha=not canonical.startswith('BG_'))
        audit_dir = BATCH / 'previews' / 'alpha_audit' / ready.stem
        if not audit_dir.exists():
            inspector.save_evidence(output_rgba, output_stats, audit_dir)
        if canonical.startswith('ICO_BUFF_'):
            asset = PROJECT / 'Assets/_Project/Art/UI/Buffs' / canonical
        else:
            theme = 'DeepSeek' if '_DS_' in canonical else 'Harness'
            asset = PROJECT / 'Assets/_Project/Art/UI/Themes' / theme / canonical
        if args.copy_assets:
            copy_if_new(ready, asset)
        assert digest(source) == before, f'Raw changed: {filename}'
        report['assets'][canonical] = {
            'selected_source': f'raw/{filename}', 'source_sha256': before,
            'source_size': source_stats['size'], 'ready': f'ready/{canonical}',
            'ready_sha256': digest(ready), 'ready_size': output_stats['size'],
            'crop_box_in_source': crop, 'removed_nonzero_alpha1_pixels': omitted_alpha1,
            'retained_rgba_unchanged': True, 'alpha': output_stats,
            'production_path': str(asset), 'copied_to_assets': args.copy_assets or asset.exists(),
            'unity_imported': False, 'visual_review': 'See visual_review.json for explicit inspected assets. Automatic alpha tests are not art approval.',
        }
        print(f'{filename} -> {canonical}: {source_stats["size"]} -> {output_stats["size"]}; alpha1 omitted={omitted_alpha1}; copied={args.copy_assets}')
    for item in report['assets'].values():
        assert digest(BATCH / item['selected_source']) == item['source_sha256']
    for item in report['rejected_sources']:
        assert digest(BATCH / 'raw' / item['file']) == item['sha256']
    report['formal_asset_count'] = len(report['assets'])
    report['formal_buff_count'] = sum(name.startswith('ICO_BUFF_') for name in report['assets'])
    report['all_recorded_raw_hashes_unchanged'] = True
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    contact_sheets()


if __name__ == '__main__':
    main()
