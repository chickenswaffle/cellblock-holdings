import sys
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from poses import *

def arm_to(pose, side, target_root, pole):
    e, el = solve_arm(pose, side, target_root, pole)
    e, el = canonical_arm(e, el)
    p = dict(pose); p["sh" + side] = e; p["el" + side] = el
    return p

def tlocal(o, pt):
    """point -> torso-local coordinates"""
    T = o["torso"]; return mv(transpose(T.R), sub(pt, T.p))

# 1. Fight stance (orthodox: left foot/shoulder forward), fists at chin height
stance = with_ground(dict(torso=(8, 22, 0), head=(-6, -20, 0),
    hipL=(-20, 0, -6), knL=24, hipR=(12, 0, 8), knR=28), feet=("L", "R"))
o = solve(stance)
# fists: lead 0.30 m in front of the face, rear near the chin (root frame)
lead = add(o["head_c"], [-0.03, -0.12, 0.28]); rear = add(o["head_c"], [0.10, -0.16, 0.16])
stance = arm_to(stance, "L", lead, [-1, -1, -0.3]); stance = arm_to(stance, "R", rear, [1, -1, -0.5])
show("Fight stance (orthodox)", stance, keys=("handL", "handR", "head_c", "elL", "elR"))

# 2. Haymaker wind-up (right): shoulder back/out/up, chest turned right (right shoulder back = torso Y +)
wind = with_ground(dict(torso=(4, 38, -8), head=(-4, -34, 6),
    shL=(-72, 0, -12), elL=(-35, 0, 0), shR=(20, 0, 70), elR=(-95, 0, 0),
    hipL=(-22, 0, -6), knL=16, hipR=(14, 0, 10), knR=34), feet=("L", "R"))
show("Haymaker wind-up (R)", wind, keys=("handL", "handR", "head_c", "elR"))

# 3. Haymaker impact: chest whipped left (torso Y -), right arm sweeping across at head height
hay = with_ground(dict(model_pos=(0, 0, 0.12), torso=(16, -36, 6), head=(-10, 26, -4),
    shL=(18, 0, -28), elL=(-85, 0, 0), shR=(-84, -28, 22), elR=(-28, 0, 0),
    hipL=(-28, 0, -6), knL=32, hipR=(26, 0, 6), knR=8), feet=("L",))
show("Haymaker impact (R)", hay, keys=("handL", "handR", "head_c", "elR"))

# 4. Straight right (cross) at full extension
cross_ = with_ground(dict(model_pos=(0, 0, 0.10), torso=(10, -24, 0), head=(-8, 22, 0),
    hipL=(-26, 0, -6), knL=30, hipR=(22, 0, 8), knR=10), feet=("L",))
o = solve(cross_)
cross_ = arm_to(cross_, "R", add(o["head_c"], [-0.02, -0.06, 0.60]), [1, -0.3, -1])
o = solve(cross_)
cross_ = arm_to(cross_, "L", add(o["head_c"], [-0.05, -0.12, 0.22]), [-1, -1, -0.3])
show("Straight right (impact)", cross_, keys=("handL", "handR", "head_c", "elR"))

# 5. Shove: load and extend (two hands at the opponent's chest ~0.55-0.65 m ahead)
shove_load = with_ground(dict(torso=(6, 0, 0), head=(-4, 0, 0),
    hipL=(-24, 0, -4), knL=28, hipR=(16, 0, 4), knR=22), feet=("L", "R"))
o = solve(shove_load)
for s, x in (("L", -0.16), ("R", 0.16)):
    shove_load = arm_to(shove_load, s, [x, 1.22, 0.24], [x * 6, -1, -0.6])
show("Shove load", shove_load, keys=("handL", "handR", "head_c"))
shove = with_ground(dict(model_pos=(0, 0, 0.15), torso=(22, 0, 0), head=(-14, 0, 0),
    hipL=(-32, 0, -4), knL=36, hipR=(30, 0, 4), knR=6), feet=("L", "R"))
for s, x in (("L", -0.15), ("R", 0.15)):
    shove = arm_to(shove, s, [x, 1.22, 0.78], [x * 6, -1, -0.4])
show("Shove extend", shove, keys=("handL", "handR", "head_c"))
