# -*- coding: utf-8 -*-
"""Build K2D with raised Thai mark PUA glyphs for Unity legacy Text."""
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen
from fontTools.misc.transform import Transform

src = TTFont("Assets/Resources/Fonts/K2D-Regular.source.ttf")
cmap = src.getBestCmap()
glyf = src["glyf"]
hmtx = src["hmtx"]
order = list(src.getGlyphOrder())
gs = src.getGlyphSet()

# Unity places Thai marks a bit low. Raise just enough to clear the consonant
# without leaving a large floating gap (tuned for K2D UPM 1000).
RAISE_UPPER = 55
RAISE_TONE = 70
STACK_TONE = 290
LEFT = -85
LOWER = -200

# F701-F707 raised upper: ั ิ ี ึ ื ็ ํ
# F708-F70C raised tone (on base)
# F70D-F711 stacked tone (above upper vowel)
# F712-F718 upper.left (ascender bases)
# F719-F71D tone.left (ascender)
# F71E-F720 lower.low
# F721 / F722 descless bases


def clone(src_code, new_code, dx, dy):
    src_name = cmap[src_code]
    new_name = f"uni{new_code:04X}"
    pen = TTGlyphPen(gs)
    tpen = TransformPen(pen, Transform(1, 0, 0, 1, dx, dy))
    gs[src_name].draw(tpen)
    glyf[new_name] = pen.glyph()
    aw, lsb = hmtx[src_name]
    hmtx[new_name] = (aw, lsb + int(dx))
    if new_name not in order:
        order.append(new_name)
    return new_name


uppers = [0x0E31, 0x0E34, 0x0E35, 0x0E36, 0x0E37, 0x0E47, 0x0E4D]
tones = [0x0E48, 0x0E49, 0x0E4A, 0x0E4B, 0x0E4C]
lowers = [0x0E38, 0x0E39, 0x0E3A]

for i, code in enumerate(uppers):
    clone(code, 0xF701 + i, 0, RAISE_UPPER)
    clone(code, 0xF712 + i, LEFT, RAISE_UPPER)

for i, code in enumerate(tones):
    clone(code, 0xF708 + i, 0, RAISE_TONE)
    clone(code, 0xF70D + i, 0, STACK_TONE)
    clone(code, 0xF719 + i, LEFT, RAISE_TONE)

for i, code in enumerate(lowers):
    clone(code, 0xF71E + i, 0, LOWER)

clone(0x0E0D, 0xF721, 0, 0)
clone(0x0E10, 0xF722, 0, 0)

src.setGlyphOrder(order)
for table in src["cmap"].tables:
    if not hasattr(table, "cmap"):
        continue
    for code in range(0xF701, 0xF723):
        name = f"uni{code:04X}"
        if name in glyf:
            table.cmap[code] = name

out = "Assets/Resources/Fonts/K2D-Regular.ttf"
src.save(out)
v = TTFont(out)
print("pua", sum(1 for c in range(0xF700, 0xF730) if c in v.getBestCmap()))
print("saved", out)
