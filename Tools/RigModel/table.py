import sys
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from poses import *
L = {}
L["Walk contact (R heel)"] = with_ground(dict(torso=(3, 4, 0), head=(-2, -4, 0),
    shL=(-20, 0, -5), elL=(-24, 0, 0), shR=(14, 0, 5), elR=(-14, 0, 0), hipR=(-22, 0, 0), knR=3, hipL=(20, 0, 0), knL=10), feet=("L", "R"))
L["Walk passing (R stance)"] = with_ground(dict(torso=(3, 0, -2), head=(-2, 0, 2),
    shL=(-3, 0, -5), elL=(-18, 0, 0), shR=(-3, 0, 5), elR=(-18, 0, 0), hipR=(-6, 0, 0), knR=10, hipL=(-28, 0, 0), knL=56), feet=("R",))
L["Jog contact (R)"] = with_ground(dict(torso=(8, 7, 0), head=(-6, -7, 0),
    shL=(-36, 0, -8), elL=(-98, 0, 0), shR=(30, 0, 8), elR=(-78, 0, 0), hipR=(-22, 0, 0), knR=12, hipL=(-14, 0, 0), knL=80), feet=("R",))
L["Jog mid-stance (R)"] = with_ground(dict(torso=(8, 0, -1), head=(-6, 0, 1),
    shL=(-5, 0, -8), elL=(-92, 0, 0), shR=(-5, 0, 8), elR=(-92, 0, 0), hipR=(-10, 0, 0), knR=30, hipL=(-45, 0, 0), knL=95), feet=("R",))
L["Sprint contact (R)"] = with_ground(dict(torso=(14, 11, 0), head=(-12, -11, 0),
    shL=(-60, 0, -8), elL=(-102, 0, 0), shR=(50, 0, 10), elR=(-62, 0, 0), hipR=(-30, 0, 0), knR=22, hipL=(-30, 0, 0), knL=112), feet=("R",))
L["Sprint passing (R mid-stance)"] = with_ground(dict(torso=(14, 0, -1), head=(-12, 0, 1),
    shL=(-10, 0, -8), elL=(-95, 0, 0), shR=(-5, 0, 8), elR=(-90, 0, 0), hipR=(-12, 0, 0), knR=32, hipL=(-55, 0, 0), knL=110), feet=("R",))
L["Sprint toe-off (R)"] = with_ground(dict(torso=(14, -11, 0), head=(-12, 11, 0),
    shL=(50, 0, -10), elL=(-62, 0, 0), shR=(-62, 0, 8), elR=(-102, 0, 0), hipR=(26, 0, 0), knR=10, hipL=(-60, 0, 0), knL=90), feet=("R",))
L["Sprint start / accel (R drive)"] = with_ground(dict(torso=(36, -8, 0), head=(-28, 8, 0),
    shL=(55, 0, -10), elL=(-55, 0, 0), shR=(-70, 0, 8), elR=(-95, 0, 0), hipR=(22, 0, 0), knR=28, hipL=(-62, 0, 0), knL=95), feet=("R",))
for k, p in L.items():
    o = solve(p)
    print("%-32s model y %+.3f | soles L %+.3f R %+.3f | hands L %s R %s" % (k, p["model_pos"][1], o["soleLowL"], o["soleLowR"], fmt(o["handL"]), fmt(o["handR"])))
