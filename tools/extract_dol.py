#!/usr/bin/env python3
"""Extract the raw main.dol blob from a GameCube ISO.

Delegates to gcdisc.read_dol (same tool used for inventory), which reads the
DOL pointer from the disc and slices the contiguous DOL blob (header + all
sections). The output is byte-for-byte the main.dol that DolRecomp and Dolphin
consume.

Usage:
    python tools/extract_dol.py <game.iso> <out/main.dol>
"""
from __future__ import annotations

import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))

from gcdisc import read_dol  # noqa: E402


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    iso_path, out_path = Path(sys.argv[1]), Path(sys.argv[2])

    dol, blob = read_dol(iso_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(blob)
    print(
        f"Wrote {out_path} ({len(blob)} bytes), entry 0x{blob[0xE0]:02x}{blob[0xE1]:02x}{blob[0xE2]:02x}{blob[0xE3]:02x}, "
        f"{len(dol.sections)} sections, bss 0x{dol.bss_address:x}+0x{dol.bss_size:x}"
    )


if __name__ == "__main__":
    main()