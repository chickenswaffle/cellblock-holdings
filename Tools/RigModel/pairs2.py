import sys, math
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from pairs import to_guard, arm_to, reach_report
from poses import *

def torso_target(pose, local):
    o = solve(pose); T = o["torso"]; return T.point(local)

def cuffs(pose):
    p = dict(pose)
    p = arm_to(p, "L", torso_target(p, [-0.035, 0.17, -0.20]), mv(solve(p)["torso"].R, [-1, 0.2, -0.6]))
    p = arm_to(p, "R", torso_target(p, [0.035, 0.17, -0.20]), mv(solve(p)["torso"].R, [1, 0.2, -0.6]))
    return p

def ground_point(pose, key, y=-0.01 + 0.07):
    p = dict(pose); o = solve(p); mp = list(p.get("model_pos", (0, 0, 0)))
    mp[1] += y - o[key][1]; p["model_pos"] = tuple(round(v, 3) for v in mp); return p

# Cuffed hands (standing) and cuffed kneel (tucked toes: knees ~112 deg, thighs vertical)
stand_cuffed = with_ground(cuffs(dict(torso=(6, 0, 0), head=(12, 0, 0))), feet=("L", "R"))
o = show("Cuffed standing (hands behind back)", stand_cuffed, keys=("handL", "handR", "elL", "elR"))
print("   hand gap %.3f" % length(sub(o["handL"], o["handR"])))
kneel = cuffs(dict(torso=(8, 0, 0), head=(16, 0, 0), hipL=(-2, 0, -5), knL=112, hipR=(-2, 0, 5), knR=112))
kneel = ground_point(kneel, "kneeL")
o = show("Cuffed kneel (prisoner)", kneel, keys=("handL", "handR", "kneeL", "toeL", "head_c"))
print("   hand gap %.3f" % length(sub(o["handL"], o["handR"])))

# Surrender kneel, hands on head
sur = dict(torso=(2, 0, 0), head=(10, 0, 0), hipL=(-2, 0, -5), knL=112, hipR=(-2, 0, 5), knR=112)
sur = ground_point(sur, "kneeL")
o0 = solve(sur)
sur = arm_to(sur, "L", add(o0["head_c"], [-0.05, 0.12, -0.02]), mv(o0["torso"].R, [-1, 0.3, 0.2]))
sur = arm_to(sur, "R", add(o0["head_c"], [0.05, 0.12, -0.02]), mv(o0["torso"].R, [1, 0.3, 0.2]))
show("Surrender kneel (hands on head)", sur, keys=("handL", "handR", "elL", "elR"))

# Prone, arms out (pinned before cuffs). Body pitched 90 about the feet, then lifted and pulled back
prone = dict(model_rot=(90, 0, 0), model_pos=(0, 0.0, -0.90), head=(0, 75, 0),
             shL=(0, 0, -80), elL=(-80, 0, 0), shR=(0, 0, 80), elR=(-80, 0, 0),
             hipL=(0, 0, -6), knL=6, hipR=(0, 0, 6), knR=14)
o = solve(prone); low = min(o["chest"][1], o["belly"][1])
mp = list(prone["model_pos"]); mp[1] += (-0.01 + 0.0) - low; prone["model_pos"] = tuple(round(v, 3) for v in mp)
o = show("Prone pinned, arms out", prone, keys=("head_c", "chest", "belly", "waist", "handL", "handR", "elL", "toeL"))
prone_c = cuffs(dict(prone, shL=(0, 0, 0), shR=(0, 0, 0)))
o = show("Prone cuffed", prone_c, keys=("handL", "handR", "head_c"))
print("   hand gap %.3f" % length(sub(o["handL"], o["handR"])))
