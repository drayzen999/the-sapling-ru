"""Сырой разбор MonoBehaviour: CanvasScaler и тексты UI.Text во всех сценах/ассетах."""
import sys, os, struct, json, UnityPy
from collections import Counter

data = sys.argv[1]
out = sys.argv[2]


def script_name(obj, raw):
    # MonoBehaviour: m_GameObject PPtr(12) + m_Enabled(4 aligned) + m_Script PPtr(12)
    fid, pid = struct.unpack_from("<iq", raw, 16)
    try:
        ptr = obj.assets_file.objects  # noqa
        from UnityPy.classes import PPtr
    except Exception:
        pass
    sf = obj.assets_file
    try:
        if fid == 0:
            target = sf.objects[pid]
        else:
            ext = sf.externals[fid - 1]
            name = os.path.basename(ext.path).lower()
            tf = None
            for k, v in sf.environment.files.items():
                if os.path.basename(k).lower() == name:
                    tf = v
            target = tf.objects[pid]
        return target.read().m_ClassName
    except Exception:
        return "?"


def name_end(raw):
    n = struct.unpack_from("<i", raw, 28)[0]
    off = 32 + n
    return off + (-off % 4)


def last_string(raw):
    # m_Text — последнее поле Text: [len][bytes][align]
    end = len(raw)
    for pad in range(0, 4):
        stop = end - pad
        for start in range(stop - 4, max(-1, stop - 20000), -1):
            if start < 0:
                break
            n = struct.unpack_from("<i", raw, start)[0]
            if n == stop - start - 4 and (start + 4 + n + (-(n) % 4)) == end:
                try:
                    return raw[start + 4:stop].decode("utf-8")
                except UnicodeDecodeError:
                    return None
    return None


env = UnityPy.load(*[os.path.join(data, f) for f in os.listdir(data)
                     if (f.startswith("level") and "." not in f) or f.endswith(".assets")])
scalers = Counter()
texts = Counter()
classes = Counter()
for obj in env.objects:
    if obj.type.name != "MonoBehaviour":
        continue
    raw = obj.get_raw_data()
    cls = script_name(obj, raw)
    classes[cls] += 1
    if cls == "CanvasScaler":
        o = name_end(raw)
        mode, ppu, sf_, rx, ry, smm, match = struct.unpack_from("<iffffif", raw, o)
        scalers[(os.path.basename(obj.assets_file.name), mode, sf_, rx, ry, smm, round(match, 2))] += 1
    elif cls in ("Text", "InputField"):
        s = last_string(raw) if cls == "Text" else None
        if s and s.strip():
            texts[s] += 1

print("classes:", classes.most_common(25))
for k, v in sorted(scalers.items()):
    print("scaler", k, "x", v)
with open(out, "w", encoding="utf-8") as f:
    json.dump(sorted(texts), f, ensure_ascii=False, indent=0)
print("texts:", len(texts))
