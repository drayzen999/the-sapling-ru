import UnityPy, sys, re, collections

e = UnityPy.load(sys.argv[1])
keys = collections.Counter()
out = set()
for o in e.objects:
    if o.type.name != "TextAsset":
        continue
    d = o.read()
    s = d.m_Script
    s = s if isinstance(s, str) else s.decode("utf-8", "replace")
    for m in re.finditer(r'"(\w+)"\s*:\s*"((?:[^"\\]|\\.)*)"', s):
        k, v = m.group(1), m.group(2)
        if re.search(r"[A-Za-z]{3,} [A-Za-z]{2,}", v) or k in ("name", "description", "title"):
            keys[k] += 1
            out.add((d.m_Name, k, v))
print(keys)
for n, k, v in sorted(out):
    print(n, "|", k, "|", v)
