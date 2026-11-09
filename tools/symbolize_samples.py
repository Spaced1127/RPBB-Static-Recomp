import argparse
import collections
import csv
import os
import re
import subprocess
import sys

SYMBOLIZER = r"C:\Program Files\LLVM\bin\llvm-symbolizer.exe"
IMAGE_BASE = 0x180000000


def split_blocks(text):
    blocks = []
    cur = []
    for line in text.splitlines():
        if line.strip() == "":
            if cur:
                blocks.append(cur)
                cur = []
        else:
            cur.append(line)
    if cur:
        blocks.append(cur)
    return blocks


def parse_block(block):
    frames = []
    for i in range(0, len(block) - 1, 2):
        frames.append((block[i], block[i + 1]))
    if len(block) % 2 == 1:
        frames.append((block[-1], "??:0:0"))
    return frames


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--samples", required=True)
    ap.add_argument("--dll", required=True)
    ap.add_argument("--module-name", default="gGBQE78_recomp.dll")
    ap.add_argument("--top", type=int, default=40)
    args = ap.parse_args()

    counts = collections.Counter()
    total = 0
    with open(args.samples, newline="") as f:
        for row in csv.DictReader(f):
            total += 1
            if row["module"].lower() == args.module_name.lower():
                counts[int(row["offset"], 16)] += 1

    print(f"total samples {total}, module samples {sum(counts.values())}, unique RVAs {len(counts)}")
    rvas = sorted(counts)
    addr_lines = "\n".join(f"0x{IMAGE_BASE + rva:X}" for rva in rvas) + "\n"
    proc = subprocess.run([SYMBOLIZER, f"--obj={args.dll}", "--functions=short", "--demangle"],
                          input=addr_lines, text=True, capture_output=True)
    if proc.returncode != 0:
        print("symbolizer failed:", proc.stderr[:2000])
        sys.exit(1)
    blocks = split_blocks(proc.stdout)
    print(f"symbolizer blocks {len(blocks)} for {len(rvas)} addresses")
    if len(blocks) != len(rvas):
        print("warning: block/address count mismatch; pairing by order anyway")

    file_cache = {}
    inner_counts = collections.Counter()
    outer_counts = collections.Counter()
    guest_counts = collections.Counter()
    guest_ops = collections.Counter()
    missing = 0
    for i, rva in enumerate(rvas):
        n = counts[rva]
        block = blocks[i] if i < len(blocks) else []
        frames = parse_block(block)
        if frames:
            inner_counts[frames[0][0]] += n
            outer_counts[frames[-1][0]] += n
        else:
            inner_counts["??"] += n
            missing += n
            continue

        chosen = None
        # Prefer a frame in a generated chunk with a real line.
        for func, loc in frames:
            m = re.match(r"^(.*?):(\d+):(\d+)$", loc)
            if not m:
                continue
            path, lineno = m.group(1), int(m.group(2))
            if lineno > 0 and ("generated/chunks" in path.replace("\\", "/") or
                               path.replace("\\", "/").endswith("generated.h")):
                chosen = (path, lineno)
                break
        if chosen is None:
            # Fall back to any frame with a real line in generated code.
            for func, loc in frames:
                m = re.match(r"^(.*?):(\d+):(\d+)$", loc)
                if not m:
                    continue
                path, lineno = m.group(1), int(m.group(2))
                if lineno > 0 and "generated" in path.replace("\\", "/"):
                    chosen = (path, lineno)
                    break
        if chosen is None:
            missing += n
            continue
        path, lineno = chosen
        if path not in file_cache:
            try:
                file_cache[path] = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except OSError:
                file_cache[path] = []
        src = file_cache[path]
        pc = op = None
        for j in range(lineno - 1, max(-1, lineno - 15), -1):
            if j < 0 or j >= len(src):
                continue
            cm = re.search(r"//\s*([0-9A-Fa-f]{8}):\s*(\S+)", src[j])
            if cm:
                pc, op = cm.group(1).upper(), cm.group(2)
                break
        if pc:
            guest_counts[pc] += n
            guest_ops[(pc, op)] += n
        else:
            missing += n

    print(f"samples without guest PC: {missing} ({100.0*missing/total:.1f}%)")
    print("\ntop innermost functions:")
    for func, n in inner_counts.most_common(args.top):
        print(f"  {n:8d} {100.0*n/total:6.2f}%  {func}")
    print("\ntop outer functions:")
    for func, n in outer_counts.most_common(args.top):
        print(f"  {n:8d} {100.0*n/total:6.2f}%  {func}")
    print("\ntop guest PCs:")
    for pc, n in guest_counts.most_common(args.top):
        opname = ""
        for (p, o), c in guest_ops.items():
            if p == pc:
                opname = o
                break
        print(f"  {n:8d} {100.0*n/total:6.2f}%  0x{pc} {opname}")


if __name__ == "__main__":
    main()
