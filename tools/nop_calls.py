import struct
import sys

src, dst, target_rva = sys.argv[1], sys.argv[2], int(sys.argv[3], 16)
data = bytearray(open(src, "rb").read())

pe = struct.unpack_from("<I", data, 0x3C)[0]
assert data[pe:pe+4] == b"PE\0\0"
nsec = struct.unpack_from("<H", data, pe + 6)[0]
opt_size = struct.unpack_from("<H", data, pe + 20)[0]
opt = pe + 24
image_base = struct.unpack_from("<Q", data, opt + 24)[0]
sec = opt + opt_size
sections = []
for i in range(nsec):
    off = sec + i * 40
    name = data[off:off+8].rstrip(b"\0").decode(errors="replace")
    vsize, va, rawsize, rawptr = struct.unpack_from("<IIII", data, off + 8)
    sections.append((name, va, vsize, rawptr, rawsize))

def va_to_off(va):
    for name, sva, vsize, rawptr, rawsize in sections:
        if sva <= va < sva + max(vsize, rawsize):
            return rawptr + (va - sva)
    return None

calls = []
for name, sva, vsize, rawptr, rawsize in sections:
    if not (name.startswith(".text")):
        continue
    blob = data[rawptr:rawptr+rawsize]
    for i in range(len(blob) - 5):
        if blob[i] == 0xE8:
            rel = struct.unpack_from("<i", blob, i + 1)[0]
            va = sva + i
            tgt = va + 5 + rel
            if tgt == target_rva:
                calls.append(va)

print(f"sections={sections}")
print(f"target_rva=0x{target_rva:X} calls={len(calls)}")
for va in calls[:30]:
    print(f"  call at RVA 0x{va:X}")
for va in calls:
    off = va_to_off(va)
    if off is not None:
        data[off:off+5] = b"\x90" * 5
open(dst, "wb").write(data)
print("wrote", dst)
