from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

import numpy as np
from PIL import Image
from ultralytics import YOLO


LABELS = ["ice", "loose_snow", "snowdrift", "snowbank_crosswalk"]
IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".bmp", ".webp", ".jfif"}


def main() -> None:
    parser = argparse.ArgumentParser(description="Evaluate road-cover ONNX model on validation set.")
    parser.add_argument("--dataset", required=True, help="Validation dataset folder (class subfolders)")
    parser.add_argument("--model", required=True, help="Path to ONNX model")
    args = parser.parse_args()

    dataset_dir = Path(args.dataset)
    model_path = Path(args.model)

    if not dataset_dir.exists():
        raise SystemExit(f"Dataset not found: {dataset_dir}")
    if not model_path.exists():
        raise SystemExit(f"Model not found: {model_path}")

    model = YOLO(str(model_path), task="classify")

    correct = 0
    total = 0
    matrix = defaultdict(lambda: defaultdict(int))

    for true_label in LABELS:
        class_dir = dataset_dir / true_label
        if not class_dir.exists():
            continue

        for image_path in class_dir.iterdir():
            if not image_path.is_file() or image_path.suffix.lower() not in IMAGE_EXTENSIONS:
                continue

            with Image.open(image_path) as image:
                rgb = image.convert("RGB")
                pred = model.predict(np.array(rgb), imgsz=224, verbose=False)[0]
            probs = pred.probs
            pred_idx = int(probs.top1)
            pred_label = LABELS[pred_idx] if pred_idx < len(LABELS) else "unknown"
            confidence = float(probs.top1conf)

            total += 1
            if pred_label == true_label:
                correct += 1
            matrix[true_label][pred_label] += 1

            print(
                json.dumps(
                    {
                        "file": image_path.name,
                        "true": true_label,
                        "pred": pred_label,
                        "confidence": round(confidence, 4),
                    },
                    ensure_ascii=False,
                )
            )

    accuracy = (correct / total) if total else 0.0
    print("\nSummary")
    print(json.dumps({"total": total, "correct": correct, "accuracy": round(accuracy, 4)}, ensure_ascii=False))
    print("Confusion matrix")
    print(json.dumps(matrix, ensure_ascii=False))


if __name__ == "__main__":
    main()
