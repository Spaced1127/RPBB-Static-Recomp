import io
import os
import re
import shutil
import sys

src = r"C:\mg-pgo\gen\GBQE78\5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d-e48b3db4fc6c517f\dolrecomp-output\generated"
dst = r"C:\mg-pgo\gen-opt\GBQE78\5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d-e48b3db4fc6c517f\dolrecomp-output\generated"

if os.path.isdir(dst):
    shutil.rmtree(dst)
print("copying generated tree...")
shutil.copytree(src, dst)
print("copied")

pat = re.compile(r"^(\s*)ppc_cache_control\(ctx, [0-9]+u, ea, 0x[0-9A-Fa-f]+u\);\r?\n\s*if \(ctx->exception\) return;\r?\n", re.M)

total = 0
files = 0
chunks = os.path.join(dst, "chunks")
for name in os.listdir(chunks):
    if not name.endswith(".c"):
        continue
    path = os.path.join(chunks, name)
    text = io.open(path, encoding="utf-8").read()
    new, n = pat.subn(lambda m: m.group(1) + "(void)ea; /* cache op elided in static module */\n", text)
    if n:
        io.open(path, "w", encoding="utf-8", newline="").write(new)
        total += n
        files += 1
print(f"patched {total} cache ops in {files} files")
