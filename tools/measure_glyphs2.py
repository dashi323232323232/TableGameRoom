import sys
from PIL import Image
from collections import deque

def find_title_bbox(px, W, H):
    # 逐行统计 #202A30 附近像素数量，找标题栏行
    rows = []
    for y in range(H):
        n = 0
        for x in range(0, W, 4):
            r, g, b = px[x, y]
            if abs(r - 32) <= 6 and abs(g - 42) <= 6 and abs(b - 48) <= 6:
                n += 1
        rows.append(n)
    best = max(rows)
    if best < 50:
        return None
    ys = [y for y, n in enumerate(rows) if n >= best * 0.8]
    top, bot = min(ys), max(ys)
    # 列范围：在该行区间内找深色列
    cols = []
    for x in range(W):
        n = 0
        for y in range(top, bot + 1):
            r, g, b = px[x, y]
            if abs(r - 32) <= 6 and abs(g - 42) <= 6 and abs(b - 48) <= 6:
                n += 1
        cols.append(n)
    thr = (bot - top + 1) * 0.8
    xs = [x for x, n in enumerate(cols) if n >= thr]
    return (min(xs), top, max(xs), bot, best)

def measure(path, tag):
    im = Image.open(path).convert('RGB')
    W, H = im.size
    px = im.load()
    bb = find_title_bbox(px, W, H)
    print(f'[{tag}] image={W}x{H} titleBBox={bb}')
    if not bb:
        return
    x0, y0, x1, y1, _ = bb
    print(f'  title bar: x={x0}..{x1} ({x1-x0+1}px) y={y0}..{y1} ({y1-y0+1}px)')
    # 按钮区：右侧 110px
    rx0 = max(x0, x1 - 110)
    seen = set()
    comps = []
    for x in range(rx0, x1 + 1):
        for y in range(y0, y1 + 1):
            if (x, y) in seen:
                continue
            r, g, b = px[x, y]
            if not (r >= 170 and g >= 170 and b >= 170):
                continue
            q = deque([(x, y)]); seen.add((x, y))
            n = 0; sx = sy = 0
            minx = maxx = x; miny = maxy = y
            while q:
                cx, cy = q.popleft(); n += 1; sx += cx; sy += cy
                minx = min(minx, cx); maxx = max(maxx, cx)
                miny = min(miny, cy); maxy = max(maxy, cy)
                for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                    nx, ny = cx + dx, cy + dy
                    if not (rx0 <= nx <= x1 and y0 <= ny <= y1): continue
                    if (nx, ny) in seen: continue
                    rr, gg, bbb = px[nx, ny]
                    if rr >= 170 and gg >= 170 and bbb >= 170:
                        seen.add((nx, ny)); q.append((nx, ny))
            if n >= 2:
                comps.append(dict(n=n, x=minx, y=miny, w=maxx-minx+1, h=maxy-miny+1,
                                  cx=round(sx/n, 1), cy=round(sy/n, 1)))
    comps.sort(key=lambda c: c['x'])
    for c in comps:
        print('  glyph n={n} bbox=({x},{y}) {w}x{h} center=({cx},{cy}) relY={ry}'.format(ry=round(c['cy']-y0,1), **c))
    print(f'  glyphCount={len(comps)}')

for path, tag in ((sys.argv[1], sys.argv[2]), (sys.argv[3], sys.argv[4])):
    measure(path, tag)
