import sys
src, dst = sys.argv[1], sys.argv[2]
data = open(src, "rb").read()
a = data.count(b"GSQE78")
b = data.count(b"dca4f72b57e8bf3f540e694f01cc23ef687d2b25028d974b67356e15f1c2e009")
data = data.replace(b"GSQE78", b"GBQE78")
data = data.replace(b"dca4f72b57e8bf3f540e694f01cc23ef687d2b25028d974b67356e15f1c2e009",
                    b"5f73b291a1b8c33a7cebda40638f00f74ff7faf3a5f3877190cd30b628ffa42d")
open(dst, "wb").write(data)
print(f"patched id={a} hash={b} -> {dst}")
