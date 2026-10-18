import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from poses import *

SA = with_ground(dict(torso=(24, 26, -6), head=(-16, -18, 4),
    shL=(52, 0, -16), elL=(-8, 0, 0), shR=(-115, 0, 36), elR=(-30, 0, 0),
    hipR=(-34, 0, 2), knR=44, hipL=(26, 0, -2), knL=12), feet=("L", "R"))
oA = solve(SA)
for gp in ([-0.36, 0, -0.42], [-0.40, 0, -0.36]):
    graspW = oA["handL"]; graspU = add(scale(add(oA["shL"], oA["elL"]), 0.5), [0, 0.03, 0])
    GA = with_ground(dict(torso=(-6, -18, 0), head=(4, 14, 0), hipL=(-26, 0, -10), knL=30, hipR=(22, 0, 10), knR=22), feet=("L", "R"))
    GA = arm_to(GA, "R", to_guard(graspW, gp, 0), [1, -1, 0])
    GA = arm_to(GA, "L", to_guard(graspU, gp, 0), [-1, -1, 0])
    og = show("Struggle A guard at %s" % gp, GA, keys=("handL", "handR", "head_c"))
    reach_report("guard", og, "R", to_guard(graspW, gp, 0)); reach_report("guard", og, "L", to_guard(graspU, gp, 0))
# Struggle B: escapee pivots toward the guard (root yaw -50) and hooks with the right
SB = with_ground(dict(yaw=-50, torso=(14, -34, 6), head=(-10, 24, -4),
    shL=(40, 0, -26), elL=(-20, 0, 0), shR=(-100, -30, 20), elR=(-32, 0, 0),
    hipL=(-20, 0, -6), knL=28, hipR=(22, 0, 6), knR=12), feet=("L", "R"))
oB = show("Struggle B: pivot toward guard + right hook (root yaw -50)", SB, keys=("handR", "handL", "head_c"))
print("   guard head approx (root frame of escapee):", fmt([-0.36, 1.58, -0.42]), " fist->guard head dist %.2f" % length(sub(oB["handR"], [-0.36, 1.58, -0.42])))
GB = dict(torso=(10, 28, 0), head=(-6, 70, 0))
show("Glance back over R shoulder (overlay, +Y = turn right)", GB, keys=("head_c",))
