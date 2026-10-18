#!/usr/bin/env python3
"""Builds Cellblock's PBR surface maps from CC0 Poly Haven photogrammetry (https://polyhaven.com, CC0 1.0).

    python3 Tools/Art/build_surfaces.py <download-cache-dir>

Needs numpy, scipy and pillow. Sources are fetched into the cache (2K maps); outputs go to
Assets/Cellblock/Resources/Surfaces/<key>_a.jpg (albedo), <key>_n.png (OpenGL normal), <key>_m.png (mask:
R metallic, G occlusion, A smoothness) and macro.png (large-scale variation, used as a detail map).

The albedo is deliberately *near-neutral*: FacilityView tints every material with its palette colour, so the maps
carry only realistic surface variation (pores, mortar, grout, scuffs) around a mean the old flat textures had. The
palette, and therefore the look of the game, stays the designer's.
"""
import json, os, sys, urllib.request
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Cellblock", "Resources", "Surfaces")
SIZE = 1024

# key: Poly Haven id, metres the source covers, albedo mean, contrast kept, chroma kept, smoothness, metallic,
#      normal strength, occlusion strength, procedural pattern
SURFACES = {
    "cmu":      dict(src="painted_plaster_wall", metres=2.0, mean=.93, contrast=.55, chroma=.10, smooth=.30, metal=0, normal=1.0, ao=.6, pattern="cmu"),
    "brick":    dict(src="brick_wall_001",       metres=3.0, mean=.90, contrast=.70, chroma=.18, smooth=.16, metal=0, normal=1.0, ao=.8),
    "floor":    dict(src="painted_concrete_02",  metres=4.0, mean=.95, contrast=.45, chroma=.06, smooth=.46, metal=0, normal=.7,  ao=.4, pattern="floor"),
    "shower":   dict(src="long_white_tiles",     metres=1.27, mean=.94, contrast=.55, chroma=.05, smooth=.62, metal=0, normal=1.0, ao=.7),
    "paving":   dict(src="concrete_pavement",    metres=1.8, mean=.92, contrast=.60, chroma=.08, smooth=.18, metal=0, normal=1.0, ao=.7),
    "concrete": dict(src="brushed_concrete_04",  metres=2.0, mean=.92, contrast=.55, chroma=.06, smooth=.20, metal=0, normal=1.0, ao=.6),
    "asphalt":  dict(src="asphalt_floor",        metres=2.35, mean=.90, contrast=.75, chroma=.04, smooth=.14, metal=0, normal=1.2, ao=.7),
    "gravel":   dict(src="gravel_floor",         metres=2.25, mean=.88, contrast=.85, chroma=.15, smooth=.10, metal=0, normal=1.2, ao=.9),
    "wood":     dict(src="oak_veneer_01",        metres=1.83, mean=.90, contrast=.80, chroma=.30, smooth=.38, metal=0, normal=.8,  ao=.3),
    "fabric":   dict(src="cotton_jersey",        metres=.26, mean=.95, contrast=.50, chroma=0,   smooth=.08, metal=0, normal=1.0, ao=.6),
    "steel":    dict(src="blue_metal_plate",     metres=2.5, mean=.94, contrast=.35, chroma=0,   smooth=.50, metal=.45, normal=.5, ao=.3),
}


def fetch(cache, pid):
    os.makedirs(cache, exist_ok=True)
    files, meta, cached = {}, None, os.path.join(cache, pid + ".json")
    if os.path.exists(cached): meta = json.load(open(cached))
    else:
        # Poly Haven asks API users to identify themselves.
        request = urllib.request.Request("https://api.polyhaven.com/files/" + pid, headers={"User-Agent": "CellblockHoldings-surface-build/1.0"})
        meta = json.load(urllib.request.urlopen(request, timeout=30)); json.dump(meta, open(cached, "w"))
    for kind, fmt in (("Diffuse", "jpg"), ("nor_gl", "png"), ("Rough", "jpg"), ("AO", "jpg")):
        path = os.path.join(cache, f"{pid}_{kind}.{fmt}")
        if not os.path.exists(path) and kind in meta:
            urllib.request.urlretrieve(meta[kind]["2k"][fmt]["url"], path)
        files[kind] = path if os.path.exists(path) else None
    return files


def load(path, size, mode="RGB"):
    image = Image.open(path)
    if image.mode in ("I;16", "I;16B", "I"):
        a = np.asarray(image, dtype=np.float32) / 65535.0
        image = Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255))
    image = image.convert(mode).resize((size, size), Image.LANCZOS)
    return np.asarray(image, dtype=np.float32) / 255.0


def luma(rgb):
    return rgb[..., 0] * .2126 + rgb[..., 1] * .7152 + rgb[..., 2] * .0722


def flatten(channel, sigma, amount):
    # Remove baked large-scale lighting/stains so a tile repeats without a visible blotch grid. Wrap keeps it seamless.
    low = ndimage.gaussian_filter(channel, sigma, mode="wrap")
    return channel - amount * (low - low.mean())


def blend_normals(base, detail):
    # Whiteout blend of two tangent-space normals (both in [-1,1]).
    n = np.stack([base[..., 0] + detail[..., 0], base[..., 1] + detail[..., 1], base[..., 2] * detail[..., 2]], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def height_normal(height, strength):
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * .5
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * .5
    # Image rows run downward; OpenGL normals point +Y up the texture.
    n = np.stack([-dx * strength, dy * strength, np.ones_like(height)], -1)
    return n / np.linalg.norm(n, axis=-1, keepdims=True)


def joints(size, metres, cols, rows, joint_m, running_bond, seed):
    """Height (1 = face, 0 = joint), per-unit id, and a soft joint mask for a grid of units tiling exactly."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) + .5
    u, v = x / size * cols, y / size * rows
    row = np.floor(v).astype(int)
    if running_bond: u = u + (row % 2) * .5
    col = np.floor(u).astype(int) % cols
    fu, fv = u - np.floor(u), v - np.floor(v)
    unit_w, unit_h = metres / cols, metres / rows
    du = np.minimum(fu, 1 - fu) * unit_w
    dv = np.minimum(fv, 1 - fv) * unit_h
    d = np.minimum(du, dv)  # metres to the nearest joint centreline
    half = joint_m * .5
    # Concave (tooled) joint profile, then a small arris roll-off on the unit edge.
    t = np.clip((d - half * .2) / (half * 1.6), 0, 1)
    height = t * t * (3 - 2 * t)
    unit = (row % rows) * cols + col
    rng = np.random.default_rng(seed)
    tone = rng.normal(0, 1, rows * cols)[unit]
    return height, tone, 1 - height


def build(key, cfg, cache):
    files = fetch(cache, cfg["src"])
    albedo = load(files["Diffuse"], SIZE)
    normal = load(files["nor_gl"], SIZE) * 2 - 1
    rough = load(files["Rough"], SIZE, "L") if files["Rough"] else np.full((SIZE, SIZE), .7, np.float32)
    ao = load(files["AO"], SIZE, "L") if files["AO"] else np.ones((SIZE, SIZE), np.float32)
    normal[..., :2] *= cfg["normal"]
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)

    # Neutral albedo: relative luminance variation around the target mean, a little of the source's own colour.
    L = luma(albedo)
    L = flatten(L, SIZE / 10, .75)
    chroma = albedo - luma(albedo)[..., None]
    rel = L / max(L.mean(), 1e-4) - 1
    rel = np.clip(rel, -3 * rel.std(), 3 * rel.std())
    lum = cfg["mean"] * (1 + cfg["contrast"] * rel)
    occlusion = 1 - cfg["ao"] * (1 - ao)

    pattern = cfg.get("pattern")
    if pattern == "cmu":
        # Painted concrete masonry units: 400 x 200 mm nominal, 10 mm joints, running bond; 2.0 m = 5 x 10 units.
        height, tone, joint = joints(SIZE, cfg["metres"], 5, 10, .010, True, 7)
        lum = lum * (1 + .022 * tone) * (1 - .10 * joint)
        occlusion = occlusion * (1 - .35 * joint)
        normal = blend_normals(height_normal(height, 1.6), normal)
        # Paint pools slightly in the face pores: a touch more sheen on the faces than in the joints.
        rough = rough + .10 * joint
    elif pattern == "floor":
        # Sealed concrete floor tiles: 500 mm squares, 3 mm joints, 4.0 m = 8 x 8, gentle per-tile tone.
        height, tone, joint = joints(SIZE, cfg["metres"], 8, 8, .004, False, 11)
        lum = lum * (1 + .018 * tone) * (1 - .16 * joint)
        occlusion = occlusion * (1 - .25 * joint)
        normal = blend_normals(height_normal(height, .9), normal)
        rough = rough + .25 * joint

    rgb = np.clip(lum[..., None] + cfg["chroma"] * chroma * (cfg["mean"] / max(luma(albedo).mean(), 1e-4)), 0, 1)
    smooth = np.clip(cfg["smooth"] + .45 * cfg["smooth"] * (rough.mean() - rough) / max(rough.std(), 1e-4) * .25, 0, 1)

    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.uint8(rgb * 255 + .5)).save(os.path.join(OUT, key + "_a.jpg"), quality=92, subsampling=0)
    Image.fromarray(np.uint8(np.clip(normal * .5 + .5, 0, 1) * 255 + .5)).save(os.path.join(OUT, key + "_n.jpg"), quality=93, subsampling=0)
    mask = np.zeros((SIZE, SIZE, 4), np.float32)
    mask[..., 0] = cfg["metal"]; mask[..., 1] = np.clip(occlusion, 0, 1); mask[..., 3] = smooth
    small = Image.fromarray(np.uint8(mask * 255 + .5)).resize((SIZE // 2, SIZE // 2), Image.LANCZOS)
    small.save(os.path.join(OUT, key + "_m.png"), optimize=True)
    print(f"{key:9s} {cfg['src']:22s} mean {rgb.mean():.3f}  std {luma(rgb).std():.3f}  smooth {smooth.mean():.2f}")


def macro():
    # Tileable large-scale variation (imported linear: grey 128 x URP's 2x detail multiply = no change).
    rng = np.random.default_rng(3); size = 512; acc = np.zeros((size, size), np.float32)
    for octave, weight in ((8, 1), (16, .5), (32, .25), (64, .12)):
        g = rng.normal(0, 1, (octave, octave)).astype(np.float32)
        acc += weight * ndimage.zoom(np.tile(g, (3, 3)), size / octave, order=3)[size:2 * size, size:2 * size]
    acc = (acc - acc.mean()) / acc.std()
    Image.fromarray(np.uint8(np.clip(128 + 11 * acc, 0, 255))).convert("RGB").save(os.path.join(OUT, "macro.png"), optimize=True)


if __name__ == "__main__":
    cache = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Temp", "surface-sources")
    for key, cfg in SURFACES.items(): build(key, cfg, cache)
    macro()
