"""Forward kinematics for the Cellblock procedural person rig, using Unity conventions.

Unity: left-handed, X right, Y up, Z forward. Quaternion.Euler(x, y, z) applies Z, then X, then Y
about the parent axes (extrinsic ZXY) == matrix Ry(y) * Rx(x) * Rz(z) acting on column vectors.
Positive rotation is counter-clockwise looking from the origin along the axis.
"""
import math

def rx(a):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    return [[1, 0, 0], [0, c, -s], [0, s, c]]

def ry(a):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    return [[c, 0, s], [0, 1, 0], [-s, 0, c]]

def rz(a):
    c, s = math.cos(math.radians(a)), math.sin(math.radians(a))
    return [[c, -s, 0], [s, c, 0], [0, 0, 1]]

def mm(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]

def mv(a, v):
    return [sum(a[i][k] * v[k] for k in range(3)) for i in range(3)]

def add(a, b):
    return [a[i] + b[i] for i in range(3)]

def sub(a, b):
    return [a[i] - b[i] for i in range(3)]

def length(a):
    return math.sqrt(sum(x * x for x in a))

def euler(e):
    x, y, z = e
    return mm(ry(y), mm(rx(x), rz(z)))

def spine(e):
    """Twist about the spine first, then lean/side: Euler(x,0,z) * Euler(0,y,0)."""
    x, y, z = e
    return mm(euler((x, 0, z)), ry(y))

I3 = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]

class Xf:
    def __init__(self, R, p):
        self.R, self.p = R, p
    def child(self, local_pos, local_rot):
        return Xf(mm(self.R, local_rot), add(self.p, mv(self.R, local_pos)))
    def point(self, local):
        return add(self.p, mv(self.R, local))

DEFAULT = dict(
    model_pos=(0, 0, 0), model_rot=(0, 0, 0), bob=0.0,
    torso=(0, 0, 0), torso_mode="unity", head=(0, 0, 0),
    shL=(0, 0, 0), shR=(0, 0, 0), elL=(0, 0, 0), elR=(0, 0, 0),
    hipL=(0, 0, 0), hipR=(0, 0, 0), knL=0, knR=0,
    yaw=0.0, root=(0, 0, 0),
)

def solve(pose):
    p = dict(DEFAULT); p.update(pose)
    root = Xf(ry(p["yaw"]), list(p["root"]))
    model = root.child(list(p["model_pos"]), euler(p["model_rot"]))
    trot = euler(p["torso"]) if p["torso_mode"] == "unity" else spine(p["torso"])
    torso = model.child([0, 0.9 + p["bob"], 0], trot)
    head = torso.child([0, 0.72, 0], euler(p["head"]))
    out = {"model": model, "torso": torso, "head": head}
    out["head_c"] = head.point([0, 0.02, 0.0])
    out["chest"] = torso.point([0, 0.42, 0.13])
    out["back"] = torso.point([0, 0.42, -0.13])
    out["belly"] = torso.point([0, 0.15, 0.12])
    out["waist"] = torso.point([0, 0.0, 0])
    for side, name, sx in ((0, "L", -1), (1, "R", 1)):
        sh = torso.child([sx * 0.23, 0.50, 0], euler(p["sh" + name]))
        el_e = p["el" + name]
        el = sh.child([0, -0.28, 0], euler((el_e[0], el_e[1], el_e[2] if len(el_e) > 2 else 0)))
        out["sh" + name] = sh.p
        out["el" + name] = el.p
        out["hand" + name] = el.point([0, -0.25, 0.03])
        out["grip" + name] = el.point([0, -0.25, 0.05])
        out["xf_sh" + name] = sh
        out["xf_el" + name] = el
        hip_e = p["hip" + name]
        hip = model.child([sx * 0.09, 0.90, 0], euler(hip_e))
        kn = hip.child([0, -0.40, 0], euler((p["kn" + name], 0, 0)))
        out["hip" + name] = hip.p
        out["knee" + name] = kn.p
        out["kneecap" + name] = kn.point([0, 0, 0.07])
        out["ankle" + name] = kn.point([0, -0.41, 0.0])
        corners = []
        for cx in (-0.081, 0.081):
            for cy in (-0.4775, -0.4425):
                for cz in (-0.088, 0.192):
                    corners.append(kn.point([cx, cy, cz]))
        out["soleLow" + name] = min(c[1] for c in corners)
        out["toe" + name] = kn.point([0, -0.46, 0.192])
        out["heel" + name] = kn.point([0, -0.46, -0.088])
        out["shin" + name] = kn.point([0, -0.20, 0.07])
    return out

FLOOR = -0.01  # floor (world 0.13) in Model space when root is at world 0.14

def fmt(v):
    return "(" + ", ".join("%+.3f" % x for x in v) + ")"

def report(name, pose, keys=("head_c", "handL", "handR", "elL", "elR", "kneeL", "kneeR", "ankleL", "ankleR")):
    o = solve(pose)
    print("==", name)
    for k in keys:
        print("  %-8s %s" % (k, fmt(o[k])))
    print("  soleLow L %.3f R %.3f (floor %.3f)" % (o["soleLowL"], o["soleLowR"], FLOOR))
    return o
