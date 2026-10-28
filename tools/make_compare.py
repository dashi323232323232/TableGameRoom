from PIL import Image, ImageDraw, ImageFont
import os

base = r'D:\dsh\xin\tools\shots'
before = Image.open(os.path.join(base, 'title-before-full.png')).convert('RGB')
after = Image.open(os.path.join(base, 'title-after-full.png')).convert('RGB')
print('before', before.size, 'after', after.size)
W = 1216
font_path = r'C:\Windows\Fonts\msyh.ttc'
def F(sz): return ImageFont.truetype(font_path, sz)

lab_h = 36
strip_h = 72
zoom = 4
b_crop = before.crop((215, 6, 335, 66))   # old buttons clustered after title
a_crop = after.crop((1100, 0, 1216, 60))  # new buttons right-aligned
zb = b_crop.resize((b_crop.width * zoom, b_crop.height * zoom), Image.NEAREST)
za = a_crop.resize((a_crop.width * zoom, a_crop.height * zoom), Image.NEAREST)
zoom_row_h = max(zb.height, za.height) + lab_h

H = (lab_h + strip_h) * 2 + (lab_h + zoom_row_h) * 2 + 10
canvas = Image.new('RGB', (W, H), (245, 245, 245))
d = ImageDraw.Draw(canvas)

def label(y, text, bg=(30, 30, 30)):
    d.rectangle([0, y, W, y + lab_h - 1], fill=bg)
    d.text((12, y + 7), text, font=F(18), fill=(255, 255, 255))

y = 0
label(y, 'BEFORE 旧版：窗口顶部 6px 白线，按钮 48×42 且紧贴标题文字右侧排列（未贴右），图标为字体字符（— 14×1 / × 约 6×6）')
y += lab_h
b_strip = before.crop((0, 0, W, strip_h))
canvas.paste(b_strip, (0, y))
d.rectangle([229, y + 15, 325, y + 57], outline=(255, 0, 0), width=2)
y += strip_h

label(y, 'AFTER 新版：无白线（客户区覆盖整个窗口），按钮 46×42 右对齐贴边，矢量图标 10×10 且几何居中')
y += lab_h
a_strip = after.crop((0, 0, W, strip_h))
canvas.paste(a_strip, (0, y))
d.rectangle([1116, y + 8, 1208, y + 50], outline=(255, 0, 0), width=2)
y += strip_h

label(y, '按钮 4× 放大 · BEFORE：— 细长横线，× 明显偏小，两者笔画与包围盒不一致', bg=(120, 30, 30))
y += lab_h
canvas.paste(zb, (8, y))
y += zoom_row_h

label(y, '按钮 4× 放大 · AFTER：10×10 统一矢量描边，横线与叉号等宽等高，均居中', bg=(30, 100, 50))
y += lab_h
canvas.paste(za, (8, y))

out = os.path.join(base, 'title-compare.png')
canvas.save(out)
print('saved', out, canvas.size)
