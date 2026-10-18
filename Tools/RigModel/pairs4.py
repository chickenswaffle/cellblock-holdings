import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from pairs2 import cuffs, ground_point
from poses import *

PIVOT = [0, 0.95, 0]   # whole-body rotation pivot ~ centre of mass (Model space)
def pivoted(rot, offset=(0, 0, 0)):
    R = euler(rot); rp = mv(R, PIVOT)
    return tuple(round(PIVOT[i] - rp[i] + offset[i], 3) for i in range(3))

prone = dict(model_rot=(90, 0, 0), model_pos=(0, 0.12, -0.9), head=(0, 75, 0), hipL=(0, 0, -6), knL=6, hipR=(0, 0, 6), knR=14)
oP = solve(cuffs(dict(prone)))
Gpos, Gyaw = [-0.50, 0, 0.12], 90.0
g = dict(torso=(52, 0, 0), head=(4, 0, 0), hipR=(6, 0, 4), knR=100, hipL=(-90, 0, -10), knL=90)
g = ground_point(g, "kneeR")
o0 = solve(g)
mid = add(scale(add(oP["handL"], oP["handR"]), 0.5), [0, 0.03, 0])
wr = to_guard(mid, Gpos, Gyaw)
blade = to_guard([0.06, 0.27, 0.42], Gpos, Gyaw)     # prisoner's shoulder blade (upper back), NOT the neck
g = arm_to(g, "R", wr, mv(o0["torso"].R, [1, -0.3, -0.5]))
g = arm_to(g, "L", blade, mv(o0["torso"].R, [-1, -0.3, -0.3]))
og = show("Guard kneel-cuff (root prisoner x -0.50, z +0.12, yaw +90)", g, keys=("handL", "handR", "kneeR", "head_c"))
reach_report("guard", og, "R", wr); reach_report("guard", og, "L", blade)
print("   left sole low %.3f right toe %s" % (og["soleLowL"], fmt(og["toeR"])))

# Tackle: dive and wrap pivoting about the COM
dive = dict(model_rot=(62, 0, 0), torso=(6, 0, 0), head=(-44, 0, 0),
    shL=(-120, 0, -14), elL=(-18, 0, 0), shR=(-120, 0, 14), elR=(-18, 0, 0),
    hipL=(16, 0, -4), knL=30, hipR=(4, 0, 4), knR=62)
dive["model_pos"] = pivoted(dive["model_rot"], (0, 0.18, 0))
show("Tackle dive (airborne, COM pivot)", dive, keys=("head_c", "handL", "handR", "waist", "toeL"))

E = with_ground(dict(torso=(16, 0, 0), hipL=(-30, 0, 0), knL=40, hipR=(26, 0, 0), knR=20), feet=("L",))
oE = solve(dict(E, root=(0, 0, 0.80)))
wrap = dict(model_rot=(48, 0, 0), torso=(8, 0, 0), head=(-30, 32, 0), hipL=(20, 0, -4), knL=30, hipR=(8, 0, 4), knR=58)
wrap["model_pos"] = pivoted(wrap["model_rot"], (0, -0.06, 0.0))
lw = add(oE["waist"], [0.15, -0.06, 0.08]); rw = add(oE["waist"], [-0.15, -0.06, 0.08])
o0 = solve(wrap)
wrap = arm_to(wrap, "L", lw, mv(o0["torso"].R, [-1, 0, 0]))
wrap = arm_to(wrap, "R", rw, mv(o0["torso"].R, [1, 0, 0]))
ow = show("Tackle wrap (escapee root 0.80 m ahead)", wrap, keys=("head_c", "handL", "handR", "chest", "waist"))
reach_report("guard", ow, "L", lw); reach_report("guard", ow, "R", rw)
print("   escapee waist %s back %s | guard chest %s head %s" % (fmt(oE["waist"]), fmt(oE["back"]), fmt(ow["chest"]), fmt(ow["head_c"])))

# Falling escapee (mid-fall 45 deg) and the guard riding down on top
fallE = dict(model_rot=(45, 0, 0), torso=(10, 0, 0), head=(-30, 0, 0),
    shL=(-70, 0, -30), elL=(-30, 0, 0), shR=(-70, 0, 30), elR=(-30, 0, 0),   # arms thrown forward to brace
    hipL=(-10, 0, -6), knL=20, hipR=(16, 0, 6), knR=30)
fallE["model_pos"] = pivoted(fallE["model_rot"], (0, -0.10, 0.25))
show("Escapee falling (tackled, 45 deg)", fallE, keys=("head_c", "handL", "handR", "waist", "toeL"))
