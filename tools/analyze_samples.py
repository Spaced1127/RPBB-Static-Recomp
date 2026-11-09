import argparse
import bisect
import collections
import csv
import re


def parse_map(path, image_base):
    syms = []
    pat = re.compile(r"^\s*[0-9A-Fa-f]{4}:[0-9A-Fa-f]{8}\s+(\S+)\s+([0-9A-Fa-f]{16})\b")
    with open(path, "r", errors="replace") as f:
        for line in f:
            m = pat.match(line)
            if not m:
                continue
            name = m.group(1)
            addr = int(m.group(2), 16)
            if addr < image_base:
                continue
            syms.append((addr - image_base, name))
    syms.sort()
    out = []
    seen = set()
    for rva, name in syms:
        if rva in seen:
            continue
        seen.add(rva)
        out.append((rva, name))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--samples", required=True)
    ap.add_argument("--map", required=True)
    ap.add_argument("--image-base", default="180000000")
    ap.add_argument("--module-name", default="gGBQE78_recomp.dll")
    ap.add_argument("--top", type=int, default=40)
    args = ap.parse_args()

    image_base = int(args.image_base, 16)
    syms = parse_map(args.map, image_base)
    addrs = [a for a, _ in syms]
    print(f"map symbols: {len(syms)} for {args.module_name}")

    counts = collections.Counter()
    exact = collections.Counter()
    module_counts = collections.Counter()
    total = 0
    with open(args.samples, newline="") as f:
        r = csv.DictReader(f)
        for row in r:
            total += 1
            mod = row["module"]
            module_counts[mod] += 1
            if mod.lower() == args.module_name.lower():
                off = int(row["offset"], 16)
                i = bisect.bisect_right(addrs, off) - 1
                if i >= 0:
                    base, name = syms[i]
                    counts[name] += 1
                    exact[(name, off - base)] += 1
                else:
                    name = f"<module+0x{off:X}>"
                    counts[name] += 1

    print(f"total samples: {total}")
    print("\nby module:")
    for mod, n in module_counts.most_common():
        print(f"  {n:8d}  {100.0*n/total:6.2f}%  {mod}")

    print(f"\ntop symbols in {args.module_name}:")
    for name, n in counts.most_common(args.top):
        print(f"  {n:8d}  {100.0*n/total:6.2f}%  {name}")

    print("\ntop exact symbol+delta:")
    for (name, delta), n in exact.most_common(args.top):
        print(f"  {n:8d}  {100.0*n/total:6.2f}%  {name}+0x{delta:X}")


if __name__ == "__main__":
    main()
