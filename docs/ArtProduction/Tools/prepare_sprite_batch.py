"""Run an explicit JSON art recipe; all outputs remain in its own folder."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image
from sprite_matte import connected_neutral, green_screen, black_emission, report, preview


def run(recipe_path):
    recipe_path = Path(recipe_path).resolve()
    root = recipe_path.parent
    recipe = json.loads(recipe_path.read_text(encoding='utf-8-sig'))
    results = []
    for item in recipe['assets']:
        source = (root / item['source']).resolve()
        target = (root / item['output']).resolve()
        if source == target or not target.is_relative_to(root):
            raise ValueError('Output must be a separate file within this recipe folder')
        image = Image.open(source)
        mode = item['method']
        parameters = item.get('parameters', {})
        if mode == 'neutral':
            result = connected_neutral(image, **parameters)
        elif mode == 'green':
            result = green_screen(image, **parameters)
        elif mode == 'emission':
            result = black_emission(image, **parameters)
        elif mode == 'alpha':
            if image.mode != 'RGBA' or image.getchannel('A').getextrema()[0] != 0:
                raise ValueError('Source does not already have real transparency')
            result = image.copy()
        else:
            raise ValueError(f'Unknown matte method: {mode}')
        stats = report(result)
        if not stats['transparent_pixels'] or not stats['opaque_pixels'] or not stats['clear_corners']:
            raise ValueError(f'Invalid empty/opaque cutout: {source}')
        target.parent.mkdir(parents=True, exist_ok=True)
        result.save(target)
        preview_root = root / 'previews'
        preview_root.mkdir(exist_ok=True)
        for label, color in [('light', '#efedf2'), ('dark', '#171622'), ('game', '#294b85')]:
            preview(result, color).save(preview_root / f'{target.stem}_{label}.jpg', quality=94)
        results.append({'source': item['source'], 'output': item['output'],
                        'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
                        'method': mode, 'parameters': parameters, **stats})
    (root / 'matte_report.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(results, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('recipe')
    run(parser.parse_args().recipe)
