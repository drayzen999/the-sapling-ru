"""Замена встроенных TTF-данных шрифтов игры на версии с кириллицей.
Montserrat 6.001 -> Montserrat 7.x (тот же дизайн + кириллица), Muli -> Mulish (переименованный Muli).
usage: patch_fonts.py <backup_dir> <fonts_dir> <out_dir>"""
import os, sys, UnityPy

backup, fonts, out = sys.argv[1:4]
font_files = {
    **{f"Montserrat-{w}": f"Montserrat-{w}.ttf" for w in
       ("Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black")},
    "Muli": "Muli-Regular.ttf",
    "Muli-Light": "Muli-Light.ttf",
}

for asset in ("sharedassets0.assets", "resources.assets"):
    env = UnityPy.load(os.path.join(backup, asset))
    changed = 0
    for obj in env.objects:
        if obj.type.name != "Font":
            continue
        t = obj.read_typetree()
        name = t["m_Name"]
        if name not in font_files:
            continue
        data = open(os.path.join(fonts, font_files[name]), "rb").read()
        old = len(t["m_FontData"])
        t["m_FontData"] = list(data)
        obj.save_typetree(t)
        changed += 1
        print(f"{asset}: {name} {old} -> {len(data)}")
    if changed:
        dst = os.path.join(out, asset)
        open(dst, "wb").write(env.file.save())
        # проверка: файл читается, данные шрифтов на месте, число объектов не изменилось
        chk = UnityPy.load(dst)
        n_old = sum(1 for _ in UnityPy.load(os.path.join(backup, asset)).objects)
        n_new = sum(1 for _ in chk.objects)
        sizes = {o.read_typetree()["m_Name"]: len(o.read_typetree()["m_FontData"]) for o in chk.objects if o.type.name == "Font"}
        print("  saved", dst, "objects", n_old, "->", n_new, sizes)
