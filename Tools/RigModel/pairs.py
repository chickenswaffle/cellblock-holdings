import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from poses import *

def to_guard(p_world, G, yaw):
    return mv(ry(-yaw), sub(p_world, G))

def arm_to(pose, side, target, pole):
    e, el = solve_arm(pose, side, target, pole)
    e, el = canonical_arm(e, el)
    p = dict(pose); p["sh" + side] = e; p["el" + side] = el
    return p

def reach_report(tag, o, side, target):
    print("   %s hand%s err %.3f m" % (tag, side, length(sub(o["hand" + side], target))))

# ---------- Arm-bar escort (prisoner P at origin facing +Z, guard G at prisoner's right/behind) ----------
P = with_ground(dict(torso=(32, 8, 0), head=(-14, -6, 0),
    shR=(62, 0, 12), elR=(-4, 0, 0), shL=(-4, 0, -4), elL=(-18, 0, 0),
    hipL=(-14, 0, 0), knL=12, hipR=(10, 0, 0), knR=18), feet=("L", "R"))
oP = show("Arm-bar escort / PRISONER", P, keys=("handR", "elR", "shR", "head_c"))
G0, yaw = [0.40, 0, -0.42], 0.0
wrist = oP["handR"]; upper = add(scale(add(oP["shR"], oP["elR"]), 0.5), [0, 0.04, 0])
Gp = with_ground(dict(torso=(16, -14, 0), head=(-6, 10, 0),
    hipL=(-12, 0, -4), knL=14, hipR=(8, 0, 4), knR=12), feet=("L", "R"))
Gp = arm_to(Gp, "L", to_guard(wrist, G0, yaw), [-1, -1, 0])
Gp = arm_to(Gp, "R", to_guard(upper, G0, yaw), [1, -0.5, -0.5])
oG = show("Arm-bar escort / GUARD (root at prisoner +0.40 x, -0.42 z)", Gp, keys=("handL", "handR"))
reach_report("guard", oG, "L", to_guard(wrist, G0, yaw)); reach_report("guard", oG, "R", to_guard(upper, G0, yaw))

# ---------- Two-on-one: both arms barred ----------
P2 = with_ground(dict(torso=(40, 0, 0), head=(-18, 0, 0),
    shR=(60, 0, 14), elR=(-4, 0, 0), shL=(60, 0, -14), elL=(-4, 0, 0),
    hipL=(-16, 0, 0), knL=18, hipR=(8, 0, 0), knR=20), feet=("L", "R"))
oP2 = show("Two-on-one / PRISONER", P2, keys=("handL", "handR", "head_c"))
for side, gx, outer, inner in (("R", 0.42, "L", "R"), ("L", -0.42, "R", "L")):
    G1 = [gx, 0, -0.40]
    wristP = oP2["hand" + side]; upperP = add(scale(add(oP2["sh" + side], oP2["el" + side]), 0.5), [0, 0.04, 0])
    tw = -12 if side == "R" else 12
    g = with_ground(dict(torso=(18, tw, 0), head=(-6, -tw * 0.7, 0), hipL=(-12, 0, -4), knL=14, hipR=(8, 0, 4), knR=12), feet=("L", "R"))
    # the hand nearer the prisoner's wrist is the guard's hand on the prisoner's side
    wrist_hand = "L" if side == "R" else "R"; upper_hand = "R" if side == "R" else "L"
    g = arm_to(g, wrist_hand, to_guard(wristP, G1, 0), [(-1 if wrist_hand == "L" else 1), -1, 0])
    g = arm_to(g, upper_hand, to_guard(upperP, G1, 0), [(-1 if upper_hand == "L" else 1), -0.5, -0.5])
    og = show("Two-on-one / GUARD on prisoner's %s (root x %+.2f, z -0.40)" % (side, gx), g, keys=("handL", "handR"))
    reach_report("guard", og, wrist_hand, to_guard(wristP, G1, 0)); reach_report("guard", og, upper_hand, to_guard(upperP, G1, 0))

# ---------- Cuffed hands behind back (standing or kneeling) ----------
cuff_arms = dict(shL=(26, 0, 10), elL=(-70, 62, 0), shR=(26, 0, -10), elR=(-70, -62, 0))
kneel = with_ground(dict(torso=(8, 0, 0), head=(16, 0, 0), hipL=(-2, 0, -4), knL=92, hipR=(-2, 0, 4), knR=92, **cuff_arms), feet=("L", "R"))
ok = show("Cuffed kneel (prisoner)", kneel, keys=("handL", "handR", "kneeL", "kneecapL", "toeL", "back"))
print("   hand gap %.3f ; hands torso-local L %s R %s" % (length(sub(ok["handL"], ok["handR"])), fmt(mv(transpose(ok["torso"].R), sub(ok["handL"], ok["torso"].p))), fmt(mv(transpose(ok["torso"].R), sub(ok["handR"], ok["torso"].p)))))
