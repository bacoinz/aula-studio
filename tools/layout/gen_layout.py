"""Builds src/AulaStudio/Assets/f108pro-layout.json from the vendor app's layouts/rgb-keyboard.xml.

Keeps only the facts the app needs (HID code, key/light indices, grid position and hotspot centre).
Usage: python -I gen_layout.py "C:/Program Files (x86)/AULA F108Pro/layouts/rgb-keyboard.xml" out.json
"""
import json
import sys
import xml.etree.ElementTree as ET

src, out = sys.argv[1], sys.argv[2]
keys = []
for k in ET.parse(src).getroot().iter("key"):
    row, col = (int(v) for v in k.get("row_col").split("#"))
    left, top = int(k.get("rect_left")), int(k.get("rect_top"))
    w, h = int(k.get("rect_width")), int(k.get("rect_height"))
    keys.append({
        "name": k.get("name"),
        "code": int(k.get("code"), 16),
        "keyIndex": int(k.get("key_index")),
        "lightIndex": int(k.get("light_index")),
        "row": row,
        "col": col,
        "x": left + w / 2,
        "y": top + h / 2,
        "fnLayerDisabled": k.get("fnlayer_disable") == "1",
    })
keys.sort(key=lambda k: (k["row"], k["x"]))
with open(out, "w", encoding="utf-8") as f:
    json.dump({"source": "AULA F108 Pro rgb-keyboard.xml", "keys": keys}, f, ensure_ascii=False, indent=1)
print(len(keys), "keys")
