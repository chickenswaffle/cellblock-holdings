import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from pairs2 import cuffs, ground_point
from poses import *

prone = dict(model_rot=(90, 0, 0), model_pos=(0, 0.12, -0.9), head=(0, 75, 0),
             hipL=(0, 0, -6), knL=6, hipR=(0, 0, 6), knR=14)
prone = arm_to(prone, "L", [-0.42, 0.04, 0.78], [-0.5, 1, 0])
prone = arm_to(prone, "R", [0.42, 0.04, 0.78], [0.5, 1, 0])
show("Prone pinned, hands by head", prone, keys=("handL", "handR", "elL", "elR", "head_c"))

# Guard kneeling beside the prone prisoner to cuff: guard root at prisoner's left (x -0.55, z +0.15) facing +X (yaw 90)
oP = solve(cuffs(dict(prone)))
Gpos, Gyaw = [-0.62, 0, 0.10], 90.0
g = dict(torso=(38, 0, 0), head=(18, 0, 0), hipR=(4, 0, 4), knR=100, hipL=(-82, 0, -8), knL=88)
g = ground_point(g, "kneeR")
o0 = solve(g)
wr = to_guard(add(scale(add(oP["handL"], oP["handR"]), 0.5), [0, 0.03, 0]), Gpos, Gyaw)
sh = to_guard(add(oP["back"] if "back" in oP else [0,0.25,0.42], [0.10, 0.02, 0.0]), Gpos, Gyaw)
g = arm_to(g, "R", wr, mv(o0["torso"].R, [1, -0.3, -0.5]))
g = arm_to(g, "L", sh, mv(o0["torso"].R, [-1, -0.3, -0.5]))
og = show("Guard kneel-cuff (root at prisoner x -0.62, z +0.10, yaw +90)", g, keys=("handL", "handR", "kneeR", "toeR", "ankleL", "head_c"))
reach_report("guard", og, "R", wr); reach_report("guard", og, "L", sh)
print("   left sole low %.3f (floor -0.01)" % og["soleLowL"])

# Tackle sequence (guard). Gather -> dive -> wrap
gather = with_ground(dict(torso=(34, 0, 0), head=(-26, 0, 0),
    shL=(42, 0, -10), elL=(-60, 0, 0), shR=(42, 0, 10), elR=(-60, 0, 0),
    hipL=(-55, 0, -4), knL=78, hipR=(18, 0, 4), knR=48), feet=("L", "R"))
show("Tackle gather (plant)", gather, keys=("head_c", "handL", "handR"))
dive = dict(model_rot=(58, 0, 0), model_pos=(0, 0.42, 0.0), torso=(8, 0, 0), head=(-40, 0, 0),
    shL=(-128, 0, -14), elL=(-20, 0, 0), shR=(-128, 0, 14), elR=(-20, 0, 0),
    hipL=(18, 0, -4), knL=34, hipR=(6, 0, 4), knR=60)
show("Tackle dive (airborne)", dive, keys=("head_c", "handL", "handR", "waist", "toeL"))
# target escapee standing 0.95 m ahead: wrap his waist. Escapee root at (0,0,0.95) facing +Z
E = with_ground(dict(torso=(16, 0, 0), hipL=(-30, 0, 0), knL=40, hipR=(26, 0, 0), knR=20), feet=("L",))
oE = solve(dict(E, root=(0, 0, 0.95)))
wrap = dict(model_rot=(52, 0, 0), model_pos=(0, 0.30, 0.10), torso=(10, 0, 0), head=(-38, 30, 0),
    hipL=(22, 0, -4), knL=30, hipR=(10, 0, 4), knR=55)
lw = add(oE["waist"], [0.14, -0.05, 0.10]); rw = add(oE["waist"], [-0.14, -0.05, 0.10])
o0 = solve(wrap)
wrap = arm_to(wrap, "L", lw, mv(o0["torso"].R, [-1, 0, 0]))
wrap = arm_to(wrap, "R", rw, mv(o0["torso"].R, [1, 0, 0]))
ow = show("Tackle wrap (impact, escapee 0.95 m ahead)", wrap, keys=("head_c", "handL", "handR", "chest"))
reach_report("guard", ow, "L", lw); reach_report("guard", ow, "R", rw)
print("   escapee waist", fmt(oE["waist"]), "guard chest", fmt(ow["chest"]))
