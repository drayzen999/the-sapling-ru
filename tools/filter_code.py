import json, re, sys
from collections import defaultdict

src, out = sys.argv[1], sys.argv[2]
items = json.load(open(src, encoding="utf-8"))
skip_files = re.compile(r"NameGenerator|AnimationSpecializer|OrganismDescriber|ObjectFileImporter|TextureFactory|"
                        r"MeshBuilder|ObjectBuilder|SaveGame|Serializ|Yaml|Steam|Json|Debug|Test|Cheat|Shader|Audio|Sound|Conductor|Music",
                        re.I)
by_file = defaultdict(list)
for it in items:
    s, f = it["s"], it["f"]
    if skip_files.search(f):
        continue
    core = s.strip()
    # одиночные строчные слова без пробелов почти всегда идентификаторы ресурсов/частей тела
    if re.fullmatch(r"[a-z]+", core) and len(core) < 4:
        continue
    if re.search(r"[{}\[\]=<>]", core) and not re.search(r"<(b|i|size|color)", core):
        continue
    by_file[f].append(s)
with open(out, "w", encoding="utf-8") as fo:
    n = 0
    for f in sorted(by_file):
        fo.write(f"## {f}\n")
        for s in by_file[f]:
            fo.write(json.dumps(s, ensure_ascii=False) + "\n")
            n += 1
print(n, "strings in", len(by_file), "files")
