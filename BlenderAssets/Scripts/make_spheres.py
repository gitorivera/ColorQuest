import bpy, numpy as np, math, os, zlib, struct

OUT = r"C:\Users\gitor\Documents\Unity\ColorQuest\BlenderAssets"
TEX = os.path.join(OUT, "Textures")
FBXD = os.path.join(OUT, "FBX")
for d in (OUT, TEX, FBXD):
    os.makedirs(d, exist_ok=True)

W, H = 2048, 1024
RADIUS = 0.4809          # diametro 0.9617 = mismo que Bola1..15.fbx
AA = 0.004

# ---------- direcciones por pixel (mapeo UV nativo de Blender) ----------
u = (np.arange(W) + 0.5) / W
v = (np.arange(H) + 0.5) / H
U, V = np.meshgrid(u, v)
LON = (U - 0.5) * 2 * np.pi
LAT = (V - 0.5) * np.pi
DX = np.cos(LAT) * np.cos(LON)
DY = np.cos(LAT) * np.sin(LON)
DZ = np.sin(LAT)

# centros de motivo: +X y -X (ecuador). x=derecha del observador, y=arriba
FR = {1: ((1, 0, 0), (0, 1, 0)), 2: ((-1, 0, 0), (0, -1, 0))}
_ctx = {}
def ctx(k):
    if k not in _ctx:
        c, r = FR[k]
        front = (DX * c[0] + DY * c[1] + DZ * c[2]) > 0
        x = DX * r[0] + DY * r[1] + DZ * r[2]
        _ctx[k] = (x, DZ.copy(), front)
    return _ctx[k]

# ---------- SDFs ----------
def circle(x, y, cx, cy, r): return np.hypot(x - cx, y - cy) - r
def ellipse(x, y, cx, cy, a, b, rot=0.0):
    px, py = x - cx, y - cy
    c, s = math.cos(rot), math.sin(rot)
    qx, qy = px * c + py * s, -px * s + py * c
    return (np.hypot(qx / a, qy / b) - 1) * min(a, b)
def seg(x, y, ax, ay, bx, by):
    pax, pay = x - ax, y - ay
    bax, bay = bx - ax, by - ay
    h = np.clip((pax * bax + pay * bay) / (bax * bax + bay * bay), 0, 1)
    return np.hypot(pax - bax * h, pay - bay * h)
def box(x, y, cx, cy, hw, hh, r=0.0):
    qx, qy = np.abs(x - cx) - hw + r, np.abs(y - cy) - hh + r
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r
def tri(x, y, p0, p1, p2):
    d = np.minimum(np.minimum(seg(x, y, *p0, *p1), seg(x, y, *p1, *p2)), seg(x, y, *p2, *p0))
    def cr(a, b): return (b[0] - a[0]) * (y - a[1]) - (b[1] - a[1]) * (x - a[0])
    c1, c2, c3 = cr(p0, p1), cr(p1, p2), cr(p2, p0)
    inside = ((c1 >= 0) & (c2 >= 0) & (c3 >= 0)) | ((c1 <= 0) & (c2 <= 0) & (c3 <= 0))
    return np.where(inside, -d, d)
def star5(x, y, cx, cy, r, rf=0.45):
    px, py = (x - cx) / r, (y - cy) / r
    k1 = (0.809016994375, -0.587785252292); k2 = (-k1[0], k1[1])
    px = np.abs(px)
    d = np.maximum(k1[0] * px + k1[1] * py, 0); px = px - 2 * d * k1[0]; py = py - 2 * d * k1[1]
    d = np.maximum(k2[0] * px + k2[1] * py, 0); px = px - 2 * d * k2[0]; py = py - 2 * d * k2[1]
    px = np.abs(px); py = py - 1.0
    bax, bay = rf * (-k1[1]), rf * k1[0] - 1.0
    h = np.clip((px * bax + py * bay) / (bax * bax + bay * bay), 0, 1.0)
    return np.hypot(px - bax * h, py - bay * h) * np.sign(py * bax - px * bay) * r
def heart(x, y, cx, cy, s):
    px, py = np.abs((x - cx) / s), (y - cy) / s + 0.55
    d1 = np.hypot(px - 0.25, py - 0.75) - math.sqrt(2) / 4
    m = 0.5 * np.maximum(px + py, 0)
    d2 = np.sqrt(np.minimum(px ** 2 + (py - 1) ** 2, (px - m) ** 2 + (py - m) ** 2)) * np.sign(px - py)
    return np.where(px + py > 1, d1, d2) * s
def ring(sd, w): return np.abs(sd) - w
def union(*a):
    r = a[0]
    for q in a[1:]: r = np.minimum(r, q)
    return r
def sub(a, b): return np.maximum(a, -b)
def arcdown(x, y, cx, cy, r, w):
    """semicirculo inferior (sonrisa) con puntas redondeadas"""
    body = np.maximum(np.abs(np.hypot(x - cx, y - cy) - r) - w, y - cy)
    return union(body, circle(x, y, cx - r, cy, w), circle(x, y, cx + r, cy, w))
def arcup(x, y, cx, cy, r, w):
    body = np.maximum(np.abs(np.hypot(x - cx, y - cy) - r) - w, cy - y)
    return union(body, circle(x, y, cx - r, cy, w), circle(x, y, cx + r, cy, w))

# ---------- lienzo ----------
img = None
def new(base):
    global img
    img = np.ones((H, W, 3), np.float32) * np.array(base, np.float32)
def fill(col):
    global img
    img[:] = np.array(col, np.float32)
def paint(k, sd, col, alpha=1.0):
    global img
    x, y, front = ctx(k)
    a = (np.clip(0.5 - sd / AA, 0, 1) * front * alpha)[..., None]
    img = img * (1 - a) + np.array(col, np.float32) * a
def paint_all(sd, col, alpha=1.0):
    global img
    a = (np.clip(0.5 - sd / AA, 0, 1) * alpha)[..., None]
    img = img * (1 - a) + np.array(col, np.float32) * a
def angdist(e):
    e = np.array(e, np.float64); e /= np.linalg.norm(e)
    return np.arccos(np.clip(DX * e[0] + DY * e[1] + DZ * e[2], -1, 1))
def rand_dirs(n, seed, avoid=0.0):
    rng = np.random.default_rng(seed)
    out = []
    while len(out) < n:
        z = rng.uniform(-1, 1); l = rng.uniform(0, 2 * math.pi)
        e = (math.sqrt(1 - z * z) * math.cos(l), math.sqrt(1 - z * z) * math.sin(l), z)
        if abs(e[0]) > 0.0 and math.sqrt(e[1] ** 2 + e[2] ** 2) < 0.62 and abs(e[0]) > 0.7 and avoid:
            continue   # evita tapar el parche central
        out.append((e, rng.uniform(0.02, 0.05)))
    return out

WHITE = (0.98, 0.98, 0.96)
DARK = (0.12, 0.08, 0.1)
def patch(k, R=0.66, col=WHITE):
    x, y, _ = ctx(k)
    paint(k, circle(x, y, 0, 0, R + 0.012), tuple(np.array(col) * 0.8))
    paint(k, circle(x, y, 0, 0, R), col)

def face(k, cx, cy, s, col=DARK, cheeks=(1.0, 0.45, 0.55), wink=False, open_mouth=False):
    x, y, _ = ctx(k)
    paint(k, ellipse(x, y, cx - 0.36 * s, cy + 0.12 * s, 0.075 * s, 0.11 * s), col)
    if wink:
        paint(k, arcup(x, y, cx + 0.36 * s, cy + 0.12 * s, 0.09 * s, 0.025 * s), col)
    else:
        paint(k, ellipse(x, y, cx + 0.36 * s, cy + 0.12 * s, 0.075 * s, 0.11 * s), col)
        paint(k, circle(x, y, cx - 0.34 * s, cy + 0.17 * s, 0.025 * s), WHITE)
        paint(k, circle(x, y, cx + 0.38 * s, cy + 0.17 * s, 0.025 * s), WHITE)
    if cheeks is not None:
        paint(k, ellipse(x, y, cx - 0.62 * s, cy - 0.12 * s, 0.11 * s, 0.07 * s), cheeks, 0.7)
        paint(k, ellipse(x, y, cx + 0.62 * s, cy - 0.12 * s, 0.11 * s, 0.07 * s), cheeks, 0.7)
    if open_mouth:
        m = np.maximum(circle(x, y, cx, cy + 0.02 * s, 0.26 * s), y - (cy + 0.02 * s))
        paint(k, m, (0.5, 0.05, 0.1))
        paint(k, np.maximum(m, circle(x, y, cx, cy - 0.26 * s, 0.14 * s)), (1.0, 0.45, 0.5))
    else:
        paint(k, arcdown(x, y, cx, cy - 0.04 * s, 0.24 * s, 0.03 * s), col)

# ---------- ESFERAS ----------
def b01(): # ESTRELLA - amarillo
    new((1.0, 0.80, 0.08))
    for k in (1, 2):
        x, y, _ = ctx(k); patch(k)
        sd = star5(x, y, 0, -0.02, 0.5, 0.48)
        paint(k, sd, (0.95, 0.45, 0.0)); paint(k, sd + 0.035, (1.0, 0.85, 0.1))
        if k == 1: face(k, 0, -0.04, 0.55)
        else:
            for (cx, cy, r) in ((-0.2, 0.15, 0.14), (0.22, 0.1, 0.12), (0.0, -0.22, 0.13)):
                s2 = star5(x, y, cx, cy, r, 0.48)
                paint(k, s2, (0.95, 0.45, 0.0)); paint(k, s2 + 0.015, (1.0, 0.85, 0.1))
def b02(): # CORAZON - rojo
    new((0.90, 0.10, 0.15))
    for k in (1, 2):
        x, y, _ = ctx(k); patch(k)
        if k == 1:
            sd = heart(x, y, 0, 0, 0.62)
            paint(k, sd, (0.75, 0.0, 0.2)); paint(k, sd + 0.035, (1.0, 0.25, 0.4))
            paint(k, ellipse(x, y, -0.2, 0.2, 0.08, 0.045, 0.7), (1, 0.8, 0.85), 0.9)
            face(k, 0, -0.04, 0.38, cheeks=(1.0, 0.7, 0.75))
        else:
            for (cx, cy, s, c) in ((-0.2, 0.12, 0.34, (1.0, 0.25, 0.4)), (0.22, 0.02, 0.26, (1.0, 0.5, 0.65)), (0.0, -0.25, 0.22, (0.95, 0.15, 0.25))):
                sd = heart(x, y, cx, cy, s)
                paint(k, sd, (0.75, 0.0, 0.2)); paint(k, sd + 0.02, c)
def b03(): # CARITA FELIZ - naranja
    new((1.0, 0.48, 0.04))
    for k in (1, 2):
        x, y, _ = ctx(k)
        paint(k, circle(x, y, 0, 0, 0.675), (0.85, 0.35, 0.0))
        paint(k, circle(x, y, 0, 0, 0.65), (1.0, 0.86, 0.12))
        if k == 1: face(k, 0, -0.02, 0.62)
        else:
            face(k, 0, -0.02, 0.62, wink=True, open_mouth=True)
            paint(k, circle(x, y, 0, -0.30, 0.1), (1.0, 0.45, 0.5), 0.0)
def b04(): # SANDIA - verde
    new((0.20, 0.72, 0.25))
    s = np.sin(LON * 8 + 0.5 * np.sin(LAT * 5) + 1.0)
    a = np.clip((s - 0.25) / 0.2, 0, 1)[..., None]
    global img
    img = img * (1 - a) + np.array((0.05, 0.40, 0.12), np.float32) * a
    for k in (1, 2):
        x, y, _ = ctx(k)
        paint(k, circle(x, y, 0, 0, 0.665), (0.12, 0.45, 0.15))
        paint(k, circle(x, y, 0, 0, 0.64), (0.62, 0.9, 0.5))
        paint(k, circle(x, y, 0, 0, 0.585), (0.96, 0.99, 0.9))
        paint(k, circle(x, y, 0, 0, 0.545), (0.97, 0.22, 0.28))
        if k == 1:
            for ang in np.linspace(0, 2 * math.pi, 7)[:-1] + 0.3:
                for rr in (0.38,):
                    cx, cy = rr * math.cos(ang), rr * math.sin(ang)
                    paint(k, ellipse(x, y, cx, cy, 0.035, 0.065, ang + math.pi / 2), (0.1, 0.07, 0.07))
            for ang in np.linspace(0, 2 * math.pi, 4)[:-1] + 0.9:
                cx, cy = 0.17 * math.cos(ang), 0.17 * math.sin(ang)
                paint(k, ellipse(x, y, cx, cy, 0.03, 0.055, ang + math.pi / 2), (0.1, 0.07, 0.07))
        else:
            face(k, 0, -0.02, 0.55, col=(0.35, 0.02, 0.1), cheeks=(1.0, 0.65, 0.7))
def b05(): # LUNA Y ESTRELLAS - azul noche
    new((0.07, 0.10, 0.42))
    for (e, a) in rand_dirs(70, 5, avoid=True):
        col = (1.0, 0.95, 0.6) if a > 0.035 else (0.85, 0.9, 1.0)
        paint_all(angdist(e) - a * 0.6, col)
    for k in (1, 2):
        x, y, _ = ctx(k)
        if k == 1:
            moon = sub(circle(x, y, 0, 0, 0.5), circle(x, y, 0.22, 0.1, 0.42))
            paint(k, moon, (0.85, 0.65, 0.2)); paint(k, moon + 0.03, (1.0, 0.92, 0.5))
            paint(k, arcup(x, y, -0.22, 0.06, 0.07, 0.016), (0.45, 0.3, 0.1))
            paint(k, ellipse(x, y, -0.28, -0.12, 0.07, 0.045), (1.0, 0.7, 0.6), 0.7)
            for (cx, cy, r) in ((0.42, 0.34, 0.1), (0.4, -0.3, 0.07), (0.1, 0.55, 0.06)):
                s2 = star5(x, y, cx, cy, r, 0.48); paint(k, s2, (1.0, 0.95, 0.5))
        else:
            sd = star5(x, y, 0, -0.02, 0.5, 0.48)
            paint(k, sd, (0.85, 0.65, 0.2)); paint(k, sd + 0.035, (1.0, 0.92, 0.5))
            face(k, 0, -0.04, 0.5, col=(0.4, 0.28, 0.1), cheeks=(1.0, 0.65, 0.5))
def fish(k, cx, cy, sc, body, fin, flip=1):
    x, y, _ = ctx(k)
    X = (x - cx) * flip / sc; Y = (y - cy) / sc
    sd_t = tri(X, Y, (0.18, 0.0), (0.5, 0.24), (0.5, -0.24))
    sd_b = ellipse(X, Y, -0.06, 0, 0.36, 0.25)
    sd_f = tri(X, Y, (-0.1, 0.18), (0.12, 0.34), (0.15, 0.1))
    allf = union(sd_t, sd_b, sd_f) * sc
    paint(k, allf, tuple(np.array(fin) * 0.6)); paint(k, allf + 0.012 * sc / 0.5, fin)
    paint(k, sd_b * sc, body)
    paint(k, ring(box(X, Y, 0.06, 0, 0.03, 0.22) , 0) * sc + 0, (1, 1, 1), 0.0)
    paint(k, np.maximum(sd_b, box(X, Y, 0.06, 0, 0.04, 0.5)) * sc, (1, 0.95, 0.9))
    paint(k, circle(X, Y, -0.22, 0.06, 0.07) * sc, (1, 1, 1))
    paint(k, circle(X, Y, -0.21, 0.06, 0.04) * sc, DARK)
    paint(k, arcdown(X, Y, -0.3, -0.04, 0.07, 0.012) * sc, DARK)
def b06(): # PEZ - turquesa
    new((0.08, 0.68, 0.82))
    for (e, a) in rand_dirs(26, 11, avoid=True):
        a2 = a * 0.8
        paint_all(ring(angdist(e) - a2, 0.004), (0.85, 1.0, 1.0))
    for k in (1, 2):
        patch(k)
        if k == 1: fish(k, 0, 0, 0.9, (1.0, 0.5, 0.08), (1.0, 0.75, 0.2))
        else: fish(k, 0, 0, 0.8, (1.0, 0.85, 0.15), (1.0, 0.55, 0.15), flip=-1)
def flower(k, petal, petal_dark, face_on=True, n=6):
    x, y, _ = ctx(k)
    sdp = None
    for i in range(n):
        a = 2 * math.pi * i / n + 0.3
        c = circle(x, y, 0.27 * math.cos(a), 0.27 * math.sin(a), 0.19)
        sdp = c if sdp is None else np.minimum(sdp, c)
    paint(k, sdp, petal_dark); paint(k, sdp + 0.02, petal)
    paint(k, circle(x, y, 0, 0, 0.19), (0.85, 0.6, 0.0)); paint(k, circle(x, y, 0, 0, 0.175), (1.0, 0.85, 0.15))
    if face_on: face(k, 0, -0.01, 0.2, col=(0.4, 0.2, 0.05), cheeks=(1.0, 0.6, 0.5))
def b07(): # FLOR - rosa
    new((1.0, 0.45, 0.72))
    for (e, a) in rand_dirs(30, 21, avoid=True):
        paint_all(circle(np.zeros_like(DX), np.zeros_like(DX), 0, 0, 1) * 0 + angdist(e) - a * 0.5, (1.0, 0.7, 0.85))
    for k in (1, 2):
        patch(k)
        if k == 1: flower(k, (1.0, 0.35, 0.65), (0.8, 0.15, 0.45))
        else: flower(k, (0.65, 0.4, 1.0), (0.45, 0.2, 0.8), n=5)
def b08(): # MONSTRUO - morado
    new((0.52, 0.2, 0.85))
    for (e, a) in rand_dirs(26, 31, avoid=True):
        paint_all(angdist(e) - a * 0.8, (0.62, 0.32, 0.92))
    for k in (1, 2):
        x, y, _ = ctx(k)
        if k == 1:
            patch(k, 0.64)
            paint(k, circle(x, y, 0, 0, 0.4), (0.1, 0.5, 0.2)); paint(k, circle(x, y, 0, 0, 0.37), (0.3, 0.85, 0.35))
            paint(k, circle(x, y, 0, 0, 0.2), (0.05, 0.05, 0.08))
            paint(k, circle(x, y, 0.09, 0.1, 0.07), WHITE); paint(k, circle(x, y, -0.07, -0.08, 0.03), WHITE)
        else:
            m = np.maximum(circle(x, y, 0, 0.14, 0.5), y - 0.14)
            paint(k, m, (0.35, 0.03, 0.15))
            paint(k, np.maximum(m, circle(x, y, 0, -0.36, 0.22)), (1.0, 0.42, 0.52))
            for cx in (-0.28, 0.0, 0.28):
                paint(k, np.maximum(tri(x, y, (cx - 0.1, 0.14), (cx + 0.1, 0.14), (cx, -0.04)), m), WHITE)
            paint(k, ellipse(x, y, -0.62, -0.1, 0.08, 0.06), (1.0, 0.55, 0.8), 0.7)
            paint(k, ellipse(x, y, 0.62, -0.1, 0.08, 0.06), (1.0, 0.55, 0.8), 0.7)
def b09(): # ARCOIRIS
    global img
    cols = [(0.9, 0.12, 0.15), (1.0, 0.5, 0.05), (1.0, 0.85, 0.1), (0.2, 0.75, 0.25), (0.15, 0.55, 0.95), (0.3, 0.25, 0.8), (0.65, 0.25, 0.8)]
    idx = np.clip(np.floor((LAT / np.pi + 0.5) * 7), 0, 6).astype(int)
    new((1, 1, 1)); img = np.array(cols, np.float32)[idx]
    for k in (1, 2):
        x, y, _ = ctx(k)
        if k == 1:
            c = union(circle(x, y, -0.3, -0.04, 0.2), circle(x, y, -0.02, 0.08, 0.28), circle(x, y, 0.3, -0.04, 0.2), box(x, y, 0, -0.14, 0.5, 0.1, 0.1))
            paint(k, c, (0.55, 0.7, 0.9)); paint(k, c + 0.03, WHITE)
            face(k, 0, -0.06, 0.42, col=(0.3, 0.3, 0.5), cheeks=(1.0, 0.65, 0.75))
        else:
            patch(k, 0.66, (1.0, 0.95, 0.75))
            paint(k, circle(x, y, 0, 0, 0.26), (1.0, 0.6, 0.0)); paint(k, circle(x, y, 0, 0, 0.235), (1.0, 0.85, 0.1))
            for i in range(10):
                a = 2 * math.pi * i / 10
                paint(k, seg(x, y, 0.34 * math.cos(a), 0.34 * math.sin(a), 0.5 * math.cos(a), 0.5 * math.sin(a)) - 0.035, (1.0, 0.7, 0.1))
            face(k, 0, -0.02, 0.24, col=(0.5, 0.2, 0.0), cheeks=(1.0, 0.5, 0.4))
def b10(): # PANDA - blanco y negro
    new((0.97, 0.97, 0.95))
    BK = (0.07, 0.07, 0.08)
    for k in (1, 2):
        x, y, _ = ctx(k)
        if k == 1:
            paint(k, circle(x, y, -0.55, 0.6, 0.22), BK); paint(k, circle(x, y, 0.55, 0.6, 0.22), BK)
            paint(k, ellipse(x, y, -0.27, 0.07, 0.115, 0.17, 0.5), BK); paint(k, ellipse(x, y, 0.27, 0.07, 0.115, 0.17, -0.5), BK)
            paint(k, circle(x, y, -0.25, 0.1, 0.055), WHITE); paint(k, circle(x, y, 0.25, 0.1, 0.055), WHITE)
            paint(k, circle(x, y, -0.24, 0.1, 0.03), BK); paint(k, circle(x, y, 0.24, 0.1, 0.03), BK)
            paint(k, ellipse(x, y, 0, -0.1, 0.09, 0.06), BK)
            paint(k, arcdown(x, y, -0.07, -0.17, 0.07, 0.012), BK); paint(k, arcdown(x, y, 0.07, -0.17, 0.07, 0.012), BK)
            paint(k, ellipse(x, y, -0.45, -0.18, 0.09, 0.06), (1.0, 0.6, 0.65), 0.6); paint(k, ellipse(x, y, 0.45, -0.18, 0.09, 0.06), (1.0, 0.6, 0.65), 0.6)
        else:
            paint(k, ellipse(x, y, 0, -0.12, 0.28, 0.24), BK)
            for (cx, cy, rot) in ((-0.32, 0.26, 0.35), (-0.11, 0.42, 0.1), (0.11, 0.42, -0.1), (0.32, 0.26, -0.35)):
                paint(k, ellipse(x, y, cx, cy, 0.09, 0.12, rot), BK)
            paint(k, ellipse(x, y, -0.1, -0.05, 0.07, 0.04, 0.5), (0.4, 0.4, 0.45), 0.7)

def tr(k, sc, cx, cy):
    x, y, _ = ctx(k)
    return (x - cx) / sc, (y - cy) / sc
def P(k, sc, sd, col, alpha=1.0): paint(k, sd * sc, col, alpha)

def b11(): # HELADO - vino
    new((0.55, 0.08, 0.22))
    for (e, a) in rand_dirs(30, 41, avoid=True):
        paint_all(angdist(e) - a * 0.5, (0.78, 0.30, 0.46))
    for k in (1, 2):
        patch(k)
        x, y, _ = ctx(k)
        if k == 1:
            sc, oy = 0.9, -0.06
            X, Y = tr(k, sc, 0.0, oy)
            cone = tri(X, Y, (-0.24, 0.0), (0.24, 0.0), (0.0, -0.58))
            P(k, sc, cone, (0.65, 0.4, 0.15)); P(k, sc, cone + 0.025, (0.93, 0.7, 0.35))
            for t in (-0.12, 0.0, 0.12):
                P(k, sc, np.maximum(seg(X, Y, t - 0.2, 0.0, t + 0.08, -0.5) - 0.008, cone + 0.02), (0.7, 0.45, 0.2))
                P(k, sc, np.maximum(seg(X, Y, t + 0.2, 0.0, t - 0.08, -0.5) - 0.008, cone + 0.02), (0.7, 0.45, 0.2))
            s1 = union(circle(X, Y, 0, 0.18, 0.27), ellipse(X, Y, 0, 0.04, 0.3, 0.1))
            P(k, sc, s1, (0.85, 0.3, 0.5)); P(k, sc, s1 + 0.025, (1.0, 0.6, 0.75))
            s2 = circle(X, Y, 0, 0.45, 0.2)
            P(k, sc, s2, (0.85, 0.75, 0.5)); P(k, sc, s2 + 0.025, (1.0, 0.95, 0.8))
            ch = circle(X, Y, 0.03, 0.68, 0.07)
            P(k, sc, ch, (0.7, 0.0, 0.1)); P(k, sc, ch + 0.015, (0.95, 0.15, 0.25))
            P(k, sc, seg(X, Y, 0.03, 0.74, 0.12, 0.88) - 0.008, (0.3, 0.5, 0.15))
            face(k, 0.0, oy + 0.18 * sc, 0.24, cheeks=(1.0, 0.8, 0.85))
        else:
            body = box(x, y, 0, 0.08, 0.22, 0.3, 0.14)
            body = sub(body, circle(x, y, 0.22, 0.3, 0.09))
            paint(k, body, (0.85, 0.3, 0.2)); paint(k, body + 0.025, (1.0, 0.55, 0.3))
            paint(k, np.maximum(body + 0.025, np.abs(y - 0.18) - 0.05), (1.0, 0.85, 0.35))
            stick = box(x, y, 0, -0.42, 0.06, 0.14, 0.04)
            paint(k, stick, (0.7, 0.5, 0.25)); paint(k, stick + 0.02, (0.95, 0.78, 0.5))
            face(k, 0, 0.0, 0.3, cheeks=(1.0, 0.7, 0.7))

def butterfly(k, wing_a, wing_b, outline):
    x, y, _ = ctx(k)
    ua = union(ellipse(x, y, -0.24, 0.13, 0.2, 0.26, 0.5), ellipse(x, y, 0.24, 0.13, 0.2, 0.26, -0.5))
    la = union(ellipse(x, y, -0.19, -0.17, 0.14, 0.18, -0.4), ellipse(x, y, 0.19, -0.17, 0.14, 0.18, 0.4))
    paint(k, la, outline); paint(k, la + 0.02, wing_b)
    paint(k, ua, outline); paint(k, ua + 0.02, wing_a)
    for sx in (-1, 1):
        paint(k, circle(x, y, sx * 0.27, 0.19, 0.06), WHITE); paint(k, circle(x, y, sx * 0.2, -0.17, 0.045), WHITE)
        paint(k, seg(x, y, 0, 0.2, sx * 0.14, 0.4) - 0.012, DARK)
        paint(k, circle(x, y, sx * 0.14, 0.4, 0.03), DARK)
    paint(k, seg(x, y, 0, -0.3, 0, 0.2) - 0.045, DARK)
    paint(k, circle(x, y, 0, 0.22, 0.055), DARK)
def b12(): # MARIPOSA - carbon
    new((0.16, 0.16, 0.20))
    for (e, a) in rand_dirs(26, 51, avoid=True):
        paint_all(angdist(e) - a * 0.6, (0.55, 0.57, 0.68))
    for k in (1, 2):
        patch(k)
        if k == 1: butterfly(k, (1.0, 0.55, 0.15), (1.0, 0.82, 0.2), (0.7, 0.3, 0.0))
        else: butterfly(k, (0.95, 0.4, 0.75), (0.65, 0.4, 0.95), (0.5, 0.15, 0.5))

def b13(): # COHETE - gris azulado
    new((0.50, 0.56, 0.72))
    for (e, a) in rand_dirs(30, 61, avoid=True):
        paint_all(angdist(e) - a * 0.5, (0.75, 0.8, 0.95))
    for k in (1, 2):
        x, y, _ = ctx(k)
        patch(k, 0.66, (0.08, 0.1, 0.32))
        for (cx, cy, r) in ((-0.42, 0.3, 0.06), (0.4, 0.38, 0.05), (0.45, -0.2, 0.06), (-0.4, -0.3, 0.05), (-0.1, 0.55, 0.04)):
            paint(k, star5(x, y, cx, cy, r, 0.48), (1.0, 0.95, 0.6))
        if k == 1:
            for sx in (-1, 1):
                fin = tri(x, y, (sx * 0.13, -0.08), (sx * 0.33, -0.34), (sx * 0.12, -0.26))
                paint(k, fin, (0.7, 0.05, 0.1)); paint(k, fin + 0.015, (0.95, 0.2, 0.25))
            paint(k, tri(x, y, (-0.09, -0.3), (0.09, -0.3), (0.0, -0.56)), (1.0, 0.45, 0.0))
            paint(k, tri(x, y, (-0.05, -0.3), (0.05, -0.3), (0.0, -0.45)), (1.0, 0.9, 0.2))
            body = ellipse(x, y, 0, 0.04, 0.16, 0.38)
            paint(k, body, (0.55, 0.6, 0.7)); paint(k, body + 0.02, WHITE)
            paint(k, np.maximum(body + 0.02, 0.19 - y), (0.9, 0.15, 0.2))
            paint(k, circle(x, y, 0, 0.0, 0.09), (0.3, 0.35, 0.5)); paint(k, circle(x, y, 0, 0.0, 0.065), (0.45, 0.8, 1.0))
            paint(k, circle(x, y, -0.02, 0.02, 0.02), WHITE)
        else:
            pl = circle(x, y, 0, 0, 0.27)
            ringsd = ring(ellipse(x, y, 0, 0, 0.5, 0.13, 0.35), 0.022)
            paint(k, ringsd, (0.95, 0.8, 0.5))
            paint(k, pl, (0.75, 0.35, 0.1)); paint(k, pl + 0.02, (1.0, 0.65, 0.25))
            for yy, col in ((0.12, (0.95, 0.5, 0.15)), (-0.08, (1.0, 0.8, 0.4))):
                paint(k, np.maximum(pl + 0.02, np.abs(y - yy) - 0.04), col)
            paint(k, np.maximum(ringsd, 0.0 - (y - 0.02) * 1.0 + (0.02 - 0.02)), (0.95, 0.8, 0.5))
            face(k, 0, -0.01, 0.24, col=(0.4, 0.15, 0.05), cheeks=(1.0, 0.55, 0.45))

def b14(): # GATITO - caramelo
    new((0.82, 0.54, 0.24))
    for (e, a) in rand_dirs(30, 71, avoid=True):
        paint_all(angdist(e) - a * 0.6, (0.9, 0.65, 0.35))
    for k in (1, 2):
        patch(k)
        x, y, _ = ctx(k)
        if k == 1:
            OR, OD = (1.0, 0.65, 0.28), (0.75, 0.38, 0.05)
            for sx in (-1, 1):
                ear = tri(x, y, (sx * 0.4, 0.05), (sx * 0.34, 0.52), (sx * 0.08, 0.3))
                paint(k, ear, OD); paint(k, ear + 0.02, OR)
                paint(k, tri(x, y, (sx * 0.34, 0.14), (sx * 0.31, 0.39), (sx * 0.16, 0.28)), (1.0, 0.7, 0.75))
            head = ellipse(x, y, 0, -0.03, 0.43, 0.34)
            paint(k, head, OD); paint(k, head + 0.025, OR)
            for t in (-0.1, 0.0, 0.1):
                paint(k, seg(x, y, t, 0.31, t * 0.7, 0.2) - 0.012, OD)
            for sx in (-1, 1):
                paint(k, ellipse(x, y, sx * 0.17, 0.04, 0.065, 0.09), DARK)
                paint(k, circle(x, y, sx * 0.15, 0.08, 0.022), WHITE)
                for dy in (0.0, -0.05, -0.1):
                    paint(k, seg(x, y, sx * 0.3, -0.11 + dy, sx * 0.55, -0.11 + dy * 2.2 + 0.03) - 0.006, (0.4, 0.2, 0.05))
                paint(k, ellipse(x, y, sx * 0.3, -0.1, 0.08, 0.05), (1.0, 0.55, 0.6), 0.6)
            paint(k, tri(x, y, (-0.055, -0.04), (0.055, -0.04), (0.0, -0.1)), (0.95, 0.45, 0.55))
            paint(k, arcdown(x, y, -0.05, -0.12, 0.05, 0.01), DARK); paint(k, arcdown(x, y, 0.05, -0.12, 0.05, 0.01), DARK)
        else:
            fish(k, 0, 0, 0.85, (0.45, 0.7, 0.95), (0.7, 0.85, 1.0))

def b15(): # RANA - verde lima
    new((0.56, 0.86, 0.18))
    for (e, a) in rand_dirs(26, 81, avoid=True):
        paint_all(angdist(e) - a * 0.7, (0.68, 0.94, 0.3))
    for k in (1, 2):
        patch(k)
        x, y, _ = ctx(k)
        if k == 1:
            G, GD = (0.30, 0.72, 0.22), (0.1, 0.4, 0.1)
            sh = union(ellipse(x, y, 0, -0.1, 0.5, 0.33), circle(x, y, -0.27, 0.2, 0.17), circle(x, y, 0.27, 0.2, 0.17))
            paint(k, sh, GD); paint(k, sh + 0.025, G)
            for sx in (-1, 1):
                paint(k, circle(x, y, sx * 0.27, 0.22, 0.115), WHITE)
                paint(k, circle(x, y, sx * 0.26, 0.21, 0.06), DARK)
                paint(k, circle(x, y, sx * 0.24, 0.24, 0.022), WHITE)
                paint(k, circle(x, y, sx * 0.06, -0.04, 0.012), GD)
                paint(k, ellipse(x, y, sx * 0.4, -0.16, 0.08, 0.05), (1.0, 0.55, 0.6), 0.65)
            paint(k, arcdown(x, y, 0, -0.1, 0.3, 0.022), (0.2, 0.3, 0.1))
        else:
            R, RD = (0.92, 0.12, 0.15), (0.45, 0.0, 0.05)
            for sx in (-1, 1):
                paint(k, seg(x, y, 0, 0.3, sx * 0.14, 0.5) - 0.012, DARK)
                paint(k, circle(x, y, sx * 0.14, 0.5, 0.025), DARK)
            paint(k, ellipse(x, y, 0, 0.2, 0.18, 0.12), DARK)
            body = circle(x, y, 0, -0.08, 0.38)
            paint(k, body, RD); paint(k, body + 0.025, R)
            paint(k, np.maximum(body + 0.025, np.abs(x) - 0.012), DARK)
            for (cx, cy, r) in ((-0.2, 0.06, 0.07), (0.2, 0.06, 0.07), (-0.22, -0.2, 0.065), (0.22, -0.2, 0.065), (-0.1, -0.36, 0.045), (0.1, -0.36, 0.045)):
                paint(k, circle(x, y, cx, cy, r), DARK)
            for sx in (-1, 1):
                paint(k, circle(x, y, sx * 0.1, 0.24, 0.035), WHITE)

BALLS = [
    ("01_Estrella", b01), ("02_Corazon", b02), ("03_CaritaFeliz", b03), ("04_Sandia", b04),
    ("05_LunaEstrellas", b05), ("06_Pez", b06), ("07_Flor", b07), ("08_Monstruo", b08),
    ("09_Arcoiris", b09), ("10_Panda", b10),
    ("11_Helado", b11), ("12_Mariposa", b12), ("13_Cohete", b13), ("14_Gatito", b14), ("15_Rana", b15),
]

def write_png(path, arr):
    a = (np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8)[::-1]   # fila 0 = arriba
    raw = b"".join(b"\x00" + a[j].tobytes() for j in range(a.shape[0]))
    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", a.shape[1], a.shape[0], 8, 2, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b"")
    with open(path, "wb") as f: f.write(png)

import sys
ONLY = [a for a in sys.argv if a[:2].isdigit()]
for name, fn in BALLS:
    if ONLY and name[:2] not in ONLY: continue
    _ctx.clear() if False else None
    fn()
    write_png(os.path.join(TEX, f"Esfera_{name}.png"), img)
    print("textura", name)
