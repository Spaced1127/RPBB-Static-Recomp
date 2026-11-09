import struct

dol_path = r"H:\Projects\RPBB-GCN-Recomp\work\game\sys\main.dol"
data = open(dol_path, "rb").read()

text_offsets = struct.unpack_from(">7I", data, 0x00)
data_offsets = struct.unpack_from(">11I", data, 0x1C)
text_addrs = struct.unpack_from(">7I", data, 0x48)
data_addrs = struct.unpack_from(">11I", data, 0x64)
text_sizes = struct.unpack_from(">7I", data, 0x90)
data_sizes = struct.unpack_from(">11I", data, 0xAC)
print("text offsets", [hex(x) for x in text_offsets])
print("text addrs", [hex(x) for x in text_addrs])
print("text sizes", [hex(x) for x in text_sizes])
print("data addrs", [hex(x) for x in data_addrs])
print("data sizes", [hex(x) for x in data_sizes])

sections = []
for i in range(7):
    if text_sizes[i]:
        sections.append((f"text{i}", text_offsets[i], text_addrs[i], text_sizes[i]))
for i in range(11):
    if data_sizes[i]:
        sections.append((f"data{i}", data_offsets[i], data_addrs[i], data_sizes[i]))

patterns = {
    "float 1/60": struct.pack(">f", 1.0 / 60.0),
    "float 1/30": struct.pack(">f", 1.0 / 30.0),
    "float 1/120": struct.pack(">f", 1.0 / 120.0),
    "float 60": struct.pack(">f", 60.0),
    "float 30": struct.pack(">f", 30.0),
    "float 120": struct.pack(">f", 120.0),
    "double 1/60": struct.pack(">d", 1.0 / 60.0),
    "double 1/30": struct.pack(">d", 1.0 / 30.0),
    "double 1/120": struct.pack(">d", 1.0 / 120.0),
    "double 60": struct.pack(">d", 60.0),
}

for name, off, addr, size in sections:
    blob = data[off:off + size]
    for label, pat in patterns.items():
        start = 0
        while True:
            idx = blob.find(pat, start)
            if idx < 0:
                break
            print(f"{name} 0x{addr + idx:08X}  {label}")
            start = idx + 1
