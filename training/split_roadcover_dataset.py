from __future__ import annotations

import argparse
import random
import shutil
from pathlib import Path

from PIL import Image

try:
    import pillow_avif  # noqa: F401
except Exception:
    pillow_avif = None

LABELS = ["clean_road", "ice", "loose_snow", "snowbank_crosswalk", "snowdrift"]
LABEL_ALIASES = {
    "ice": ["ice"],
    "loose_snow": ["loose_snow", "looseSnow"],
    "snowdrift": ["snowdrift"],
    "snowbank_crosswalk": ["snowbank_crosswalk", "snowbankCrosswalk"],
    "clean_road": ["clean_road", "cleanRoad", "cleanroad"]
}
IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif", ".avif"}


def collect_images(label_dir: Path) -> list[Path]:
    candidates = sorted(
        [p for p in label_dir.rglob("*") if p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS]
    )
    valid: list[Path] = []
    for path in candidates:
        try:
            with Image.open(path) as image:
                image.verify()
            valid.append(path)
        except Exception:
            # Skip corrupted or unsupported files to keep split reproducible.
            continue

    return valid


def copy_as_jpeg(source: Path, destination: Path) -> None:
    with Image.open(source) as image:
        rgb = image.convert("RGB")
        rgb.save(destination.with_suffix(".jpg"), format="JPEG", quality=95)


def main() -> None:
    parser = argparse.ArgumentParser(description="Split raw road-cover dataset into train/val folders.")
    parser.add_argument("--source", required=True, help="Source folder with class subfolders")
    parser.add_argument("--target", required=True, help="Output dataset folder")
    parser.add_argument("--train-ratio", type=float, default=0.8, help="Train split ratio")
    parser.add_argument("--seed", type=int, default=42, help="Random seed")
    args = parser.parse_args()

    source = Path(args.source)
    target = Path(args.target)

    if not source.exists():
        raise SystemExit(f"Source folder not found: {source}")

    if target.exists():
        shutil.rmtree(target)

    random.seed(args.seed)

    for split in ["train", "val"]:
        for label in LABELS:
            (target / split / label).mkdir(parents=True, exist_ok=True)

    for label in LABELS:
        source_candidate = next((source / alias for alias in LABEL_ALIASES[label] if (source / alias).exists()), None)
        if source_candidate is None:
            raise SystemExit(f"Missing class folder for '{label}'. Expected one of: {LABEL_ALIASES[label]}")

        images = collect_images(source_candidate)
        if len(images) < 2:
            raise SystemExit(f"Need at least 2 images in class '{label}', got {len(images)}")

        random.shuffle(images)
        split_index = max(1, min(len(images) - 1, int(len(images) * args.train_ratio)))
        train_items = images[:split_index]
        val_items = images[split_index:]

        for src in train_items:
            copy_as_jpeg(src, target / "train" / label / src.stem)

        for src in val_items:
            copy_as_jpeg(src, target / "val" / label / src.stem)

        print(f"{label}: train={len(train_items)}, val={len(val_items)}")

    print(f"Dataset prepared at: {target}")


if __name__ == "__main__":
    main()
