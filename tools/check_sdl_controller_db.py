#!/usr/bin/env python3
"""Audit the shipped SDL controller database against the canonical system.

Reads ModernGekko's vendored SDL_gamepad_db.h and reports:
  - rows whose live ``a`` binding sorts after ``b`` (informational: the class
    rule no longer uses it, but a Microsoft row with a>b must still be zero),
  - rows whose ``dpleft`` is a higher raw button than ``dpright`` (normal for
    many pads; the canonical system never reads this),
  - the 8BitDo / Switch-mode GUID fallback pairs and 8BitDo candidates observed
    in the database (validate with a real pad before adding one).

Usage: python tools/check_sdl_controller_db.py [--db PATH]
"""

import argparse
import pathlib
import re

DEFAULT_DB = pathlib.Path(
    r"H:\Projects\SB-RFD\lib\ModernGekko-Template\lib\ModernGekko"
    r"\vendor\dolphin\Externals\SDL\SDL\src\joystick\SDL_gamepad_db.h"
)

ROW = re.compile(r'"([0-9a-fA-F]{32}),([^,]+),(.*)"')
FAMILIES = {
    0x045E: "Microsoft / Xbox",
    0x054C: "Sony / PlayStation",
    0x057E: "Nintendo",
    0x2DC8: "8BitDo",
    0x05AC: "8BitDo (Bluetooth)",
}


def bindings(mapping):
    out = {}
    for item in mapping.split(","):
        if ":" in item:
            key, value = item.split(":", 1)
            out[key.strip()] = value.strip()
    return out


def button_index(value):
    match = re.fullmatch(r"b(\d+)", value or "")
    return int(match.group(1)) if match else None


def load_rows(path):
    rows = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        match = ROW.search(line)
        if not match:
            continue
        guid, name, mapping = match.group(1), match.group(2), match.group(3)
        vendor = int.from_bytes(bytes.fromhex(guid[8:12]), "little")
        product = int.from_bytes(bytes.fromhex(guid[16:20]), "little")
        rows.append((guid, name, mapping, vendor, product))
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", type=pathlib.Path, default=DEFAULT_DB)
    args = parser.parse_args()
    if not args.db.exists():
        raise SystemExit(f"SDL database not found: {args.db}")

    rows = load_rows(args.db)
    print(f"SDL database: {args.db}")
    print(f"rows: {len(rows)}\n")

    bad_a = []
    high_left = []
    for guid, name, mapping, vendor, product in rows:
        b = bindings(mapping)
        a_idx = button_index(b.get("a"))
        b_idx = button_index(b.get("b"))
        if a_idx is not None and b_idx is not None and a_idx > b_idx:
            bad_a.append((guid, name, vendor, product, a_idx, b_idx))
        left = button_index(b.get("dpleft"))
        right = button_index(b.get("dpright"))
        if left is not None and right is not None and left > right:
            high_left.append((guid, name, vendor, product, left, right))

    print("Rows where raw a > b (only Nintendo-labelled pads may act on this):")
    by_vendor = {}
    for row in bad_a:
        by_vendor.setdefault(row[2], []).append(row)
    for vendor, items in sorted(by_vendor.items(), key=lambda kv: -len(kv[1])):
        label = FAMILIES.get(vendor, f"vendor {vendor:04x}")
        print(f"  {label:<24} {len(items):>3} rows")
    microsoft = [row for row in bad_a if row[2] == 0x045E]
    print(f"  Microsoft rows with a>b: {len(microsoft)} (must be 0)\n")

    print("Rows where dpleft button > dpright button (NOT a reversed D-pad):")
    by_vendor = {}
    for row in high_left:
        by_vendor.setdefault(row[2], []).append(row)
    for vendor, items in sorted(by_vendor.items(), key=lambda kv: -len(kv[1]))[:8]:
        label = FAMILIES.get(vendor, f"vendor {vendor:04x}")
        print(f"  {label:<24} {len(items):>3} rows")
    print(f"  total: {len(high_left)} rows\n")

    print("8BitDo / Switch-mode GUID fallback (the only per-device knowledge):")
    # Must match IsNintendoLabelVendorPair in LayoutClass.h.
    fallback = {(0x2DC8, 0x6006), (0x05AC, 0x0004)}
    for vendor, product in sorted(fallback):
        rows_for_pair = [row for row in rows if row[3] == vendor and row[4] == product]
        state = "present in DB" if rows_for_pair else "not in DB (runtime fallback)"
        print(f"  {vendor:04x}:{product:04x}  {state}")

    candidates = sorted({(row[3], row[4]) for row in rows
                         if row[3] in (0x2DC8, 0x05AC)
                         and (row[3], row[4]) not in fallback})
    print("  other 8BitDo PIDs (validate with a real pad before adding):")
    for vendor, product in candidates[:20]:
        print(f"    {vendor:04x}:{product:04x}")
    print("\nThe canonical system has no vendor/type table. A pad is positional "
          "unless SDL reports a Switch type or its GUID pair is in the fallback, "
          "so an unlisted pad can only be wrong if it prints Nintendo labels while "
          "reporting STANDARD/UNKNOWN.")


if __name__ == "__main__":
    main()