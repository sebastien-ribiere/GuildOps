#!/usr/bin/env python3
"""Install the original B5 asset pack (PNG only) without Pillow.
Run from the repository root:
  python3 tools/import_b5_art.py /path/to/guildops_b5_art_16bits.zip
"""
import argparse
import struct
import zipfile
from pathlib import Path

EXPECTED = {'backgrounds':1, 'portraits':12, 'sprites':12, 'ui':8, 'icons':9}
MAGIC = b'\x89PNG\r\n\x1a\n'

def run(zip_path: Path, destination: Path) -> None:
    if not zip_path.is_file():
        raise FileNotFoundError(zip_path)
    counts = {k: 0 for k in EXPECTED}
    members = []
    with zipfile.ZipFile(zip_path) as archive:
        for info in archive.infolist():
            path = Path(info.filename)
            if info.is_dir() or 'assets' not in path.parts:
                continue
            pos = path.parts.index('assets')
            sub = path.parts[pos + 1:]
            if len(sub) != 2 or sub[0] not in EXPECTED or not sub[1].endswith('.png'):
                continue
            if '..' in sub or any(p.startswith('.') for p in sub):
                raise ValueError(f'Invalid path in asset pack: {info.filename}')
            data = archive.read(info)
            if data[:8] != MAGIC or len(data) < 24:
                raise ValueError(f'Not a PNG: {info.filename}')
            width, height = struct.unpack('>II', data[16:24])
            if sub[0] == 'portraits' and (width,height) != (64,64):
                raise ValueError(f'Unexpected portrait size: {info.filename}')
            if sub[0] == 'sprites' and (width,height) != (128,48):
                raise ValueError(f'Unexpected idle strip: {info.filename}')
            counts[sub[0]] += 1
            members.append((sub,data))
    if counts != EXPECTED:
        raise ValueError(f'Asset pack incomplete: counts={counts}, expected={EXPECTED}')
    for (folder,filename),data in members:
        path = destination/folder/filename
        path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(data)
    print(f'B5 art: {len(members)} verified PNGs installed in {destination}')

if __name__ == '__main__':
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('zip_file', type=Path)
    ap.add_argument('--destination',type=Path,default=Path('src/GuildOps.Game/Assets'))
    args=ap.parse_args()
    run(args.zip_file,args.destination)
