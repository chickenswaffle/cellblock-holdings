using System.Linq;
using Cellblock.Core;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        Label securityInfo, powerInfo;
        Button upgradeEquipment, assignPost, clearPost, gridSwitch;
        string securityOfficerId="";
        Button chooseOfficer;
        VisualElement searchlightPane;
        Label searchlightInfo;
        Button sweepLight, holdLight, stopLight;
        void BuildSecurity()
        {
            securityInfo=Text("","body");securityInfo.AddToClassList("equipment-status");fixturePane.Add(securityInfo);
            upgradeEquipment=Action("Upgrade",()=>game.UpgradeSelected(),"primary");upgradeEquipment.style.height=64;upgradeEquipment.style.whiteSpace=WhiteSpace.Normal;fixturePane.Add(upgradeEquipment);
            chooseOfficer=Action("Choose officer",()=>{
                var officers=game.World.Data.Staff.Where(s=>s.Role=="Officer").ToList();
                if(officers.Count==0){game.Message("Hire an officer in Control first.");return;}
                int index=officers.FindIndex(s=>s.Id==securityOfficerId);securityOfficerId=officers[(index+1)%officers.Count].Id;Refresh(true);
            });fixturePane.Add(chooseOfficer);
            assignPost=Action("Assign",()=>{
                Fixture f=game.SelectedFixture;if(f==null)return;
                string role=f.Kind=="GuardTower"?"Tower":f.Kind=="Kennel"?"K9":"Armed";
                game.World.AssignSecurityOfficer(f.Id,securityOfficerId,role);Refresh(true);
            });fixturePane.Add(assignPost);
            clearPost=Action("Release post assignment",()=>{
                Fixture f=game.SelectedFixture;if(f==null)return;
                foreach(var staff in game.World.Data.Staff.Where(s=>s.SecurityPostId==f.Id).ToArray())game.World.ClearSecurityAssignment(staff.Id);
                Refresh(true);
            },"quiet");fixturePane.Add(clearPost);
            BuildSearchlightControls();
            controlPane.Add(Text("POWER / COMFORT / SECURITY","section-label"));
            powerInfo=Text("","body");controlPane.Add(powerInfo);
            gridSwitch=Action("Grid",()=>{game.World.Data.Security.GridEnabled=!game.World.Data.Security.GridEnabled;Refresh(true);});controlPane.Add(gridSwitch);
            controlPane.Add(Text("New units need a distribution panel for room power. Cooling raises local comfort over time. K9 and armed duty add wages; each new qualification costs $750 once. Staffed towers and nearby dog teams detect through real sightlines. Select equipment in the world for upgrades and assignments.","muted"));
        }
        void RefreshSecurity(World world)
        {
            if(securityInfo==null)return;
            powerInfo.text=(world.PowerOnline?"Grid supplying":"Power unavailable")+" · "+world.PowerDemandKw.ToString("0.00")+" / "+world.PowerCapacityKw.ToString("0.0")+" kW\n"+
                "Added equipment: $0.18/kWh · "+world.Data.Staff.Count(world.IsOnShift)+" on shift\nStaff morale "+(world.Data.Staff.Count==0?"—":world.Data.Staff.Average(s=>s.Morale).ToString("0"))+" / 100 · "+world.Data.Security.Dogs.Count+" dogs\n"+(world.DispatchOnline?"Staffed dispatch console online":"Dispatch console unstaffed or offline")+"\nEffective perimeter reinforcement "+(world.PerimeterIntegrity*100).ToString("0.0")+"%\n"+world.Data.Fixtures.Count(world.SearchlightOnline)+" active searchlights · "+world.SearchlightSleepers+" sleeping residents exposed";
            gridSwitch.text=world.Data.Security.GridEnabled?"Isolate electricity":"Restore electricity";
            Fixture f=game.SelectedFixture;
            RefreshSearchlightControls(world,f);
            if(f==null)return;
            securityInfo.text=SecurityRules.LevelName(f.UpgradeLevel)+" · condition "+f.Condition.ToString("0")+"%\n"+SecurityRules.Features(f.Kind,f.UpgradeLevel)+"\n"+world.EquipmentStatus(f)+"\nDemand "+SecurityRules.Demand(f).ToString("0.00")+" kW · local comfort "+world.ComfortAt(f.Position).ToString("0")+"/100";
            if(f.UpgradeLevel<2){Fixture next=f.Copy();next.UpgradeLevel++;upgradeEquipment.text="Upgrade: "+SecurityRules.LevelName(next.UpgradeLevel)+"\n"+Money(SecurityRules.Price(next)-SecurityRules.Price(f))+" materials + work";}
            else upgradeEquipment.text="Fully upgraded";
            upgradeEquipment.SetEnabled(f.UpgradeLevel<2&&game.Draft==null);
            bool post=f.Kind=="GuardTower"||f.Kind=="Kennel"||f.Kind=="Armory";
            chooseOfficer.style.display=assignPost.style.display=clearPost.style.display=post?DisplayStyle.Flex:DisplayStyle.None;
            StaffMember officer=world.Data.Staff.FirstOrDefault(s=>s.Id==securityOfficerId&&s.Role=="Officer");
            if(officer==null){officer=world.Data.Staff.FirstOrDefault(s=>s.Role=="Officer");securityOfficerId=officer==null?"":officer.Id;}
            if(post&&officer!=null)
            {
                bool trained=f.Kind=="Kennel"?officer.DogTrained:f.Kind=="Armory"?officer.ArmedTrained:true;
                securityInfo.text+="\n"+(trained?"Qualification ready":"Qualification: $750 once before assignment")+(f.Kind=="Kennel"?" · duty +$80/day":f.Kind=="Armory"?" · duty +$100/day":" · standard officer wage");
            }
            chooseOfficer.text=officer==null?"Hire an officer in Control":"Officer: "+officer.Name+" · tap to cycle";
            var assigned=world.Data.Staff.FirstOrDefault(s=>s.SecurityPostId==f.Id);
            assignPost.text=assigned!=null?"Assigned: "+assigned.Name:f.Kind=="Kennel"?"Assign K9 handler · +$80/day":f.Kind=="Armory"?"Assign armed duty · +$100/day":"Assign tower lookout";
            assignPost.SetEnabled(officer!=null&&assigned==null&&(f.Kind!="Armory"||f.UpgradeLevel>0));clearPost.SetEnabled(assigned!=null);
        }
        void BuildSearchlightControls()
        {
            searchlightPane=new VisualElement{name="searchlight-controls"};fixturePane.Add(searchlightPane);
            searchlightPane.Add(Text("SEARCHLIGHT / NIGHT WATCH","section-label"));
            searchlightInfo=Text("","body");searchlightPane.Add(searchlightInfo);
            var modes=new VisualElement{name="searchlight-mode-buttons"};modes.AddToClassList("searchlight-mode-buttons");modes.style.flexDirection=FlexDirection.Row;searchlightPane.Add(modes);
            sweepLight=Action("Sweep",()=>SetLightMode("Sweep"));holdLight=Action("Hold",()=>SetLightMode("Hold"));stopLight=Action("Off",()=>SetLightMode("Off"));
            foreach(Button b in new[]{sweepLight,holdLight,stopLight}){b.style.flexGrow=1;b.style.minWidth=0;modes.Add(b);}
            var aim=new VisualElement();aim.style.flexDirection=FlexDirection.Row;searchlightPane.Add(aim);
            foreach(int delta in new[]{-15,15})
            {int turn=delta;Button b=Action(delta<0?"Aim −15°":"Aim +15°",()=>{Fixture f=game.SelectedFixture;if(f==null)return;var c=game.World.TowerLight(f);game.World.SetSearchlight(f.Id,c==null?"Hold":c.Mode,(c==null?f.Rotation:c.Bearing)+turn);Refresh(true);});b.style.flexGrow=1;b.style.minWidth=0;aim.Add(b);}
            searchlightPane.Add(Text("0° = north. Sweep moves ±55° around your aim. Hold freezes the beam. Walls block light. Aim away from bedrooms to protect sleep.","muted"));
        }
        void SetLightMode(string mode)
        {
            Fixture f=game.SelectedFixture;if(f==null||f.Kind!="GuardTower")return;
            var c=game.World.TowerLight(f);float bearing=mode=="Hold"?game.World.SearchlightBearing(f):c==null?f.Rotation:c.Bearing;
            game.World.SetSearchlight(f.Id,mode,bearing);Refresh(true);
        }
        void RefreshSearchlightControls(World world,Fixture f)
        {
            bool tower=f!=null&&f.Kind=="GuardTower";searchlightPane.style.display=tower?DisplayStyle.Flex:DisplayStyle.None;if(!tower)return;
            var c=world.TowerLight(f);string mode=c==null?"Sweep":c.Mode;
            searchlightInfo.text=world.SearchlightStatus(f)+"\n"+mode+" · aim "+(c==null?f.Rotation:c.Bearing).ToString("0")+"° · beam "+world.SearchlightBearing(f).ToString("0")+"°\n"+world.SearchlightRange(f).ToString("0")+" m reach · "+(world.SearchlightHalfAngle(f)*2).ToString("0")+"° cone\n"+(c==null?0:c.DisturbedMinutes)+" resident-minutes of disturbed sleep";
            foreach(Button b in new[]{sweepLight,holdLight,stopLight})b.EnableInClassList("primary",b==(mode=="Sweep"?sweepLight:mode=="Hold"?holdLight:stopLight));
        }
#if UNITY_EDITOR
        public bool PreviewSearchlightControlsFit()
        {
            var viewport=root.worldBound;
            foreach(Button button in searchlightPane.Query<Button>().ToList())
            {var b=button.worldBound;if(b.width<44||b.height<43.5f||b.xMin<viewport.xMin||b.xMax>viewport.xMax)return false;}
            return true;
        }
#endif
    }
}
