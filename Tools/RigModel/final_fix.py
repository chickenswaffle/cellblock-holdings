import sys
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from poses import *
cross_ = with_ground(dict(model_pos=(0, 0, 0.10), torso=(10, -24, 0), head=(-8, 22, 0),
    shL=(-62, 14, -14), elL=(-120, 30, 0), shR=(-100, -6, 4), elR=(-6, 0, 0),
    hipL=(-26, 0, -6), knL=30, hipR=(22, 0, 8), knR=10), feet=("L",))
show("Straight right (impact) raised", cross_, keys=("handL", "handR", "head_c"))
P = with_ground(dict(torso=(32, 8, 0), head=(-14, -6, 0), shR=(62, 0, 12), elR=(-4, 0, 0), shL=(-4, 0, -4), elL=(-18, 0, 0),
    hipL=(-14, 0, 0), knL=12, hipR=(10, 0, 0), knR=18), feet=("L", "R"))
oP = solve(P)
for G0 in ([0.42, 0, -0.55], [0.46, 0, -0.62]):
    wrist = oP["handR"]; upper = add(scale(add(oP["shR"], oP["elR"]), 0.5), [0, 0.04, 0])
    Gp = with_ground(dict(torso=(16, -14, 0), head=(-6, 10, 0), hipL=(-12, 0, -4), knL=14, hipR=(8, 0, 4), knR=12), feet=("L", "R"))
    Gp = arm_to(Gp, "L", to_guard(wrist, G0, 0), [-1, -1, 0])
    Gp = arm_to(Gp, "R", to_guard(upper, G0, 0), [1, -0.5, -0.5])
    oG = show("Escort GUARD at %s" % G0, Gp, keys=("handL", "handR"))
    reach_report("g", oG, "L", to_guard(wrist, G0, 0)); reach_report("g", oG, "R", to_guard(upper, G0, 0))
P2 = with_ground(dict(torso=(40, 0, 0), head=(-18, 0, 0), shR=(60, 0, 14), elR=(-4, 0, 0), shL=(60, 0, -14), elL=(-4, 0, 0),
    hipL=(-16, 0, 0), knL=18, hipR=(8, 0, 0), knR=20), feet=("L", "R"))
oP2 = solve(P2)
for G1 in ([0.46, 0, -0.55], [0.50, 0, -0.50]):
    wristP = oP2["handR"]; upperP = add(scale(add(oP2["shR"], oP2["elR"]), 0.5), [0, 0.04, 0])
    g = with_ground(dict(torso=(18, -12, 0), head=(-6, 8, 0), hipL=(-12, 0, -4), knL=14, hipR=(8, 0, 4), knR=12), feet=("L", "R"))
    g = arm_to(g, "L", to_guard(wristP, G1, 0), [-1, -1, 0])
    g = arm_to(g, "R", to_guard(upperP, G1, 0), [1, -0.6, -0.4])
    og = show("2-on-1 GUARD (prisoner's right) at %s" % G1, g, keys=("handL", "handR"))
    reach_report("g", og, "L", to_guard(wristP, G1, 0)); reach_report("g", og, "R", to_guard(upperP, G1, 0))
BF = with_ground(dict(torso=(34, -10, 0), head=(-26, 8, 0),
    shL=(50, 0, -14), elL=(-60, 0, 0), shR=(-62, 0, 10), elR=(-95, 0, 0),
    hipL=(-55, 0, -2), knL=80, hipR=(22, 0, 2), knR=26), feet=("R",))
show("Break free (corrected arms)", BF, keys=("handL", "handR", "head_c"))
