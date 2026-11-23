"""Разрешить все соотношения сторон (в т.ч. 21:9) в окне выбора разрешения Unity.
Точечный патч: меняются только байты флагов внутри PlayerSettings."""
import sys, UnityPy

src, dst = sys.argv[1], sys.argv[2]
orig = bytearray(open(src, "rb").read())


def player_settings(env):
    return next(o for o in env.objects if o.type.name == "PlayerSettings")


# 1) пересобираем копию через UnityPy, только чтобы получить новые байты объекта
env = UnityPy.load(bytes(orig))
ps = player_settings(env)
raw = bytes(ps.get_raw_data())
base = ps.byte_start
t = ps.read_typetree()
t["m_SupportedAspectRatios"] = {k: True for k in t["m_SupportedAspectRatios"]}
ps.save_typetree(t)
new = bytes(player_settings(UnityPy.load(env.file.save())).get_raw_data())

# 2) переносим отличающиеся байты в оригинальный файл
assert len(raw) == len(new), "размер объекта изменился"
diffs = [i for i in range(len(raw)) if raw[i] != new[i]]
print("diffs in object:", diffs, [raw[i] for i in diffs], "->", [new[i] for i in diffs])
assert 0 < len(diffs) <= 5 and diffs[-1] - diffs[0] < 5
assert orig[base:base + len(raw)] == raw
for i in diffs:
    orig[base + i] = new[i]
open(dst, "wb").write(orig)

print("check:", player_settings(UnityPy.load(dst)).read_typetree()["m_SupportedAspectRatios"])
