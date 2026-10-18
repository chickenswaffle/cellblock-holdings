import sys, json
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from ik import *

def with_ground(pose, feet=("L", "R"), extra=0.0):
    """Lower/raise the Model so the lowest sole of the listed feet touches the floor."""
    p = dict(pose)
    mp = list(p.get("model_pos", (0, 0, 0)))
    p["model_pos"] = tuple(mp)
    o = solve(p)
    low = min(o["soleLow" + f] for f in feet)
    mp[1] += (FLOOR - low) + extra
    p["model_pos"] = tuple(round(v, 3) for v in mp)
    return p

def show(name, p, keys=("handL", "handR", "head_c"), extra=None):
    o = solve(p)
    print("== %s" % name)
    for k in ("model_pos", "model_rot", "torso", "head", "shL", "elL", "shR", "elR", "hipL", "knL", "hipR", "knR"):
        if k in p: print("   %-9s %s" % (k, p[k]))
    for k in keys: print("   -> %-7s %s" % (k, fmt(o[k])))
    print("   -> soles L %.3f R %.3f (floor %.2f)" % (o["soleLowL"], o["soleLowR"], FLOOR))
    if extra: extra(o)
    return o

POSES = {}

# ---------------- locomotion key poses (right foot = stance/lead) ----------------
POSES["Walk contact (R heel strike)"] = with_ground(dict(
    torso=(3, -4, 0), head=(-2, 4, 0),
    shL=(-20, 0, -4), elL=(-22, 0, 0), shR=(16, 0, 4), elR=(-12, 0, 0),
    hipR=(-22, 0, 0), knR=4, hipL=(16, 0, 0), knL=14), feet=("L", "R"))
POSES["Walk passing (R mid-stance)"] = with_ground(dict(
    torso=(3, 0, 1.5), head=(-2, 0, -1.5),
    shL=(-2, 0, -4), elL=(-15, 0, 0), shR=(2, 0, 4), elR=(-12, 0, 0),
    hipR=(-2, 0, 0), knR=8, hipL=(-12, 0, 0), knL=58), feet=("R",))

POSES["Jog contact (R)"] = with_ground(dict(
    torso=(7, -7, 0), head=(-5, 7, 0),
    shL=(-38, 6, -6), elL=(-95, 0, 0), shR=(28, 0, 8), elR=(-75, 0, 0),
    hipR=(-27, 0, 0), knR=16, hipL=(6, 0, 0), knL=92), feet=("R",))
POSES["Jog mid-stance (R)"] = with_ground(dict(
    torso=(7, 0, 0), head=(-5, 0, 0),
    shL=(-6, 0, -6), elL=(-90, 0, 0), shR=(4, 0, 6), elR=(-85, 0, 0),
    hipR=(2, 0, 0), knR=38, hipL=(-32, 0, 0), knL=100), feet=("R",))
POSES["Jog flight (after R toe-off)"] = dict(with_ground(dict(
    torso=(7, 6, 0), head=(-5, -6, 0),
    shL=(30, 0, -8), elL=(-75, 0, 0), shR=(-38, -6, 6), elR=(-95, 0, 0),
    hipR=(22, 0, 0), knR=40, hipL=(-40, 0, 0), knL=70), feet=("R",)))

POSES["Sprint contact (R)"] = with_ground(dict(
    torso=(14, -10, 0), head=(-12, 10, 0),
    shL=(-62, 8, -6), elL=(-100, 0, 0), shR=(48, 0, 10), elR=(-62, 0, 0),
    hipR=(-30, 0, 0), knR=24, hipL=(4, 0, 0), knL=118), feet=("R",))
POSES["Sprint mid-stance (R)"] = with_ground(dict(
    torso=(14, 0, 0), head=(-12, 0, 0),
    shL=(-12, 0, -6), elL=(-92, 0, 0), shR=(10, 0, 8), elR=(-85, 0, 0),
    hipR=(6, 0, 0), knR=42, hipL=(-38, 0, 0), knL=122), feet=("R",))
POSES["Sprint toe-off (R)"] = with_ground(dict(
    torso=(14, 10, 0), head=(-12, -10, 0),
    shL=(50, 0, -10), elL=(-60, 0, 0), shR=(-64, -8, 6), elR=(-100, 0, 0),
    hipR=(30, 0, 0), knR=14, hipL=(-58, 0, 0), knL=88), feet=("R",))
POSES["Sprint start (first step, R drive)"] = with_ground(dict(
    torso=(38, 8, 0), head=(-30, -8, 0),
    shL=(55, 0, -10), elL=(-55, 0, 0), shR=(-70, -8, 8), elR=(-95, 0, 0),
    hipR=(20, 0, 0), knR=30, hipL=(-62, 0, 0), knL=95), feet=("R",))

if __name__ == "__main__":
    for name, p in POSES.items():
        show(name, p, keys=("handL", "handR", "head_c", "ankleL", "ankleR", "toeR"))
