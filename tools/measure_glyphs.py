import sys
from PIL import Image
from collections import deque

def measure(path, tag):
    im = Image.open(path).convert('RGB')
    W, H = im.size
    px = im.load()
    # 找标题栏顶部：中心列第一个深色像素
    title_top = None
    for y in range(min(60, H)):
        r, g, b = px[W // 2, y]
        if r < 60 and g < 70 and b < 80:
            title_top = y
            break
    print(f'[{tag}] image={W}x{H} titleTop={title_top}')
    if title_top is None:
        return
    x0, x1 = W - 120, W - 2
    y0, y1 = title_top, min(title_top + 44, H - 1)
    seen = set()
    comps = []
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            if (x, y) in seen:
                continue
            r, g, b = px[x, y]
            if not (r >= 190 and g >= 190 and b >= 190):
                continue
            q = deque([(x, y)])
            seen.add((x, y))
            n = 0
            sx = sy = 0
            minx = maxx = x
            miny = maxy = y
            while q:
                cx, cy = q.popleft()
                n += 1
                sx += cx
                sy += cy
                minx = min(minx, cx); maxx = max(maxx, cx)
                miny = min(miny, cy); maxy = max(maxy, cy)
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if not (x0 <= nx <= x1 and y0 <= ny <= y1):
                        continue
                    if (nx, ny) in seen:
                        continue
                    rr, gg, bb = px[nx, ny]
                    if rr >= 190 and gg >= 190 and bb >= 190:
                        seen.add((nx, ny))
                        q.append((nx, ny))
            if n >= 3:
                comps.append(dict(n=n, x=minx, y=miny, w=maxx - minx + 1, h=maxy - miny + 1,
                                  cx=round(sx / n, 1), cy=round(sy / n, 1)))
    comps.sort(key=lambda c: c['x'])
    for c in comps:
        print('  glyph n={n} bbox=({x},{y}) {w}x{h} center=({cx},{cy})'.format(**c))
    print(f'  glyphCount={len(comps)}')
    # 相对标题栏的纵向中心
    for c in comps:
        print(f"    rel-center-y={round(c['cy'] - title_top, 1)}")

for path, tag in ((sys.argv[1], sys.argv[2]), (sys.argv[3], sys.argv[4])):
    measure(path, tag)
