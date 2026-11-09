#!/usr/bin/env python3
"""Build a ModernGekko game root from a GameCube ISO.

Produces the same layout Dolphin's DiscExtractor writes:

    <root>/sys/boot.bin         0x0000 .. 0x0440
    <root>/sys/bi2.bin          0x0440 .. 0x2440
    <root>/sys/apploader.img    0x2440 .. 0x2440 + 0x20 + size + trailer
    <root>/sys/main.dol         DOL blob (header + sections)
    <root>/sys/fst.bin          FST table
    <root>/files/               extracted FST tree

Usage:
    python tools/make_game_dir.py <game.iso> <game-root>
"""
from __future__ import annotations

import struct
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

from gcdisc import read_dol, parse_fst, walk  # noqa: E402

BOOT_SIZE = 0x440
BI2_ADDRESS = 0x440
BI2_SIZE = 0x2000
APPLOADER_ADDRESS = 0x2440
APPLOADER_HEADER_SIZE = 0x20


def u32be(iso: bytes, off: int) -> int:
    return struct.unpack_from(">I", iso, off)[0]


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    iso_path, root = Path(sys.argv[1]), Path(sys.argv[2])
    iso = iso_path.read_bytes()

    dol, dol_blob = read_dol(iso_path)

    app_size = u32be(iso, APPLOADER_ADDRESS + 0x14)
    app_trailer = u32be(iso, APPLOADER_ADDRESS + 0x18)
    app_total = APPLOADER_HEADER_SIZE + app_size + app_trailer

    fst_off = u32be(iso, 0x424)
    fst_size = u32be(iso, 0x428)

    sys_dir = root / "sys"
    files_dir = root / "files"
    sys_dir.mkdir(parents=True, exist_ok=True)
    files_dir.mkdir(parents=True, exist_ok=True)

    (sys_dir / "boot.bin").write_bytes(iso[0:BOOT_SIZE])
    (sys_dir / "bi2.bin").write_bytes(iso[BI2_ADDRESS:BI2_ADDRESS + BI2_SIZE])
    (sys_dir / "apploader.img").write_bytes(
        iso[APPLOADER_ADDRESS:APPLOADER_ADDRESS + app_total]
    )
    (sys_dir / "main.dol").write_bytes(dol_blob)
    (sys_dir / "fst.bin").write_bytes(iso[fst_off:fst_off + fst_size])

    fst_root, _, _ = parse_fst(iso_path)
    count = 0
    total = 0
    for path, e in walk(fst_root):
        if e.is_dir:
            continue
        dest = files_dir / path
        dest.parent.mkdir(parents=True, exist_ok=True)
        data = iso[e.offset:e.offset + e.size]
        if len(data) != e.size:
            raise ValueError(f"truncated file {path}")
        dest.write_bytes(data)
        count += 1
        total += e.size

    print(f"Game root:   {root}")
    print(f"boot.bin     {BOOT_SIZE} bytes")
    print(f"bi2.bin      {BI2_SIZE} bytes")
    print(f"apploader.img {app_total} bytes (size {app_size:#x} + trailer {app_trailer:#x})")
    print(f"main.dol     {len(dol_blob)} bytes, entry {dol.entry_point:#010x}")
    print(f"fst.bin      {fst_size} bytes at {fst_off:#x}")
    print(f"files/       {count} files, {total:,} bytes")


if __name__ == "__main__":
    main()