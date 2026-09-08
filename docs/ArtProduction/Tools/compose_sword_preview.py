"""Preview separate character and weapon art; never modifies either source.

Only whole-weapon rotation, translation and UNIFORM scaling are used. This is not
limb deformation or animation generation. Every pose uses the same weapon scale.
"""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw


def attached_layer(source, canvas_size, source_pivot, destination_pivot, angle, scale):
    cosine, sine = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    x, y = destination_pivot
    px, py = source_pivot
    # Pillow asks for inverse destination -> source coordinates.
    matrix = (cosine/scale, sine/scale, px-(cosine*x+sine*y)/scale,
              -sine/scale, cosine/scale, py+(sine*x-cosine*y)/scale)
    return source.convert('RGBa').transform(canvas_size, Image.Transform.AFFINE,
        matrix, resample=Image.Resampling.BICUBIC).convert('RGBA')


def run(config_path):
    config_path = Path(config_path).resolve()
    root = config_path.parent
    config = json.loads(config_path.read_text(encoding='utf-8'))
    canvas_size = tuple(config['canvas'])
    weapon_config = config['weapon']
    weapon = Image.open(root / weapon_config['file']).convert('RGBA')
    split_x = weapon_config['front_from_x']
    blade = Image.new('RGBA', weapon.size)
    blade.alpha_composite(weapon.crop((split_x, 0, weapon.width, weapon.height)), (split_x, 0))
    rear_grip = Image.new('RGBA', weapon.size)
    rear_grip.alpha_composite(weapon.crop((0, 0, split_x, weapon.height)))
    blade.save(root / 'cutouts' / 'HA_Sword_BladeFront_v01.png')
    rear_grip.save(root / 'cutouts' / 'HA_Sword_GripBack_v01.png')
    output = root / 'previews'
    output.mkdir(exist_ok=True)
    assembled = []
    for pose in config['poses']:
        character = Image.open(root / pose['file']).convert('RGBA')
        offset = tuple(a-b for a, b in zip(config['body_anchor_on_canvas'], pose['body_anchor']))
        hand = tuple(a+b for a, b in zip(offset, pose['hand_anchor']))
        result = Image.new('RGBA', canvas_size)
        rear_layer = attached_layer(rear_grip, canvas_size, weapon_config['grip_pixel'], hand,
                                    pose['weapon_angle'], weapon_config['uniform_scale'])
        result.alpha_composite(rear_layer)
        result.alpha_composite(character, offset)
        weapon_layer = attached_layer(blade, canvas_size, weapon_config['grip_pixel'], hand,
                                      pose['weapon_angle'], weapon_config['uniform_scale'])
        result.alpha_composite(weapon_layer)
        # Restore original hand pixels in front of the hilt. No finger is redrawn.
        cover = pose['grip_cover']
        grip_front = character.crop(cover)
        result.alpha_composite(grip_front, (offset[0]+cover[0], offset[1]+cover[1]))
        # Separate full-canvas front-grip sprite is reusable for future equipment.
        grip_asset = Image.new('RGBA', character.size)
        grip_asset.alpha_composite(grip_front, (cover[0], cover[1]))
        grip_asset.save(root / 'cutouts' / f'HA_{pose["name"]}_GripFront_v01.png')
        result.save(output / f'COMPOSITE_{pose["name"]}.png')
        assembled.append((pose['name'], result))
        # Full-resolution grip detail, no resampling: useful for occlusion QA.
        grip = result.crop((hand[0]-140, hand[1]-140, hand[0]+180, hand[1]+180))
        bg = Image.new('RGBA', grip.size, '#294b85')
        bg.alpha_composite(grip)
        bg.convert('RGB').save(output / f'GRIP_{pose["name"]}.jpg', quality=95)
    sheet = Image.new('RGB', (1440, 1320), '#203957')
    draw = ImageDraw.Draw(sheet)
    for index, (name, result) in enumerate(assembled):
        tile = Image.new('RGBA', canvas_size, '#294b85')
        tile.alpha_composite(result)
        tile.thumbnail((710, 630), Image.Resampling.LANCZOS)
        x, y = (index % 2) * 720, (index // 2) * 660
        sheet.paste(tile.convert('RGB'), (x, y+28))
        draw.text((x+16, y+10), name, fill='white')
    sheet.save(output / 'CONTACT_SwordAssembly.jpg', quality=95)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('config')
    run(parser.parse_args().config)
