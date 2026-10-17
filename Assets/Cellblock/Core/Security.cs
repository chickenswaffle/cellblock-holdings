using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable] public sealed class SecurityState
    {
        public bool GridEnabled = true;
        public List<Point> WiredRooms = new List<Point>();
        public List<SecurityDog> Dogs = new List<SecurityDog>();
        public double UtilityRemainder;
    }
    [Serializable] public sealed class SecurityDog
    {
        public string Id = "", KennelId = "", HandlerId = "", Activity = "Resting";
        public Point Position;
        public float Fatigue = 15;
        public int RestUntilMinute;
    }
    public sealed partial class State
    {
        public int SecurityVersion;
        public SecurityState Security = new SecurityState();
    }
    public static class SecurityRules
    {
        public static bool Outdoor(string kind) { return kind == "CCTV" || kind == "Floodlight" || kind == "PerimeterWire" || kind == "Kennel" || kind == "GuardTower" || kind == "ElectricalPanel"; }
        public static bool Equipment(string kind) { return Outdoor(kind) || kind == "ElectricalPanel" || kind == "ClimateUnit" || kind == "ControlComputer" || kind == "Armory"; }
        public static string LevelName(int level) { return level == 0 ? "Basic" : level == 1 ? "Improved" : "Enhanced"; }
        public static long Price(Fixture fixture)
        {
            long basic = FixtureCatalog.Get(fixture.Kind).PriceCents;
            return fixture.UpgradeLevel == 0 ? basic : fixture.UpgradeLevel == 1 ? basic * 17 / 10 + 15000 : basic * 26 / 10 + 40000;
        }
        public static float Demand(Fixture f)
        {
            if (f.Kind == "ControlComputer") return f.UpgradeLevel == 2 ? 0.12f : 0.2f;
            if (f.UpgradeLevel == 0) return 0;
            if (f.Kind == "ClimateUnit") return f.UpgradeLevel == 1 ? 1.5f : 1.1f;
            if (f.Kind == "Floodlight") return f.UpgradeLevel == 1 ? 0.18f : 0.12f;
            if (f.Kind == "CCTV") return 0.08f;
            if (f.Kind == "GuardTower") return 0.25f;
            if (f.Kind == "Kennel" && f.UpgradeLevel == 2) return 0.15f;
            if (f.Kind == "PerimeterWire" && f.UpgradeLevel == 2) return 0.04f;
            if (f.Kind == "Armory" && f.UpgradeLevel == 2) return 0.1f;
            return 0;
        }
        public static string Features(string kind, int level)
        {
            if(kind=="CCTV")return level==0?"Battery camera / 15 m day, 8 m unlit night":level==1?"Mains + infrared / 20 m day, 14 m night":"Efficient network camera / 25 m day, 20 m night";
            if(kind=="Floodlight")return level==0?"Solar area light / 7 m local illumination":level==1?"Powered floodlight / 11 m coverage":"Efficient LED array / 14 m coverage";
            if(kind=="PerimeterWire")return level==0?"Mesh barrier / physical footprint":level==1?"Reinforced wire top / stronger perimeter":"Reinforced barrier + powered proximity sensor";
            if(kind=="GuardTower")return level==0?"Lookout platform and ladder / assign an officer":level==1?"Powered searchlight, radio and longer sight range":"Searchlight + console + cooling / improved guard comfort";
            if(kind=="Kennel")return level==0?"Dry shelter / assign a qualified K9 handler":level==1?"Improved shelter and team rest":"Climate controlled shelter / reduced fatigue";
            if(kind=="Armory")return level==0?"Locked equipment storage":level==1?"Equipment for qualified armed assignments":"Powered monitored storage / armed assignments";
            if(kind=="ElectricalPanel")return "Room wiring / "+(4+level*4)+" kW additional capacity";
            if(kind=="ClimateUnit")return level==0?"Passive ventilation / +4 local comfort":level==1?"Active cooling / +15 comfort, 1.50 kW":"Efficient AC / +20 comfort, 1.10 kW";
            if(kind=="ControlComputer")return "Powered dispatch console / nearby on-shift staff improve response coordination"+(level==2?" / efficient terminal":"");
            return level==0?"Standard furnishing":(kind=="Bed"||kind=="Bench"||kind=="Desk")?"Improved comfort / +"+(level*2)+" local comfort":"Improved finish and fittings";
        }
    }

    public sealed partial class World
    {
        // Equipment is authoritative only in Data.Fixtures. Pending upgrades remain ordinary
        // construction, so neither queries nor a quote can grant power, comfort or detection early.
        void InitializeSecurity()
        {
            if (Data.SecurityVersion < 0 || Data.SecurityVersion > 1) throw new ArgumentException("Unsupported security save version.");
            if (Data.SecurityVersion == 0 && Data.Walls.Count > 0)
            {
                Data.Security = new SecurityState();
                foreach (List<Point> room in Rooms) Data.Security.WiredRooms.Add(room.Aggregate(new Point(), (sum,p) => sum+p) * (1f/room.Count));
                foreach (StaffMember staff in Data.Staff) { staff.Morale = 70; staff.SecurityRole = ""; staff.SecurityPostId = ""; staff.SecurityDailyCents = 0; }
                Data.SecurityVersion = 1;
            }
            if (Data.Security == null || Data.Security.WiredRooms == null || Data.Security.Dogs == null || Data.Security.WiredRooms.Any(p => !ValidPoint(p)) ||
                double.IsNaN(Data.Security.UtilityRemainder) || double.IsInfinity(Data.Security.UtilityRemainder) || Data.Security.UtilityRemainder < 0 || Data.Security.UtilityRemainder >= 1)
                throw new ArgumentException("Invalid utility state.");
            if (Data.Staff.Any(s => !FiniteRange(s.Morale,0,100) || s.SecurityDailyCents < 0 || s.PostArrivedMinute > Data.Minute ||
                !(new[] { "", "Tower", "K9", "Armed" }).Contains(s.SecurityRole ?? ""))) throw new ArgumentException("Invalid staff specialization.");
            foreach (SecurityDog dog in Data.Security.Dogs)
                if (dog == null || !ValidPoint(dog.Position) || !FiniteRange(dog.Fatigue,0,100) || string.IsNullOrEmpty(dog.Id)) throw new ArgumentException("Invalid dog team.");
            ReconcileSecurityPosts();
            var postIds=new System.Collections.Generic.HashSet<string>();
            foreach(StaffMember staff in Data.Staff)
            {
                string role=staff.SecurityRole??"";Fixture post=Data.Fixtures.FirstOrDefault(f=>f.Id==staff.SecurityPostId);
                if(role==""){if(!string.IsNullOrEmpty(staff.SecurityPostId)||staff.SecurityDailyCents!=0)throw new ArgumentException("Unassigned security post has wages or a reference.");continue;}
                string required=role=="Tower"?"GuardTower":role=="K9"?"Kennel":"Armory";
                long wage=role=="K9"?8000:role=="Armed"?10000:0;
                if(staff.Role!="Officer"||post==null||post.Kind!=required||!postIds.Add(post.Id)||staff.SecurityDailyCents!=wage||role=="K9"&&!staff.DogTrained||role=="Armed"&&(!staff.ArmedTrained||post.UpgradeLevel<1))throw new ArgumentException("Invalid security post assignment.");
            }
            if(Data.Security.Dogs.Select(d=>d.Id).Distinct().Count()!=Data.Security.Dogs.Count||Data.Security.Dogs.Select(d=>d.KennelId).Distinct().Count()!=Data.Security.Dogs.Count)throw new ArgumentException("Duplicate dog team.");
        }
        void ReconcileSecurityPosts()
        {
            Data.Security.Dogs.RemoveAll(d => !Data.Fixtures.Any(f => f.Id == d.KennelId && f.Kind == "Kennel"));
            foreach (StaffMember staff in Data.Staff)
                if (!string.IsNullOrEmpty(staff.SecurityPostId) && !Data.Fixtures.Any(f => f.Id == staff.SecurityPostId))
                { staff.SecurityPostId = ""; staff.SecurityRole = ""; staff.SecurityDailyCents = 0; staff.PostArrivedMinute = -1; }
        }
        public float PowerCapacityKw { get { return 8 + Data.Fixtures.Where(f => f.Kind == "ElectricalPanel" && f.Condition > 15).Sum(f => 4 + f.UpgradeLevel * 4); } }
        public float ExtraPowerDemandKw { get { return Data.Fixtures.Where(f => f.Condition > 15 && WiredAt(f.Position)).Sum(SecurityRules.Demand); } }
        public float PowerDemandKw { get { return ExtraPowerDemandKw + (Data.Operations.Lights ? Rooms.Count(RoomWired) * 0.08f : 0); } }
        public bool PowerOnline { get { return Data.Security.GridEnabled && PowerDemandKw <= PowerCapacityKw; } }
        public bool Night { get { int time = Data.Minute % 1440; return time < 360 || time >= 1140; } }
        public bool RoomWired(List<Point> room)
        {
            return Data.Security.WiredRooms.Any(p => Geometry.Contains(room,p)) || Data.Fixtures.Any(f => f.Kind == "ElectricalPanel" && f.Condition > 15 && Geometry.Contains(room,f.Position));
        }
        public bool RoomPowered(List<Point> room) { return PowerOnline && RoomWired(room); }
        bool WiredAt(Point position)
        {
            List<Point> room=Rooms.FirstOrDefault(r=>Geometry.Contains(r,position));
            if(room!=null)return RoomWired(room);
            return Geometry.Distance(position,new Point(-2,-8))<=25||Data.Fixtures.Any(f=>f.Kind=="ElectricalPanel"&&f.Condition>15&&Geometry.Distance(f.Position,position)<=16);
        }
        public bool PowerAt(Point position) { return PowerOnline && WiredAt(position); }
        public bool EquipmentOnline(Fixture fixture)
        {
            if (fixture == null || fixture.Condition <= 15) return false;
            return SecurityRules.Demand(fixture) == 0 || PowerAt(fixture.Position);
        }
        public string EquipmentStatus(Fixture fixture)
        {
            if (fixture == null) return "Select installed equipment.";
            if (fixture.Condition <= 15) return "Maintenance required";
            if (SecurityRules.Demand(fixture) > 0 && !PowerAt(fixture.Position)) return !Data.Security.GridEnabled ? "Grid isolated" : PowerDemandKw > PowerCapacityKw ? "Power capacity exceeded" : "Room needs an electrical panel";
            if (fixture.Kind == "GuardTower" && !StaffAtPost(fixture,"Tower")) return "Waiting for an on-shift lookout at the tower";
            if (fixture.Kind == "Kennel" && !Data.Staff.Any(s => s.SecurityPostId == fixture.Id && s.SecurityRole == "K9" && IsOnShift(s))) return "Dog resting / assign an on-shift handler";
            return "Operational";
        }
        public float LightRange(Fixture light) { return light.UpgradeLevel == 0 ? 7 : light.UpgradeLevel == 1 ? 11 : 14; }
        public bool IsIlluminated(Point point)
        {
            if (!Night) return true;
            if (Data.Operations.Lights && Data.Fixtures.Any(f=>f.Kind=="GuardTower"&&f.UpgradeLevel>0&&EquipmentOnline(f)&&StaffAtPost(f,"Tower")&&Geometry.Distance(f.Position,point)<12&&SightLines.Clear(f.Position,point)))return true;
            if (Data.Operations.Lights && Rooms.Any(r => Geometry.Contains(r,point) && RoomPowered(r))) return true;
            return Data.Fixtures.Any(f => f.Kind == "Floodlight" && Data.Operations.Lights && EquipmentOnline(f) && Geometry.Distance(f.Position,point) <= LightRange(f) && SightLines.Clear(f.Position,point));
        }
        public float CameraRangeFor(Fixture camera)
        {
            if (camera == null) return 0;
            if (camera.UpgradeLevel == 0) return !Night || IsIlluminated(camera.Position) ? 15 : 8;
            return camera.UpgradeLevel == 1 ? Night ? 14 : 20 : Night ? 20 : 25;
        }
        public Plan PlanSecurityUpgrade(string id)
        {
            Fixture old = Data.Fixtures.FirstOrDefault(f => f.Id == id);
            if (old == null || old.UpgradeLevel >= 2) return null;
            Fixture improved = old.Copy(); improved.UpgradeLevel++;
            var plan = new Plan { Name = "Upgrade " + FixtureCatalog.Get(old.Kind).Name + " / " + SecurityRules.LevelName(improved.UpgradeLevel) };
            plan.RemoveFixtures.Add(id); plan.UpgradeIds.Add(id); plan.Fixtures.Add(improved); return plan;
        }
        public bool StaffAtPost(Fixture post,string role)
        {
            return Data.Staff.Any(s => s.SecurityRole == role && s.SecurityPostId == post.Id && IsOnShift(s) && Geometry.Distance(s.Position,Geometry.AccessPoint(post)) < 0.3f && s.PostArrivedMinute >= 0 && Data.Minute-s.PostArrivedMinute >= 5);
        }
        public bool AssignSecurityOfficer(string postId,string officerId,string role)
        {
            Fixture post = Data.Fixtures.FirstOrDefault(f => f.Id == postId); StaffMember officer = Data.Staff.FirstOrDefault(s => s.Id == officerId && s.Role == "Officer");
            string required = role == "Tower" ? "GuardTower" : role == "K9" ? "Kennel" : role == "Armed" ? "Armory" : "";
            if (officer == null || post == null || required != post.Kind || (role == "Armed" && post.UpgradeLevel < 1)) { Notice = "Select an officer and an appropriate operational post; armed duty needs an upgraded armory."; return false; }
            if(IsWarden(officer)||IsSuspended(officer)){Notice="Choose an available line officer for this security post.";return false;}
            if (!EquipmentOnline(post) || Data.Projects.Any(p => !p.Completed && p.RemoveFixtures.Contains(postId)))
            { Notice = "Restore this post and finish its construction before assigning staff."; return false; }
            if (officer.SecurityPostId == postId && officer.SecurityRole == role) return false;
            if (Data.Staff.Any(s => s != officer && s.SecurityPostId == postId)) { Notice = "That post already has an assigned officer."; return false; }
            bool trained = role == "Armed" ? officer.ArmedTrained : role == "K9" ? officer.DogTrained : true;
            if (!trained && Data.CashCents < 75000) { Notice = "This qualification needs a $750 training payment."; return false; }
            if (!trained) { Book(-75000,"staff-training",officer.Name+" / "+role+" qualification"); if(role=="Armed") officer.ArmedTrained=true; else officer.DogTrained=true; }
            PersonnelFile dutyFile=FileFor(officer);if(dutyFile!=null){dutyFile.Duty="";dutyFile.WatchId="";dutyFile.MenteeId="";}
            officer.SecurityRole=role; officer.SecurityPostId=postId; officer.SecurityDailyCents=role=="Armed"?10000:role=="K9"?8000:0; officer.PostArrivedMinute=-1; officer.NextDutyMinute=0;
            Notice=officer.Name+" assigned to "+role+" duty. Arrival, shift and reachable access remain required."; return true;
        }
        public void ClearSecurityAssignment(string officerId)
        {
            StaffMember officer=Data.Staff.FirstOrDefault(s=>s.Id==officerId); if(officer==null)return;
            officer.SecurityRole=""; officer.SecurityPostId=""; officer.SecurityDailyCents=0; officer.PostArrivedMinute=-1; officer.NextDutyMinute=0;
        }
        public float ComfortAt(Point point)
        {
            float comfort=50;
            List<Point> room=Rooms.FirstOrDefault(r=>Geometry.Contains(r,point));
            foreach(Fixture f in Data.Fixtures)
            {
                bool local=room!=null ? Geometry.Contains(room,f.Position) : Geometry.Distance(f.Position,point)<4;
                if(!local || f.Condition<=15)continue;
                if(f.Kind=="GuardTower" && f.UpgradeLevel==2 && EquipmentOnline(f))comfort+=12;
                if(f.Kind=="ClimateUnit") comfort+=f.UpgradeLevel==0?4:EquipmentOnline(f)?f.UpgradeLevel==1?15:20:0;
                if((f.Kind=="Bed"||f.Kind=="Bench"||f.Kind=="Desk")&&f.UpgradeLevel>0)comfort+=f.UpgradeLevel*2;
            }
            if(Night && room!=null && !IsIlluminated(point))comfort-=4;
            return Geometry.Clamp(comfort,0,100);
        }
        public float PerimeterIntegrity
        {
            get { return Geometry.Clamp(Data.Fixtures.Where(f=>f.Kind=="PerimeterWire"&&f.Condition>15&&NearPerimeter(f.Position)).Sum(f=>3*(1+f.UpgradeLevel*0.25f))/(4*(Geometry.SiteX+Geometry.SiteY)),0,1); }
        }
        public static bool NearPerimeter(Point p) { return Geometry.SiteX-Math.Abs(p.X)<3.5f || Geometry.SiteY-Math.Abs(p.Y)<3.5f; }
        public string SecurityDetectionAt(Point point)
        {
            foreach(Fixture tower in Data.Fixtures.Where(f=>f.Kind=="GuardTower"&&EquipmentOnline(f)))
                if(StaffAtPost(tower,"Tower")&&Geometry.Distance(tower.Position,point)<=12+tower.UpgradeLevel*4&&SightLines.Clear(tower.Position,point))return "staffed guard tower";
            foreach(Fixture wire in Data.Fixtures.Where(f=>f.Kind=="PerimeterWire"&&f.UpgradeLevel==2&&EquipmentOnline(f)))
                if(Geometry.Distance(wire.Position,point)<2.5f&&SightLines.Clear(Geometry.AccessPoint(wire),point))return "perimeter sensor";
            foreach(SecurityDog dog in Data.Security.Dogs)
            {
                StaffMember handler=Data.Staff.FirstOrDefault(s=>s.Id==dog.HandlerId);
                if(handler!=null&&handler.SecurityRole=="K9"&&handler.SecurityPostId==dog.KennelId&&Data.Fixtures.Any(f=>f.Id==dog.KennelId&&EquipmentOnline(f))&&IsOnShift(handler)&&dog.Fatigue<80&&Geometry.Distance(handler.Position,dog.Position)<4&&Geometry.Distance(dog.Position,point)<6&&SightLines.Clear(dog.Position,point))return "dog and handler";
            }
            return "";
        }
        public bool DispatchOnline { get { return Data.Fixtures.Any(f => f.Kind == "ControlComputer" && EquipmentOnline(f) && Data.Staff.Any(s => IsOnShift(s) && Geometry.Distance(s.Position, Geometry.AccessPoint(f)) < 2 && SightLines.Clear(s.Position,Geometry.AccessPoint(f)))); } }
        public float ResponseSpeed { get { return (Data.Operations.Alarm ? 0.68f : 0.55f) * (DispatchOnline ? 1.1f : 1); } }
        public float SecurityDeterrenceAt(Point point)
        {
            bool armed=Data.Staff.Any(s=>s.SecurityRole=="Armed"&&s.ArmedTrained&&IsOnShift(s)&&Data.Fixtures.Any(f=>f.Id==s.SecurityPostId&&f.Kind=="Armory"&&EquipmentOnline(f))&&Geometry.Distance(s.Position,point)<8&&SightLines.Clear(s.Position,point));
            return Math.Min(8,PerimeterIntegrity*20)+(SecurityDetectionAt(point)!=""?6:0)+(armed?4:0);
        }
        void AdvanceSecurity()
        {
            ReconcileSecurityPosts();
            if(Data.Security.GridEnabled&&PowerOnline)
            {
                // Starter lighting is already in contract operating costs. Only added equipment
                // demand is metered here, at eighteen cents per kWh, on explicit game minutes.
                Data.Security.UtilityRemainder+=ExtraPowerDemandKw*18.0/60;
                long cents=(long)Data.Security.UtilityRemainder; Data.Security.UtilityRemainder-=cents; if(cents>0)Book(-cents,"utilities","Additional equipment electricity");
            }
            foreach(Fixture kennel in Data.Fixtures.Where(f=>f.Kind=="Kennel"))
            {
                SecurityDog dog=Data.Security.Dogs.FirstOrDefault(d=>d.KennelId==kennel.Id);
                if(dog==null){dog=new SecurityDog{Id="dog/"+kennel.Id,KennelId=kennel.Id,Position=Geometry.AccessPoint(kennel)};Data.Security.Dogs.Add(dog);}
                StaffMember handler=Data.Staff.FirstOrDefault(s=>s.SecurityPostId==kennel.Id&&s.SecurityRole=="K9"&&IsOnShift(s));
                if(dog.Fatigue>=90)dog.RestUntilMinute=Data.Minute+120;
                bool active=handler!=null&&EquipmentOnline(kennel)&&Data.Minute>=dog.RestUntilMinute;
                dog.HandlerId=active?handler.Id:""; Point target=active?NearbyClear(handler.Position+new Point(-0.65f,0),Routes):Geometry.AccessPoint(kennel);
                Walk(dog.Id,ref dog.Position,target,active?0.44f:0.35f); dog.Activity=active?"Patrolling with handler":"Resting at kennel";
                dog.Fatigue=Geometry.Clamp(dog.Fatigue+(active?0.025f*(kennel.UpgradeLevel==2?0.7f:kennel.UpgradeLevel==1?0.85f:1):-0.10f),0,100);
            }
            if(Data.Minute%30!=0)return;
            foreach(StaffMember staff in Data.Staff.Where(IsOnShift))
            {
                float target=Geometry.Clamp(70+(ComfortAt(staff.Position)-50)*0.7f,0,100);
                staff.Morale+=Geometry.Clamp(target-staff.Morale,-0.4f,0.4f); staff.Pressure=Geometry.Clamp(staff.Pressure+(70-staff.Morale)*0.003f,0,100);
            }
            foreach(Resident resident in Data.Residents.Where(IsPresent))
            {
                resident.Stress=Geometry.Clamp(resident.Stress+(50-ComfortAt(resident.Home))*0.006f,0,100);
                foreach(StaffMember armed in Data.Staff.Where(s=>s.SecurityRole=="Armed"&&IsOnShift(s)&&Geometry.Distance(s.Position,resident.Position)<6&&SightLines.Clear(s.Position,resident.Position)))
                    resident.Stress=Geometry.Clamp(resident.Stress+0.06f,0,100);
            }
            if(Data.Minute%60==0)foreach(Fixture f in Data.Fixtures.Where(f=>SecurityRules.Equipment(f.Kind)&&f.Kind!="CCTV"))f.Condition=Math.Max(0,f.Condition-0.12f);
        }
    }
}
