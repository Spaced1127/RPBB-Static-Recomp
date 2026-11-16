#!/usr/bin/env python3
"""Offline check of a GameCube raw memory card image.

Verifies the header and directory checksums, lists save entries and follows the
BAT chain for each save. Host-side only; it reads a file and prints a report.

Usage: python tools/check_card_image.py <MemoryCardA.USA.raw>
"""

import pathlib
import sys

BLOCK = 0x2000
ENTRIES = 127
ENTRY_SIZE = 0x40
DIRECTORY_OFFSET = BLOCK
BAT_OFFSETS = (3 * BLOCK, 4 * BLOCK)
BAT_MAP_OFFSET = 0x0A
FIRST_DATA_BLOCK = 5

# Header: checksum area starts after the two checksum words and ends at 0x01FC.
HEADER_CSUM_START = 0x0000
HEADER_CSUM_END = 0x01FC
HEADER_CSUM_STORED = 0x01FC
# Directory: entries start at the block base, checksums at block + 0x1FFC.
DIRECTORY_CSUM_START = 0x0000
DIRECTORY_CSUM_END = 0x1FFC
DIRECTORY_CSUM_STORED = 0x1FFC


def checksums(data: bytes):
    csum = 0
    inv = 0
    for i in range(0, len(data) - 1, 2):
        value = (data[i] << 8) | data[i + 1]
        csum = (csum + value) & 0xFFFF
        inv = (inv + (value ^ 0xFFFF)) & 0xFFFF
    if csum == 0xFFFF:
        csum = 0
    if inv == 0xFFFF:
        inv = 0
    return csum, inv


def be16(data: bytes, offset: int) -> int:
    return (data[offset] << 8) | data[offset + 1]


def le16(data: bytes, offset: int) -> int:
    return data[offset] | (data[offset + 1] << 8)


def check_block(label: str, block: bytes, start: int, end: int, stored: int):
    want = checksums(block[start:end])
    have = (be16(block, stored), be16(block, stored + 2))
    print(f"{label} checksum: stored={have[0]:04x}/{have[1]:04x} "
          f"computed={want[0]:04x}/{want[1]:04x} "
          f"{'OK' if want == have else 'MISMATCH'}")
    return want == have


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    path = pathlib.Path(sys.argv[1])
    image = path.read_bytes()
    if len(image) < FIRST_DATA_BLOCK * BLOCK:
        print(f"not a raw card: {len(image)} bytes")
        return 1

    print(f"image: {path}")
    print(f"size:  {len(image):,} bytes ({len(image) // BLOCK} blocks)")

    ok = True
    ok &= check_block("header", image, HEADER_CSUM_START, HEADER_CSUM_END,
                      HEADER_CSUM_STORED)
    directory_start = DIRECTORY_OFFSET
    ok &= check_block("directory", image, directory_start + DIRECTORY_CSUM_START,
                      directory_start + DIRECTORY_CSUM_END,
                      directory_start + DIRECTORY_CSUM_STORED)

    directory = image[directory_start:directory_start + BLOCK]
    bats = [image[o:o + BLOCK] for o in BAT_OFFSETS]

    used = 0
    for index in range(ENTRIES):
        entry = directory[index * ENTRY_SIZE:(index + 1) * ENTRY_SIZE]
        gamecode = entry[0:4]
        if gamecode == b"\xff\xff\xff\xff" or gamecode == b"\x00\x00\x00\x00":
            continue
        used += 1
        company = entry[4:6]
        filename = entry[8:0x28].split(b"\x00", 1)[0].decode("ascii", "replace")
        first = be16(entry, 0x36)
        count = be16(entry, 0x38)

        found = None
        for bat_index, bat in enumerate(bats):
            chain = []
            block = first
            while block != 0xFFFF and len(chain) <= 2048:
                chain.append(block)
                if block < FIRST_DATA_BLOCK:
                    break
                offset = BAT_MAP_OFFSET + (block - FIRST_DATA_BLOCK) * 2
                if offset + 1 >= len(bat):
                    break
                block = be16(bat, offset)
            if first >= FIRST_DATA_BLOCK and len(chain) == count:
                found = (bat_index, chain)
                break
        chain_ok = found is not None
        chain = found[1] if found else []
        bat_label = str(found[0]) if found else "-"
        ok &= chain_ok
        print(f"slot {index + 1}: game={gamecode.decode('ascii', 'replace')} "
              f"company={company.decode('ascii', 'replace')} name={filename!r} "
              f"first={first} blocks={count} chain={len(chain)} bat={bat_label} "
              f"{'OK' if chain_ok else 'CHAIN MISMATCH'}")

    if not used:
        print("no save entries on this card")
    print("RESULT:", "OK" if ok else "ISSUES FOUND")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())


