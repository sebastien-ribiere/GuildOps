#!/usr/bin/env python3
"""Valide les 27 PNG runtime B5.3 de src/GuildOps.Game/Assets/b5_3 contre docs/b5-3/manifest.json (taille, mode, SHA-256).
usage : python3 tools/validate_b5_3.py   (nécessite Pillow ; tests/…/AssetsB53Tests.cs fait le même contrôle sans Python)"""
from pathlib import Path
import hashlib, json
from PIL import Image

root = Path(__file__).resolve().parents[1]
runtime = root / "src" / "GuildOps.Game" / "Assets" / "b5_3"
manifest = json.loads((root / "docs" / "b5-3" / "manifest.json").read_text(encoding="utf8"))
assets = manifest["integrity"]
assert len(assets) == manifest["runtime_png_count"] == 27
expected = {a["path"].removeprefix("assets/b5_3/") for a in assets}
actual = {p.relative_to(runtime).as_posix() for p in runtime.rglob("*.png")}
assert actual == expected, f"manquants/en trop : {actual ^ expected}"
for a in assets:
    f = runtime / a["path"].removeprefix("assets/b5_3/")
    im = Image.open(f); im.load()
    assert (im.width, im.height, im.mode) == (a["width"], a["height"], a["mode"]), a["path"]
    assert hashlib.sha256(f.read_bytes()).hexdigest() == a["sha256"], a["path"]
print(f"OK: {len(assets)} PNG runtime validés (taille, mode, SHA-256)")
