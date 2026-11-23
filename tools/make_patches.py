"""Патчи формата SRUP1: bsdiff (control/diff/extra) + gzip, применяются установщиком на .NET Framework.
usage: make_patches.py <work_dir> <out_dir>"""
import bsdiff4.core, gzip, hashlib, io, json, os, struct, sys

w, out = sys.argv[1], sys.argv[2]
pairs = {
    "globalgamemanagers": (r"backup\globalgamemanagers", r"globalgamemanagers.patched"),
    "sharedassets0.assets": (r"backup\sharedassets0.assets", r"patched\sharedassets0.assets"),
    "resources.assets": (r"backup\resources.assets", r"patched\resources.assets"),
}
sha = lambda b: hashlib.sha256(b).hexdigest()
manifest = []
os.makedirs(out, exist_ok=True)
for name, (a, b) in pairs.items():
    src = open(os.path.join(w, a), "rb").read()
    dst = open(os.path.join(w, b), "rb").read()
    control, diff, extra = bsdiff4.core.diff(src, dst)
    body = io.BytesIO()
    # control — тройки чисел bsdiff (8 байт, little-endian, знак в старшем бите)
    for blob in (control, diff, extra):
        body.write(struct.pack("<q", len(blob)))
        body.write(blob)
    data = b"SRUP1" + gzip.compress(body.getvalue(), 9)
    open(os.path.join(out, name + ".srup"), "wb").write(data)
    manifest.append({"file": name, "src": sha(src), "dst": sha(dst), "size": len(dst)})
    print(name, len(data))
json.dump(manifest, open(os.path.join(out, "manifest.json"), "w"), indent=1)
