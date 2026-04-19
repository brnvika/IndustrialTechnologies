from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from ultralytics import YOLO


ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    parser = argparse.ArgumentParser(description="Train YOLOv8 classifier and export ONNX model.")
    parser.add_argument("--dataset", required=True, help="Dataset folder with train/val")
    parser.add_argument("--model-output", required=True, help="Target ONNX path")
    parser.add_argument("--weights", default="yolov8s-cls.pt", help="Initial weights or checkpoint for fine-tuning")
    parser.add_argument("--epochs", type=int, default=75, help="Training epochs")
    parser.add_argument("--imgsz", type=int, default=224, help="Input image size")
    parser.add_argument("--batch", type=int, default=16, help="Batch size")
    args = parser.parse_args()

    dataset_dir = Path(args.dataset)
    output_model = Path(args.model_output)

    if not (dataset_dir / "train").exists() or not (dataset_dir / "val").exists():
        raise SystemExit(f"Dataset must contain train/ and val/: {dataset_dir}")

    model = YOLO(args.weights)
    model.train(
        data=str(dataset_dir),
        imgsz=args.imgsz,
        epochs=args.epochs,
        batch=args.batch,
        device="cpu",
        project=str(ROOT / "training" / "runs"),
        name="roadcover",
        patience=25,
    )

    exported = model.export(format="onnx", opset=17, dynamic=False)
    exported_path = Path(exported)

    output_model.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(exported_path, output_model)

    print(f"Model exported to: {output_model}")


if __name__ == "__main__":
    main()
