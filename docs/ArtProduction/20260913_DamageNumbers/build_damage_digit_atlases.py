from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
BATCH = Path(__file__).resolve().parent
RAW = BATCH / "raw"
READY = BATCH / "ready"
PREVIEWS = BATCH / "previews"
ART_ROOT = ROOT / "Assets" / "_Project" / "Art" / "UI" / "DamageNumbers"

CANVAS_HEIGHT = 440
DS_CORE_HEIGHT = 260
HS_CORE_HEIGHT = 278
DECIMAL_VISIBLE_HEIGHT = 48
CORE_BASELINE = 370
DECIMAL_BASELINE = 378
HORIZONTAL_PADDING = 4


def chroma_matte(
    image: Image.Image,
    key: tuple[int, int, int],
    clear_distance: float = 8.0,
    opaque_distance: float = 105.0,
) -> Image.Image:
    """Recover straight-alpha foreground from a saturated, nearly flat key."""
    source = image.convert("RGB")
    output = Image.new("RGBA", source.size)
    source_pixels = source.load()
    target_pixels = output.load()

    for y in range(source.height):
        for x in range(source.width):
            red, green, blue = source_pixels[x, y]
            distance = ((red - key[0]) ** 2 +
                        (green - key[1]) ** 2 +
                        (blue - key[2]) ** 2) ** 0.5
            alpha = max(0.0, min(1.0,
                (distance - clear_distance) /
                (opaque_distance - clear_distance)))
            if alpha <= 0.0:
                target_pixels[x, y] = (0, 0, 0, 0)
                continue

            recovered = []
            for observed, background in zip((red, green, blue), key):
                value = (observed - (1.0 - alpha) * background) / alpha
                recovered.append(round(max(0.0, min(255.0, value))))
            target_pixels[x, y] = (*recovered, round(alpha * 255.0))

    return output


def visible_bbox(image: Image.Image, threshold: int = 10) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A").point(lambda value: 255 if value > threshold else 0)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("No visible glyph content found")
    return bounds


def bright_core_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    mask = Image.new("L", image.size)
    source_pixels = image.load()
    mask_pixels = mask.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = source_pixels[x, y]
            maximum = max(red, green, blue)
            saturation_range = maximum - min(red, green, blue)
            is_core = (
                alpha > 80 and
                maximum > 210 and
                saturation_range < 75
            )
            mask_pixels[x, y] = 255 if is_core else 0

    bounds = mask.getbbox()
    if bounds is None:
        raise ValueError("No bright glyph core found")
    return bounds


def normalize_glyph(
    cell: Image.Image,
    target_core_height: int,
) -> Image.Image:
    bounds = visible_bbox(cell)
    glyph = cell.crop(bounds)
    core_bounds = bright_core_bbox(glyph)
    core_height = core_bounds[3] - core_bounds[1]
    scale = target_core_height / core_height
    resized = glyph.resize(
        (max(1, round(glyph.width * scale)),
         max(1, round(glyph.height * scale))),
        Image.Resampling.LANCZOS,
    )
    scaled_core_bottom = round(core_bounds[3] * scale)
    y = CORE_BASELINE - scaled_core_bottom
    if y < 0 or y + resized.height > CANVAS_HEIGHT:
        raise ValueError(
            "Normalized glyph exceeds shared vertical canvas: "
            f"y={y}, height={resized.height}"
        )
    canvas = Image.new(
        "RGBA",
        (resized.width + HORIZONTAL_PADDING * 2, CANVAS_HEIGHT),
    )
    canvas.alpha_composite(resized, (HORIZONTAL_PADDING, y))
    return canvas


def normalize_decimal(image: Image.Image) -> Image.Image:
    # Generated chroma backgrounds contain tiny low-alpha colour variations.
    # Use the solid foreground to locate the mark, then retain nearby glow.
    strong = visible_bbox(image, 96)
    margin_x = max(8, round((strong[2] - strong[0]) * 0.18))
    margin_y = max(8, round((strong[3] - strong[1]) * 0.18))
    crop_box = (
        max(0, strong[0] - margin_x),
        max(0, strong[1] - margin_y),
        min(image.width, strong[2] + margin_x),
        min(image.height, strong[3] + margin_y),
    )
    glyph = image.crop(crop_box)
    visible = visible_bbox(glyph, 8)
    glyph = glyph.crop(visible)
    scale = DECIMAL_VISIBLE_HEIGHT / max(1, glyph.height)
    resized = glyph.resize(
        (max(1, round(glyph.width * scale)), DECIMAL_VISIBLE_HEIGHT),
        Image.Resampling.LANCZOS,
    )
    canvas = Image.new(
        "RGBA",
        (resized.width + HORIZONTAL_PADDING * 2, CANVAS_HEIGHT),
    )
    y = DECIMAL_BASELINE - resized.height
    canvas.alpha_composite(resized, (HORIZONTAL_PADDING, y))
    return canvas


def process_sheet(
    role: str,
    source_name: str,
    key: tuple[int, int, int],
    decimal_source_name: str,
    decimal_key: tuple[int, int, int],
    target_core_height: int,
) -> list[Image.Image]:
    source = Image.open(RAW / source_name)
    matte = chroma_matte(source, key)
    glyphs: list[Image.Image] = []

    for digit in range(10):
        column = digit % 5
        row = digit // 5
        left = round(source.width * column / 5)
        right = round(source.width * (column + 1) / 5)
        top = round(source.height * row / 2)
        bottom = round(source.height * (row + 1) / 2)
        glyph = normalize_glyph(
            matte.crop((left, top, right, bottom)),
            target_core_height,
        )
        glyphs.append(glyph)

    decimal_source = Image.open(RAW / decimal_source_name)
    decimal_matte = chroma_matte(
        decimal_source,
        decimal_key,
        clear_distance=26.0,
        opaque_distance=118.0,
    )
    decimal = normalize_decimal(decimal_matte)
    glyphs.append(decimal)

    role_ready = READY / role
    role_art = ART_ROOT / role
    role_ready.mkdir(parents=True, exist_ok=True)
    role_art.mkdir(parents=True, exist_ok=True)
    prefix = "DS" if role == "DeepSeek" else "HS"
    for index, glyph in enumerate(glyphs):
        token = str(index) if index < 10 else "Decimal"
        name = f"SPR_{prefix}_DamageDigit_{token}_v01.png"
        glyph.save(role_ready / name)
        glyph.save(role_art / name)

    return glyphs


def compose_number(glyphs: list[Image.Image], value: str, height: int = 92) -> Image.Image:
    prepared = []
    for character in value:
        source = glyphs[10 if character == "." else int(character)]
        scale = height / max(1, source.height)
        prepared.append(source.resize(
            (max(1, round(source.width * scale)), height),
            Image.Resampling.LANCZOS,
        ))
    width = sum(item.width for item in prepared) + 2 * (len(prepared) - 1)
    output = Image.new("RGBA", (width, height))
    x = 0
    for item in prepared:
        output.alpha_composite(item, (x, 0))
        x += item.width + 2
    return output


def checker(size: tuple[int, int], cell: int = 16) -> Image.Image:
    result = Image.new("RGB", size, (44, 48, 58))
    draw = ImageDraw.Draw(result)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1),
                               fill=(70, 75, 88))
    return result


def make_preview(ds: list[Image.Image], hs: list[Image.Image]) -> None:
    preview = checker((1200, 520))
    sky = Image.new("RGB", preview.size)
    sky_pixels = sky.load()
    for y in range(sky.height):
        t = y / max(1, sky.height - 1)
        color = (
            round(11 + 66 * t),
            round(52 + 112 * t),
            round(124 + 105 * t),
        )
        for x in range(sky.width):
            sky_pixels[x, y] = color

    for background, suffix in ((preview, "checker"), (sky, "sky")):
        composed = background.convert("RGBA")
        ds_number = compose_number(ds, "12.5")
        hs_number = compose_number(hs, "12.5")
        composed.alpha_composite(ds_number, (180, 150))
        composed.alpha_composite(hs_number, (680, 150))
        composed.save(PREVIEWS / f"damage_digits_pair_{suffix}.png")


def main() -> None:
    READY.mkdir(parents=True, exist_ok=True)
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    ds = process_sheet(
        "DeepSeek",
        "DS_DamageDigits_StyleSheet_v01.png",
        (250, 2, 249),
        "DS_DamageDecimal_v01.png",
        (251, 4, 250),
        DS_CORE_HEIGHT,
    )
    hs = process_sheet(
        "Harness",
        "HS_DamageDigits_StyleSheet_v01.png",
        (3, 250, 2),
        "HS_DamageDecimal_v01.png",
        (6, 243, 13),
        HS_CORE_HEIGHT,
    )
    make_preview(ds, hs)


if __name__ == "__main__":
    main()
