using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // A county company held still (no transfers, no court) with calm relationships, so a check controls every plan.
    static World IntelWorld()
    {
        World w = EconomyWorld(); StayPut(w);
        foreach (Resident r in w.Data.Residents) foreach (Relationship rel in r.Relationships) { rel.Tension = 10; rel.Trust = 30; }
        return w;
    }
    static void NextDay(World w) { w.Data.Minute += 1440; }
    static Relationship Link(Resident a, Resident b) { return a.Relationships.First(x => x.OtherId == b.Id); }
    static void Pair(Resident a, Resident b, float tension, float trust) { Relationship x = Link(a, b), y = Link(b, a); x.Tension = y.Tension = tension; x.Trust = y.Trust = trust; }
    static Resident Cellmate(World w, Resident r) { var room = w.Rooms.First(x => Geometry.Contains(x, r.Home)); return w.Data.Residents.First(o => o != r && Geometry.Contains(room, o.Home)); }
    static Resident Stranger(World w, Resident r) { var room = w.Rooms.First(x => Geometry.Contains(x, r.Home)); return w.Data.Residents.First(o => o != r && !Geometry.Contains(room, o.Home)); }
    static void RunUntilTip(World w, Tip t, int limit) { int start = w.Data.Minute; while ((t.Status == "Acting" || t.Status == "New" || t.Status == "Corroborating") && w.Data.Minute - start < limit) w.AdvanceMinutes(1); }

    static void RunIntelChecks()
    {
        Test("saves from before intelligence work gain it with nobody under suspicion", () => {
            World source = IntelWorld(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("IntelWork"); json["IntelVersion"] = 0;
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.IntelWork.Version == IntelRules.Version && loaded.IntelWork.Policy == "Protect sources" && Math.Abs(loaded.IntelWork.Trust - 60) < 0.01f, "Intelligence defaults");
            Check(loaded.Data.Residents.All(r => r.Suspicion == 0 && !r.Jacket && !r.ProtectiveCustody), "Old saves start with suspicion");
        });
        Test("a stash is known to the holder's cellmates and friends, not to strangers", () => {
            World w = IntelWorld(); Resident holder = w.Data.Residents[0], mate = Cellmate(w, holder), stranger = Stranger(w, holder), friend = w.Data.Residents.Last(r => r != mate && r != stranger && r != holder && !Geometry.Contains(w.Rooms.First(x => Geometry.Contains(x, holder.Home)), r.Home));
            Pair(holder, friend, 5, 80);
            ContrabandItem item = w.Introduce("Drugs", "Throw-over", holder);
            Intel intel = w.IntelWork.Intel.Single(i => i.ItemId == item.Id);
            Check(intel.Kind == "Stash" && intel.SubjectId == holder.Id && intel.Knowers.Contains(holder.Id) && intel.Knowers.Contains(mate.Id) && intel.Knowers.Contains(friend.Id) && !intel.Knowers.Contains(stranger.Id), "Knowers: " + string.Join(",", intel.Knowers));
            Check(w.Introduce("Tools", "Throw-over", holder) != null && w.IntelWork.Intel.Any(i => i.Kind == "Escape" && i.SubjectId == holder.Id), "Escape tools were not escape intel");
        });
        Test("a tip-led cell search finds the stash, confirms the tip and puts the few who knew under suspicion", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review");
            Resident holder = w.Data.Residents[0], mate = Cellmate(w, holder);
            ContrabandItem item = w.Introduce("Weapon", "Made inside", holder);
            Intel intel = w.IntelWork.Intel.Single(i => i.ItemId == item.Id);
            Tip tip = w.AddTip(mate.Id, "Note", intel.Kind, intel.SubjectId, "", intel.Position, true, intel.Id, w.Data.Minute + 3 * 1440, "check");
            float trust = w.IntelWork.Trust, before = mate.Suspicion;
            Check(w.ActOnTip(tip.Id, "Search cell") && tip.Status == "Acting", w.Notice);
            RunUntilTip(w, tip, 600);
            Check(tip.Status == "Confirmed" && !w.Contraband.Items.Contains(item) && w.IntelWork.TipsConfirmed == 1, "Search result " + tip.Status);
            Check(mate.Suspicion > before + 4 && w.IntelWork.Trust > trust, "Exposure or trust: " + mate.Suspicion + " / " + w.IntelWork.Trust);
            Check(w.IntelWork.SnitchHunts == 1 && w.IntelWork.Intel.Any(i => i.Kind == "Hit"), "A weapon bust did not start a hunt for the leak");
        });
        Test("a false tip searches an innocent cell, stresses them and costs trust", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review"); w.Contraband.Items.Clear();
            Resident target = w.Data.Residents[3];
            Tip tip = w.AddTip(w.Data.Residents[4].Id, "Note", "Stash", target.Id, "", target.Home, false, "", w.Data.Minute + 3 * 1440, "invented");
            float stress = target.Stress, trust = w.IntelWork.Trust;
            w.ActOnTip(tip.Id, "Search cell"); RunUntilTip(w, tip, 600);
            Check(tip.Status == "False" && w.IntelWork.TipsFalse == 1 && target.Stress >= stress + 5 && w.IntelWork.Trust <= trust - 2.5f, "False search consequences: " + tip.Status + " stress " + target.Stress + " trust " + w.IntelWork.Trust);
        });
        Test("searching the whole unit hides the source better and stresses everyone in the room", () => {
            float Exposed(string action, out float roomStress)
            {
                World w = IntelWorld(); w.SetIntelPolicy("Hold for review");
                Resident holder = w.Data.Residents[0], mate = Cellmate(w, holder);
                ContrabandItem item = w.Introduce("Drugs", "Throw-over", holder);
                Intel intel = w.IntelWork.Intel.Single(i => i.ItemId == item.Id);
                Tip tip = w.AddTip(mate.Id, "Informant", intel.Kind, holder.Id, "", intel.Position, true, intel.Id, w.Data.Minute + 3 * 1440, "check");
                var room = w.Rooms.First(x => Geometry.Contains(x, holder.Home));
                var inRoom = w.Data.Residents.Where(r => Geometry.Contains(room, r.Home) && r != holder).ToList();
                float stress = inRoom.Sum(r => r.Stress);
                w.ActOnTip(tip.Id, action); RunUntilTip(w, tip, 900);
                roomStress = inRoom.Sum(r => r.Stress) - stress;
                return mate.Suspicion;
            }
            float cellStress, unitStress;
            float cell = Exposed("Search cell", out cellStress), unit = Exposed("Search the unit", out unitStress);
            Check(unit < cell * 0.5f && unitStress > cellStress, "Unit search exposure " + unit + " vs cell " + cell + "; stress " + unitStress + " vs " + cellStress);
        });
        Test("a warned attack is prevented by separation; an ignored one becomes an audit finding", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review");
            Resident actor = w.Data.Residents[0], target = w.Data.Residents[1];
            Intel hit = w.NoteHit(actor, target, 3);
            Tip tip = w.AddTip(w.Data.Residents[2].Id, "Note", "Hit", target.Id, actor.Id, target.Home, true, hit.Id, w.Data.Minute + 2 * 1440, "check");
            int prevented = w.Personnel.FightsPrevented;
            Check(w.ActOnTip(tip.Id, "Separate them") && tip.Status == "Confirmed", w.Notice);
            Check(actor.SeparatedUntil >= w.Data.Minute + 700 && actor.Intake.KeepSeparate.Contains(target.Id) && !w.IntelWork.Intel.Any(i => i.Kind == "Hit") && w.Personnel.FightsPrevented == prevented + 1, "Separation did not take");
            World v = IntelWorld(); v.SetIntelPolicy("Hold for review");
            Resident a = v.Data.Residents[0], b = v.Data.Residents[1];
            Intel planned = v.NoteHit(a, b, 3);
            Tip warning = v.AddTip(v.Data.Residents[2].Id, "Note", "Hit", b.Id, a.Id, b.Home, true, planned.Id, v.Data.Minute + 2 * 1440, "check");
            v.ActOnTip(warning.Id, "Ignore");
            Check(v.IntelFightFactor(a, b) == 2, "A planned attack did not double the fight chance");
            v.AfterFight(a, b);
            Check(v.IntelWork.IgnoredWarnings == 1 && v.Inspect(false).Findings.Any(f => f.Text.Contains("warning about an attack")), "An ignored warning was not on the audit");
        });
        Test("drones are ordered hours ahead; officers posted on a true tip meet the drone and take the drop", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review"); w.Contraband.Drones.Clear();
            Resident buyer = w.Data.Residents[0]; w.Introduce("Phone", "Throw-over", buyer);
            Point target = w.Data.Yard + new Point(1, 1);
            w.PlanDrop(4242, target, true, buyer);
            Intel drop = w.IntelWork.Intel.Single(i => i.Kind == "Drop");
            Check(drop.LaunchMinute >= w.Data.Minute + 90 && drop.LaunchMinute <= w.Data.Minute + 240 && drop.Knowers.Contains(buyer.Id) && w.Contraband.Drones.Count == 0, "The drone was not planned ahead");
            Tip tip = w.AddTip(Cellmate(w, buyer).Id, "Informant", "Drop", buyer.Id, "", drop.Position, true, drop.Id, drop.ExpiresMinute, "check");
            w.ActOnTip(tip.Id, "Post an officer");
            int seized = w.Contraband.Seized;
            RunUntil(w, () => tip.Status != "Acting", 400, "The stakeout");
            Check(tip.Status == "Confirmed", "Stakeout ended " + tip.Status);
            RunUntil(w, () => w.Contraband.Drones.Count == 0, 200, "The drone leaving");
            Check(w.Contraband.Seized > seized && !w.Contraband.Items.Any(i => i.Place == "Dropped"), "The waiting officer did not take the drop");
        });
        Test("a double agent in the ordering gang points officers at a decoy", () => {
            World w = IntelWorld(); Resident buyer = w.Data.Residents[0], agent = w.Data.Residents[1];
            buyer.Gang = agent.Gang = "Harbor Union";
            w.IntelWork.Informants.Add(new Informant { ResidentId = agent.Id, DoubleAgent = true, Reliability = 80 });
            w.PlanDrop(777, w.Data.Yard, true, buyer);
            Tip decoy = w.IntelWork.Tips.Single(t => t.Kind == "Drop");
            Check(!decoy.Truth && decoy.SourceId == agent.Id && Geometry.Distance(decoy.Position, w.Data.Yard) > 6, "No decoy tip: " + decoy.Position.X + "," + decoy.Position.Y);
        });
        Test("recruiting informants needs a handler, leverage helps, and a refusal is noticed", () => {
            World w = IntelWorld(); Resident r = w.Data.Residents[0];
            float plain = w.RecruitChance(r, "Store credit");
            r.Legal.Status = "Pretrial"; r.Safety = 30;
            float leverage = w.RecruitChance(r, "Court letter");
            Check(leverage > plain + 0.3f, "Leverage did not help: " + plain + " → " + leverage);
            foreach (StaffMember s in w.Data.Staff) { s.ShiftStart = (w.Data.Minute / 60 + 12) % 24; }
            Check(w.ApproachBlocker(r).Contains("handler"), "Approached without a handler on shift");
            World v = IntelWorld(); Resident a = v.Data.Residents[0];
            int approaches = 0; float suspicion = 0; bool recruited = false;
            foreach (Resident x in v.Data.Residents.Take(6)) { if (v.InformantOf(x) != null || v.IntelWork.Informants.Count >= v.InformantCap) break; x.Safety = 20; x.Legal.Status = "Pretrial"; bool ok = v.ApproachInformant(x.Id, "Court letter"); approaches++; recruited |= ok; suspicion = Math.Max(suspicion, x.Suspicion); }
            Check(approaches > 0 && suspicion >= 4 && v.IntelWork.Approaches == approaches, "Approaches were not noticed");
            Check(v.IntelWork.Informants.Count <= 2, "More than two informants without the intelligence unit");
        });
        Test("reliable informants report what they know; double agents mostly mislead", () => {
            World w = IntelWorld(); Resident holder = w.Data.Residents[0], inf = Cellmate(w, holder);
            w.Introduce("Drugs", "Throw-over", holder);
            w.IntelWork.Informants.Add(new Informant { ResidentId = inf.Id, Reliability = 100 });
            for (int d = 0; d < 6 && !w.IntelWork.Tips.Any(); d++) { w.InformantTips(); NextDay(w); }
            Tip tip = w.IntelWork.Tips.FirstOrDefault();
            Check(tip != null && tip.Truth && tip.SubjectId == holder.Id && tip.SourceId == inf.Id && inf.Suspicion > 0, "A reliable informant did not report a known stash");
            World v = IntelWorld(); Resident liar = v.Data.Residents[1]; Pair(liar, v.Data.Residents[2], 60, 10);
            v.IntelWork.Informants.Add(new Informant { ResidentId = liar.Id, DoubleAgent = true, Reliability = 90 });
            for (int d = 0; d < 30; d++) { v.InformantTips(); NextDay(v); }
            Check(v.IntelWork.Tips.Count > 0 && v.IntelWork.Tips.Count(t => !t.Truth) * 2 > v.IntelWork.Tips.Count, "A double agent told the truth: " + v.IntelWork.Tips.Count(t => !t.Truth) + " false of " + v.IntelWork.Tips.Count);
        });
        Test("anonymous notes come from grudges and fear, and some are inventions", () => {
            World w = IntelWorld(); Resident holder = w.Data.Residents[0], mate = Cellmate(w, holder);
            Pair(holder, mate, 65, 10);
            w.Introduce("Drugs", "Throw-over", holder);
            for (int d = 0; d < 60 && !w.IntelWork.Tips.Any(t => t.SourceId == mate.Id); d++) { w.Kites(); NextDay(w); }
            Tip kite = w.IntelWork.Tips.FirstOrDefault(t => t.SourceId == mate.Id);
            Check(kite != null && kite.Truth && kite.SubjectId == holder.Id && kite.Source == "Note", "A cellmate with a grudge never dropped a note about the stash");
            World v = IntelWorld(); Resident liar = v.Data.Residents[1], rival = v.Data.Residents[5]; liar.Impulsivity = 80; Pair(liar, rival, 80, 5); v.Contraband.Items.Clear();
            for (int d = 0; d < 80 && !v.IntelWork.Tips.Any(t => t.SourceId == liar.Id); d++) { v.Kites(); NextDay(v); }
            Tip invented = v.IntelWork.Tips.FirstOrDefault(t => t.SourceId == liar.Id);
            Check(invented != null && !invented.Truth && invented.SubjectId == rival.Id, "No invented note about a rival");
        });
        Test("suspicion becomes a snitch jacket: the informant is burned, a gang green-lights an attack, and protection follows", () => {
            World w = IntelWorld(); Resident snitch = w.Data.Residents[0], enforcer = w.Data.Residents[1];
            enforcer.Gang = "Cedar Street"; enforcer.Impulsivity = 80; snitch.Gang = "Cedar Street";
            w.IntelWork.Informants.Add(new Informant { ResidentId = snitch.Id });
            w.Suspect(snitch, 75, "check");
            Check(snitch.Jacket && w.InformantOf(snitch).Status == "Burned" && w.IntelWork.Burned == 1, "No jacket at 75 suspicion");
            Check(w.IntelWork.Intel.Any(i => i.Kind == "Hit" && i.SubjectId == snitch.Id && i.ActorId == enforcer.Id) && w.IntelFightFactor(enforcer, snitch) >= 2.9f, "No green light from the gang");
            snitch.Safety = 35;
            for (int i = 0; i < 20 && !snitch.ProtectiveCustody; i++) w.Harassment();
            Check(snitch.ProtectiveCustody && w.InProtectiveCustody == 1, "A threatened snitch was not protected");
            Check(w.JobBlocker(snitch, "orderly") != "" && !w.AssignJob(snitch.Id, "orderly"), "Protective custody went to work");
            w.AdvanceMinutes(120);
            Check(Geometry.Distance(snitch.Position, snitch.Home) < 1.5f, "Protective custody left the cell");
        });
        Test("an attack on a known informant dries up tips and is cited by inspectors", () => {
            World w = IntelWorld(); Resident snitch = w.Data.Residents[0], attacker = w.Data.Residents[1];
            w.Suspect(snitch, 80, "check"); float trust = w.IntelWork.Trust;
            w.AfterFight(attacker, snitch);
            Check(w.IntelWork.SnitchAssaults == 1 && w.IntelWork.DryUntil > w.Data.Minute + 9 * 1440 && w.IntelWork.Trust <= trust - 2, "Assault consequences");
            Check(w.Inspect(false).Findings.Any(f => f.Category == "Welfare" && f.Text.Contains("informant")), "Inspectors ignored the assault");
            Resident r = w.Data.Residents[2]; r.Safety = 30; r.Legal.Status = "Pretrial";
            float dry = w.RecruitChance(r, "Store credit"); w.IntelWork.DryUntil = -1;
            Check(w.RecruitChance(r, "Store credit") > dry + 0.25f, "Recruitment did not dry up");
        });
        Test("rumors, leaks and court papers label people; secure records stop paperwork checks", () => {
            World w = IntelWorld(); Resident liar = w.Data.Residents[0], victim = w.Data.Residents[1];
            liar.Impulsivity = 80; liar.Discipline = 20; liar.Gang = "Harbor Union"; Pair(liar, victim, 90, 5);
            for (int d = 0; d < 60 && victim.Suspicion == 0; d++) { w.Rumors(); NextDay(w); }
            Check(Math.Abs(victim.Suspicion - 27) < 0.01f && w.IntelWork.Rumors == 1, "A gang rumor did not add 27 suspicion: " + victim.Suspicion);
            World v = IntelWorld(); Resident inf = v.Data.Residents[2]; StaffMember crooked = v.Data.Staff.First(s => s.Role == "Officer");
            crooked.Integrity = 20; crooked.FavoredGang = "Cedar Street";
            v.IntelWork.Informants.Add(new Informant { ResidentId = inf.Id });
            for (int d = 0; d < 80 && !inf.Jacket; d++) { v.LeakNames(); NextDay(v); }
            Check(inf.Jacket && v.IntelWork.Leaks >= 1, "A corrupt officer never sold the name");
            World p = IntelWorld(); Resident arrival = p.Data.Residents[3]; arrival.CooperationHistory = 1;
            p.Data.Residents[4].Gang = p.Data.Residents[5].Gang = "Harbor Union";
            p.PaperworkCheck(arrival);
            Check(Math.Abs(arrival.Suspicion - 45) < 0.01f && p.IntelWork.Paperwork == 1, "Court papers were not checked");
            World q = IntelWorld(); Capital(q, 5000000); Check(q.BuyUpgrade("records-security"), q.Notice);
            Resident sealedArrival = q.Data.Residents[3]; sealedArrival.CooperationHistory = 1; q.Data.Residents[4].Gang = q.Data.Residents[5].Gang = "Harbor Union";
            q.PaperworkCheck(sealedArrival);
            Check(sealedArrival.Suspicion == 0, "Secure records leaked a cooperation deal");
        });
        Test("corroboration confirms a true tip seen on camera and drops a false one", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review");
            Resident holder = w.Data.Residents[0]; ContrabandItem item = w.Introduce("Drugs", "Throw-over", holder);
            Intel intel = w.IntelWork.Intel.Single(i => i.ItemId == item.Id);
            foreach (string id in w.Data.Residents.Take(4).Select(r => r.Id)) if (!intel.Knowers.Contains(id)) intel.Knowers.Add(id);
            Tip good = w.AddTip(w.Data.Residents[1].Id, "Note", "Stash", holder.Id, "", holder.Home, true, intel.Id, w.Data.Minute + 3 * 1440, "true");
            Tip bad = w.AddTip(w.Data.Residents[2].Id, "Note", "Stash", w.Data.Residents[5].Id, "", w.Data.Residents[5].Home, false, "", w.Data.Minute + 3 * 1440, "false");
            w.ActOnTip(good.Id, "Corroborate"); w.ActOnTip(bad.Id, "Corroborate");
            w.AdvanceMinutes(185);
            Check(good.Status == "New" && good.Corroborated && bad.Status == "False", "Corroboration: " + good.Status + "/" + good.Corroborated + " and " + bad.Status);
            Check(!w.TipActions(good).Contains("Corroborate") && w.TipActions(good).Contains("Search cell"), "A corroborated tip can be corroborated again");
        });
        Test("tip policies: held tips wait, protected sources corroborate first, act at once does not wait", () => {
            Tip Run(string policy, out World w)
            {
                w = IntelWorld(); w.SetIntelPolicy(policy);
                Resident holder = w.Data.Residents[0]; ContrabandItem item = w.Introduce("Drugs", "Throw-over", holder);
                Intel intel = w.IntelWork.Intel.Single(i => i.ItemId == item.Id);
                Tip t = w.AddTip(w.Data.Residents[1].Id, "Note", "Stash", holder.Id, "", holder.Home, true, intel.Id, w.Data.Minute + 3 * 1440, "p");
                w.AdvanceMinutes(40); return t;
            }
            World a, b, c;
            Check(Run("Hold for review", out a).Status == "New", "A held tip was acted on");
            Check(Run("Protect sources", out b).Status == "Corroborating", "Protect sources did not corroborate first");
            Tip now = Run("Act at once", out c);
            Check(now.Status == "Acting" || now.Status == "Confirmed" || now.Status == "False", "Act at once waited");
            Check(now.Action == "Search cell", "Act at once chose " + now.Action);
            Check(!a.SetIntelPolicy("Whatever"), "An unknown policy was accepted");
        });
        Test("investigating a staff tip finds a corrupt officer and insults an honest one", () => {
            World w = IntelWorld(); StaffMember crooked = w.Data.Staff.First(s => s.Role == "Officer"), honest = w.Data.Staff.Last(s => s.Role == "Officer");
            crooked.Integrity = 30; honest.Integrity = 85;
            Tip t1 = w.AddTip(w.Data.Residents[0].Id, "Note", "Smuggler", crooked.Id, "", crooked.Position, true, "", w.Data.Minute + 3 * 1440, "a");
            Tip t2 = w.AddTip(w.Data.Residents[1].Id, "Note", "Smuggler", honest.Id, "", honest.Position, true, "", w.Data.Minute + 3 * 1440, "b");
            float pressure = honest.Pressure;
            w.ActOnTip(t1.Id, "Investigate"); w.ActOnTip(t2.Id, "Investigate");
            Check(t1.Status == "Confirmed" && w.Personnel.WriteUps.Any(x => x.SubjectId == crooked.Id && x.Category == "Contraband smuggling"), "The corrupt officer was not caught");
            Check(t2.Status == "False" && honest.Pressure >= pressure + 9.9f, "The honest officer was not affected");
        });
        Test("exposure is 12 divided by the square root of how many knew, scaled by the action", () => {
            World w = IntelWorld(); w.SetIntelPolicy("Hold for review");
            Resident actor = w.Data.Residents[0], target = w.Data.Residents[1];
            var circle = w.Data.Residents.Skip(2).Take(4).ToList();
            Intel hit = w.NoteHit(actor, target, 3);
            hit.Knowers.Clear(); hit.Knowers.Add(actor.Id); hit.Knowers.AddRange(circle.Select(r => r.Id));
            foreach (Resident r in circle) r.Suspicion = 0;
            Tip tip = w.AddTip("", "Note", "Hit", target.Id, actor.Id, target.Home, true, hit.Id, w.Data.Minute + 1440, "check");
            Check(w.ActOnTip(tip.Id, "Separate them") && tip.Status == "Confirmed", w.Notice);
            Check(circle.All(r => Math.Abs(r.Suspicion - 12f / 2f * 0.6f) < 0.001f), "Exposure per knower: " + string.Join(", ", circle.Select(r => r.Suspicion)));
        });
        Test("protective custody keeps a person out of fights even next to a nemesis", () => {
            int Fights(bool protect)
            {
                World w = IntelWorld(); Resident a = w.Data.Residents[0], b = Cellmate(w, a);
                foreach (Resident r in new[] { a, b }) { r.Impulsivity = 100; r.Stress = 100; }
                Pair(a, b, 95, 0);
                foreach (StaffMember s in w.Data.Staff) { s.Position = s.Station = s.Destination = new Point(24, 20); s.NextDutyMinute = int.MaxValue / 2; }
                if (protect) Check(w.SetProtectiveCustody(b.Id, true, "check"), w.Notice);
                int before = w.Personnel.Fights + w.Personnel.FightsPrevented;
                for (int m = 0; m < 600; m++) { b.Position = a.Position + new Point(0.5f, 0); w.CheckFights(); w.AdvanceIncidentScenes(); w.Data.Minute++; }
                return w.Personnel.Fights + w.Personnel.FightsPrevented - before;
            }
            Check(Fights(false) > 0, "Setup: nemeses side by side never fought");
            Check(Fights(true) == 0, "A resident in protective custody was drawn into a fight");
        });
        Test("trust in staff moves unrest both ways", () => {
            World w = IntelWorld(); var factors = new List<RiskFactor>();
            w.IntelWork.Trust = 30; w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Distrust of staff" && f.Points >= 4), "Distrust added no unrest");
            factors.Clear(); w.IntelWork.Trust = 90; w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Trust in staff" && f.Points < 0), "Trust did not calm anyone");
        });
        Test("intelligence work is deterministic, survives a reload mid-tip and rejects malformed saves", () => {
            World Make() { World w = IntelWorld(); w.SetIntelPolicy("Act at once"); w.Introduce("Drugs", "Throw-over", w.Data.Residents[0]); w.StageIntelDemo(); return w; }
            World a = Make(), b = Make();
            a.AdvanceMinutes(90); b.AdvanceMinutes(90);
            World resumed = new World(RoundTrip(a.Data));
            for (int d = 0; d < 3; d++) { b.AdvanceMinutes(1440); resumed.AdvanceMinutes(1440); }
            Check(JsonSerializer.Serialize(b.Data.IntelWork, Json) == JsonSerializer.Serialize(resumed.Data.IntelWork, Json) && b.Data.Residents.Select(r => r.Suspicion).SequenceEqual(resumed.Data.Residents.Select(r => r.Suspicion)), "A reload changed intelligence work");
            void Rejects(Action<State> corrupt, string what)
            {
                State s = RoundTrip(b.Data); corrupt(s); bool rejected = false;
                try { new World(s); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Accepted " + what);
            }
            Rejects(s => s.IntelWork.Policy = "Shoot first", "an unknown policy");
            Rejects(s => s.IntelWork.Trust = 140, "trust out of range");
            Rejects(s => s.Residents[0].Suspicion = -3, "negative suspicion");
            Rejects(s => s.IntelWork.Tips.Add(new Tip { Id = "x", Kind = "Gossip" }), "an unknown tip kind");
            Rejects(s => s.IntelWork.Informants.Add(new Informant { ResidentId = s.Residents[0].Id, Reward = "A pony" }), "an unknown reward");
        });
    }
}
