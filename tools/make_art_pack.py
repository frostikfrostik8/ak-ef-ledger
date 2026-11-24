#!/usr/bin/env python3
"""Собрать внешний арт-пак AK:EF Ledger из папки со снапшотом сайта.

Ожидаемая структура источника:
    <source>/<slug>/icon.png
    <source>/<slug>/portrait.png
    <source>/<slug>/full.png

Результат:
    <out>/manifest.json
    <out>/operators/<slug>/{icon,portrait,full}.png
    <out>/art-v1.zip            (manifest.json + operators/)
    <out>/art-v1.zip.sha256

Пример:
    python tools/make_art_pack.py \
        --source "C:/AEF/dist/design-references/Сайт/operators" \
        --out "C:/AEF/dist/release/art"

По умолчанию full уменьшается до 1200 px и переводится в 256 цветов:
34,1 МБ -> ~12,5 МБ без заметной потери для окна 330x430 DIP.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
import shutil
import sys
import zipfile
from datetime import date

try:
    from PIL import Image
except ImportError:  # pragma: no cover - подсказка вместо трейсбека
    sys.exit("Pillow не установлен: python -m pip install pillow")

KINDS = ("icon", "portrait", "full")
MANIFEST = "manifest.json"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: str) -> str:
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def optimize_full(path: str, max_size: int) -> bytes:
    """Уменьшить full-арт и вернуть PNG в палитре с alpha."""
    with Image.open(path) as image:
        image = image.convert("RGBA")
        width, height = image.size
        longest = max(width, height)
        if longest > max_size:
            scale = max_size / longest
            image = image.resize(
                (max(1, round(width * scale)), max(1, round(height * scale))),
                Image.LANCZOS,
            )
        image = image.quantize(
            colors=256,
            method=Image.Quantize.FASTOCTREE,
            dither=Image.Dither.FLOYDSTEINBERG,
        )
        buffer = io.BytesIO()
        image.save(buffer, "PNG", optimize=True)
        return buffer.getvalue()


def build(source: str, out: str, max_size: int, version: str, raw: bool) -> int:
    source = os.path.abspath(source)
    out = os.path.abspath(out)
    if not os.path.isdir(source):
        sys.exit(f"Нет папки-источника: {source}")

    slugs = sorted(
        name
        for name in os.listdir(source)
        if os.path.isdir(os.path.join(source, name))
        and any(os.path.isfile(os.path.join(source, name, kind + ".png")) for kind in KINDS)
    )
    if not slugs:
        sys.exit("В источнике нет папок операторов с icon/portrait/full.png")

    if os.path.isdir(out):
        shutil.rmtree(out)
    os.makedirs(out)

    operators_dir = os.path.join(out, "operators")
    manifest = {
        "schema": 1,
        "version": version,
        "generatedAt": date.today().isoformat(),
        "source": "endfield.gryphline.com snapshot",
        "operators": [],
    }

    total = 0
    processed = 0
    for slug in slugs:
        entry: dict[str, object] = {"slug": slug, "files": {}}
        for kind in KINDS:
            source_file = os.path.join(source, slug, kind + ".png")
            if not os.path.isfile(source_file):
                continue

            target_dir = os.path.join(operators_dir, slug)
            os.makedirs(target_dir, exist_ok=True)
            target_file = os.path.join(target_dir, kind + ".png")

            if kind == "full" and not raw:
                data = optimize_full(source_file, max_size)
                with open(target_file, "wb") as stream:
                    stream.write(data)
            else:
                shutil.copyfile(source_file, target_file)
                with open(target_file, "rb") as stream:
                    data = stream.read()

            with Image.open(io.BytesIO(data)) as image:
                width, height = image.size

            entry["files"][kind] = {
                "w": width,
                "h": height,
                "bytes": len(data),
                "sha256": sha256_bytes(data),
            }
            total += len(data)
            processed += 1
        manifest["operators"].append(entry)

    manifest_path = os.path.join(out, MANIFEST)
    with open(manifest_path, "w", encoding="utf-8") as stream:
        json.dump(manifest, stream, ensure_ascii=False, indent=2)
        stream.write("\n")

    archive_path = os.path.join(out, f"art-v{version}.zip")
    with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        archive.write(manifest_path, MANIFEST)
        for root, _, files in os.walk(operators_dir):
            for name in sorted(files):
                full = os.path.join(root, name)
                relative = os.path.relpath(full, out).replace("\\", "/")
                archive.write(full, relative)

    digest = sha256_file(archive_path)
    with open(archive_path + ".sha256", "w", encoding="ascii") as stream:
        stream.write(f"{digest}  {os.path.basename(archive_path)}\n")

    archive_size = os.path.getsize(archive_path)
    print(f"Папок:        {len(slugs)}")
    print(f"Файлов:       {processed}")
    print(f"Арт:          {total / 1048576:.2f} МБ")
    print(f"ZIP:          {archive_size / 1048576:.2f} МБ -> {archive_path}")
    print(f"SHA-256:      {digest}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Собрать art-v1.zip для AK:EF Ledger")
    parser.add_argument("--source", required=True, help="Папка с <slug>/{icon,portrait,full}.png")
    parser.add_argument("--out", required=True, help="Куда положить manifest.json и zip")
    parser.add_argument("--max-size", type=int, default=1200, help="Максимальная сторона full (px)")
    parser.add_argument("--version", default="1", help="Версия пака")
    parser.add_argument("--raw", action="store_true", help="Не пережимать full, копировать как есть")
    args = parser.parse_args()
    return build(args.source, args.out, args.max_size, args.version, args.raw)


if __name__ == "__main__":
    raise SystemExit(main())
