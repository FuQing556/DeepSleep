"""Read-only source alpha measurement; outputs reports/previews, never edits sources."""
from pathlib import Path
import argparse
import importlib.util
import json
import hashlib

from PIL import Image, ImageDraw


# Border pixels are relative to the independently cropped production image.
# These are art-reviewed corner/cap extents, not automatic alpha threshold results.
BORDERS_LBRT = {
    "SPR_UI_DS_ButtonBase": [200, 92, 200, 92],
    "SPR_UI_DS_ButtonFrame": [250, 95, 250, 95],
    "SPR_UI_DS_ButtonGlow": [180, 160, 180, 160],
    "SPR_UI_DS_PanelFrame": [250, 250, 250, 250],
    "SPR_UI_HA_ButtonBase": [180, 100, 180, 100],
    "SPR_UI_HA_ButtonFrame": [200, 100, 200, 100],
    "SPR_UI_HA_ButtonGlow": [160, 140, 160, 140],
    "SPR_UI_HA_PanelFrame": [200, 200, 200, 200],
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--inspector", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--write-ready", action="store_true")
    args = parser.parse_args()
    batch = Path(__file__).resolve().parent
    spec = importlib.util.spec_from_file_location("sprite_inspector", args.inspector)
    inspector = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(inspector)
    output = args.output_dir.resolve()
    output.mkdir(parents=True, exist_ok=True)
    reports = []
    thumbs = []
    production = []
    for source in sorted((batch / "raw").glob("*.png")):
        rgba, stats = inspector.inspect(source, require_alpha=True)
        inspector.save_evidence(rgba, stats, output)
        alpha = rgba.getchannel("A")
        w, h = rgba.size
        hist = alpha.histogram()
        stats["bounds_at_threshold"] = {
            str(t): alpha.point(lambda a: 255 if a > t else 0).getbbox()
            for t in [0, 1, 2, 4, 8, 16, 32, 64, 128, 250]
        }
        stats["alpha_one_pixels"] = hist[1]
        b = alpha.getbbox()
        stats["lossless_crop_rect_exclusive"] = (
            [max(0, b[0] - 2), max(0, b[1] - 2), min(w, b[2] + 2), min(h, b[3] + 2)]
            if b else None
        )
        core = (int(w * .30), int(h * .30), int(w * .70), int(h * .70))
        core_hist = alpha.crop(core).histogram()
        stats["central_40_percent"] = {
            "rect_exclusive": core,
            "alpha_extrema": alpha.crop(core).getextrema(),
            "nonzero_pixels": sum(core_hist[1:]),
            "over_8_pixels": sum(core_hist[9:]),
            "over_64_pixels": sum(core_hist[65:]),
            "total_pixels": (core[2]-core[0])*(core[3]-core[1]),
        }
        stats["center_rgba"] = rgba.getpixel((w//2, h//2))
        meaningful = stats["bounds_at_threshold"]["1"]
        if meaningful:
            practical = [max(0, meaningful[0]-16), max(0, meaningful[1]-16),
                         min(w, meaningful[2]+16), min(h, meaningful[3]+16)]
            inside_hist = alpha.crop(practical).histogram()
            discarded = [hist[i]-inside_hist[i] for i in range(256)]
            stats["optional_production_crop_rect_exclusive"] = practical
            stats["optional_crop_nonzero_pixels_discarded"] = sum(discarded[1:])
            stats["optional_crop_max_discarded_alpha"] = max([i for i in range(1, 256) if discarded[i]] or [0])
            stats["optional_crop_requires_explicit_acceptance"] = True
            stats["production_crop_accepted_by_root"] = True
            inner = [round(meaningful[0]+(meaningful[2]-meaningful[0])*.20),
                     round(meaningful[1]+(meaningful[3]-meaningful[1])*.30),
                     round(meaningful[0]+(meaningful[2]-meaningful[0])*.80),
                     round(meaningful[1]+(meaningful[3]-meaningful[1])*.70)]
            inner_hist = alpha.crop(inner).histogram()
            stats["visible_shape_interior"] = {"rect_exclusive": inner,
                "alpha_extrema": alpha.crop(inner).getextrema(),
                "nonzero_pixels": sum(inner_hist[1:]), "over_one_pixels": sum(inner_hist[2:])}
        stats["suggested_border_left_bottom_right_top"] = BORDERS_LBRT.get(source.stem, [0, 0, 0, 0])
        stats["source_pixel_changes"] = "none"
        stats["unity_imported"] = False
        stats["visual_review"] = "Pending human/agent inspection; automatic measurements alone are not approval"
        reports.append(stats)
        if args.write_ready and meaningful:
            target = batch / "ready" / source.name
            if target.exists():
                raise FileExistsError(f"Refusing to overwrite a ready asset: {target}")
            target.parent.mkdir(parents=True, exist_ok=True)
            result = rgba.crop(practical)
            result.save(target)
            solid = stats["bounds_at_threshold"]["64"]
            optical = [solid[0]-practical[0], solid[1]-practical[1],
                       practical[2]-solid[2], practical[3]-solid[3]]
            item = {"source": str(source), "source_sha256": stats["source_sha256"],
                    "output": str(target), "output_sha256": hashlib.sha256(target.read_bytes()).hexdigest(),
                    "original_size": stats["size"], "output_size": list(result.size),
                    "crop_rect_source_xyxy_exclusive": practical,
                    "crop_offset_source_xy": practical[:2],
                    "method": "Crop alpha>1 bbox plus 16 px padding, preserving all retained RGB and alpha; no resampling or color key",
                    "discarded_nonzero_pixels": stats["optional_crop_nonzero_pixels_discarded"],
                    "discarded_max_alpha": stats["optional_crop_max_discarded_alpha"],
                    "retained_pixel_changes": 0,
                    "optical_inset_ltrb_px_at_alpha64": optical,
                    "suggested_border_lbrt_px": stats["suggested_border_left_bottom_right_top"],
                    "image_type": "Simple preserveAspect" if "Ornament" in source.stem or "Circle" in source.stem else "Sliced",
                    "integration_note": "Border units are source pixels including transparent padding. Button frame/base/glow align by optical bounds; use dedicated PanelFrame for tall panels. Circle/ornament must remain proportional.",
                    "unity_imported": False, "user_accepted_style_direction": True,
                    "user_accepted_each_final_asset": False}
            production.append(item)
        thumb = rgba.copy()
        thumb.thumbnail((510, 290), Image.Resampling.LANCZOS)
        cell = Image.new("RGB", (540, 340), "#202936")
        draw = ImageDraw.Draw(cell)
        draw.text((12, 8), source.stem, fill="white")
        cell.paste(thumb, ((540-thumb.width)//2, 34+(290-thumb.height)//2), thumb)
        thumbs.append(cell)
    (output / "layer_measurements.json").write_text(json.dumps(reports, ensure_ascii=False, indent=2), encoding="utf-8")
    if production:
        (output / "production_crop_manifest.json").write_text(json.dumps(production, ensure_ascii=False, indent=2), encoding="utf-8")
    sheet = Image.new("RGB", (1080, ((len(thumbs)+1)//2)*340), "#111820")
    for i, cell in enumerate(thumbs):
        sheet.paste(cell, ((i%2)*540, (i//2)*340))
    sheet.save(output / "contact_sheet_dark.png")
    for background in ["white", "sky"]:
        sheet = Image.new("RGB", (1080, ((len(reports)+1)//2)*340), "#111820")
        for i, report in enumerate(reports):
            stem = Path(report["source"]).stem
            thumb = Image.open(output / f"{stem}_{background}.png")
            thumb.thumbnail((510, 290), Image.Resampling.LANCZOS)
            cell = Image.new("RGB", (540, 340), "#202936")
            ImageDraw.Draw(cell).text((12, 8), stem, fill="white")
            cell.paste(thumb, ((540-thumb.width)//2, 34+(290-thumb.height)//2))
            sheet.paste(cell, ((i%2)*540, (i//2)*340))
        sheet.save(output / f"contact_sheet_{background}.png")
    for r in reports:
        print(r["source"].split("\\")[-1], r["size"], r["lossless_crop_rect_exclusive"],
              "b8", r["bounds_at_threshold"]["8"], "center", r["central_40_percent"],
              "sha", r["source_sha256"])


if __name__ == "__main__":
    main()
