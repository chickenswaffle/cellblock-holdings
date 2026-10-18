import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from pairs2 import cuffs, ground_point
from pairs4 import pivoted
from poses import *

dive = dict(model_rot=(68, 0, 0), torso=(4, 0, 0), head=(-48, 0, 0),
    shL=(-115, 0, -14), elL=(-18, 0, 0), shR=(-115, 0, 14), elR=(-18, 0, 0),
    hipL=(16, 0, -4), knL=30, hipR=(4, 0, 4), knR=62)
dive["model_pos"] = pivoted(dive["model_rot"], (0, -0.08, 0))
show("Tackle dive (airborne, COM pivot)", dive, keys=("head_c", "handL", "handR", "waist", "toeL"))

E = with_ground(dict(torso=(16, 0, 0), hipL=(-30, 0, 0), knL=40, hipR=(26, 0, 0), knR=20), feet=("L",))
oE = solve(dict(E, root=(0, 0, 0.52)))
wrap = dict(model_rot=(50, 0, 0), torso=(6, 0, 0), head=(-30, 34, 0), hipL=(20, 0, -4), knL=30, hipR=(8, 0, 4), knR=58)
wrap["model_pos"] = pivoted(wrap["model_rot"], (0, -0.12, 0.0))
lw = add(oE["waist"], [0.16, -0.08, 0.06]); rw = add(oE["waist"], [-0.16, -0.08, 0.06])
o0 = solve(wrap)
wrap = arm_to(wrap, "L", lw, mv(o0["torso"].R, [-1, 0.2, 0]))
wrap = arm_to(wrap, "R", rw, mv(o0["torso"].R, [1, 0.2, 0]))
ow = show("Tackle wrap (escapee root 0.52 m ahead)", wrap, keys=("head_c", "handL", "handR", "chest", "waist"))
reach_report("guard", ow, "L", lw); reach_report("guard", ow, "R", rw)
print("   escapee waist %s back-surface z %.3f | guard chest %s head %s" % (fmt(oE["waist"]), oE["back"][2], fmt(ow["chest"]), fmt(ow["head_c"])))

# ---- Grab from behind + struggle (escapee E at origin; guard G behind-left at (-0.22, 0, -0.62)) ----
Gpos = [-0.22, 0, -0.62]
SA = with_ground(dict(torso=(24, 26, -6), head=(-16, -18, 4),
    shL=(52, 0, -16), elL=(-8, 0, 0), shR=(-115, 0, 36), elR=(-30, 0, 0),
    hipR=(-34, 0, 2), knR=44, hipL=(26, 0, -2), knL=12), feet=("L", "R"))
oA = show("Struggle A: escapee yanks away (held by L arm)", SA, keys=("handL", "elL", "handR", "head_c"))
graspW = oA["handL"]; graspU = add(scale(add(oA["shL"], oA["elL"]), 0.5), [0, 0.03, 0])
GA = with_ground(dict(torso=(-8, -12, 0), head=(6, 10, 0), hipL=(-26, 0, -10), knL=30, hipR=(22, 0, 10), knR=22), feet=("L", "R"))
o0 = solve(GA)
GA = arm_to(GA, "R", to_guard(graspW, Gpos, 0), [1, -1, 0])
GA = arm_to(GA, "L", to_guard(graspU, Gpos, 0), [-1, -1, 0])
og = show("Struggle A: guard bracing (root -0.22, 0, -0.62)", GA, keys=("handL", "handR"))
reach_report("guard", og, "R", to_guard(graspW, Gpos, 0)); reach_report("guard", og, "L", to_guard(graspU, Gpos, 0))

SB = with_ground(dict(torso=(10, -40, 8), head=(-6, -30, 0),
    shL=(30, 0, -30), elL=(-50, 0, 0), shR=(-88, -62, 30), elR=(-60, 0, 0),
    hipR=(-12, 0, 8), knR=30, hipL=(18, 0, -8), knL=24), feet=("L", "R"))
oB = show("Struggle B: escapee twists back, swings R elbow/fist at guard", SB, keys=("handL", "handR", "elR", "head_c"))
print("   guard head (root frame) approx", fmt(add(Gpos, [0, 1.62, 0])))

# Break free burst (start of sprint, arm ripped forward-down)
BF = with_ground(dict(torso=(34, -10, 0), head=(-26, 8, 0),
    shL=(-40, 0, -20), elL=(-70, 0, 0), shR=(60, 0, 14), elR=(-60, 0, 0),
    hipL=(-55, 0, -2), knL=80, hipR=(22, 0, 2), knR=26), feet=("R",))
show("Break free (rip arm, first drive step)", BF, keys=("handL", "handR", "head_c"))

# Stagger / hit-react (shoved or punched in the chest): lean back, arms flung, step back
ST = with_ground(dict(torso=(-18, 10, 8), head=(-22, -6, 6),
    shL=(-38, 0, -62), elL=(-30, 0, 0), shR=(-30, 0, 70), elR=(-25, 0, 0),
    hipR=(28, 0, 6), knR=14, hipL=(-12, 0, -6), knL=22), feet=("L",))
show("Stagger back (hit react)", ST, keys=("handL", "handR", "head_c"))

# Exhausted: hands on knees
EX = with_ground(dict(torso=(52, 0, 0), head=(-38, 0, 0), hipL=(-34, 0, -8), knL=40, hipR=(-34, 0, 8), knR=40), feet=("L", "R"))
o0 = solve(EX)
EX = arm_to(EX, "L", add(o0["kneecapL"], [-0.02, 0.06, 0.02]), [-1, 0, -0.5])
EX = arm_to(EX, "R", add(o0["kneecapR"], [0.02, 0.06, 0.02]), [1, 0, -0.5])
oX = show("Exhausted: hands on knees", EX, keys=("handL", "handR", "kneecapL", "head_c"))

# Glance back over the right shoulder while running (overlay on the run cycle)
GB = dict(torso=(10, -28, 0), head=(-6, -70, 0))
show("Glance back over R shoulder (overlay)", GB, keys=("head_c",))

# Stumble (trip / catch step)
SU = with_ground(dict(torso=(40, 6, -8), head=(-30, 0, 6),
    shL=(-70, 0, -46), elL=(-40, 0, 0), shR=(-58, 0, 50), elR=(-35, 0, 0),
    hipR=(-48, 0, 4), knR=58, hipL=(30, 0, -4), knL=24), feet=("R",))
show("Stumble (catch step on R)", SU, keys=("handL", "handR", "head_c"))
