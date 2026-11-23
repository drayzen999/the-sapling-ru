"""Статические Muli Regular/Light из вариативного Mulish (это переименованный Muli, но с кириллицей).
Семейство называется "Muli", чтобы игра подхватила его как системный запасной шрифт."""
import sys
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

src, outdir = sys.argv[1], sys.argv[2]
for style, wght in (("Regular", 400), ("Light", 300)):
    f = instancer.instantiateVariableFont(TTFont(src), {"wght": wght})
    name = f["name"]
    for rec in list(name.names):
        if rec.nameID in (1, 2, 3, 4, 6, 16, 17, 21, 22, 25):
            name.removeNames(nameID=rec.nameID)
    fam = "Muli" if style == "Regular" else "Muli Light"
    for pid, eid, lid in ((3, 1, 0x409), (1, 0, 0)):
        name.setName(fam, 1, pid, eid, lid)
        name.setName("Regular", 2, pid, eid, lid)
        name.setName(f"Muli-{style} (Mulish)", 3, pid, eid, lid)
        name.setName(f"Muli {style}" if style != "Regular" else "Muli", 4, pid, eid, lid)
        name.setName(f"Muli-{style}", 6, pid, eid, lid)
        if style != "Regular":
            name.setName("Muli", 16, pid, eid, lid)
            name.setName(style, 17, pid, eid, lid)
    f["OS/2"].usWeightClass = wght
    f["OS/2"].fsSelection = (f["OS/2"].fsSelection & ~0b1100001) | 0b1000000  # REGULAR
    f["head"].macStyle = 0
    f.save(f"{outdir}/Muli-{style}.ttf")
    print("ok", style)
