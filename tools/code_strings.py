"""Строковые литералы из декомпилированного кода, похожие на текст интерфейса."""
import sys, os, re, json
from collections import Counter

src, out = sys.argv[1], sys.argv[2]
lit = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
skip_files = re.compile(r'(Steamworks|UnityStandardAssets|NativeMethods|InteropHelp|websocket|YamlDotNet|Post)', re.I)
skip_line = re.compile(r'(Debug\.Log|Resources\.Load|GetComponent|Find\(|PlayerPrefs|\[DllImport|GetFloat|SetFloat|GetInt|SetInt|Shader|Animator|\.Play\(|Invoke\(|SendMessage|File\.|Path\.|Directory\.|LoadAll|StartCoroutine|case "|== "|!= "|Contains\("|Split\(|Replace\(|EndsWith|StartsWith|throw new)')

strings = Counter()
where = {}
for root, _, files in os.walk(src):
    for fn in files:
        if not fn.endswith(".cs") or skip_files.search(root + fn):
            continue
        for ln in open(os.path.join(root, fn), encoding="utf-8", errors="replace"):
            if skip_line.search(ln):
                continue
            for m in lit.finditer(ln):
                s = m.group(1).encode().decode("unicode_escape", errors="ignore") if "\\" in m.group(1) else m.group(1)
                core = s.strip()
                if len(core) < 2 or not re.search(r"[A-Za-z]{2}", core):
                    continue
                if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", core) and not re.fullmatch(r"[A-Z][a-z]+|[a-z]+", core):
                    continue  # идентификаторы вида camelCase/snake_case
                if re.search(r"[/\\]|\.(png|txt|json|yaml|wav|mtl|obj)\b|^#|http", core):
                    continue
                strings[s] += 1
                where.setdefault(s, fn)
res = [{"s": s, "f": where[s]} for s in sorted(strings)]
json.dump(res, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=0)
print(len(res))
