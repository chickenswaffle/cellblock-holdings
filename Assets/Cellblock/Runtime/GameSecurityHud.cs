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
            controlPane.Add(Text("POWER / COMFORT / SECURITY","section-label"));
            powerInfo=Text("","body");controlPane.Add(powerInfo);
            gridSwitch=Action("Grid",()=>{game.World.Data.Security.GridEnabled=!game.World.Data.Security.GridEnabled;Refresh(true);});controlPane.Add(gridSwitch);
            controlPane.Add(Text("New units need a distribution panel for room power. Cooling raises local comfort over time. K9 and armed duty add wages; each new qualification costs $750 once. Staffed towers and nearby dog teams detect through real sightlines. Select equipment in the world for upgrades and assignments.","muted"));
        }
        void RefreshSecurity(World world)
        {
            if(securityInfo==null)return;
            powerInfo.text=(world.PowerOnline?"Grid supplying":"Power unavailable")+" · "+world.PowerDemandKw.ToString("0.00")+" / "+world.PowerCapacityKw.ToString("0.0")+" kW\n"+
                "Added equipment: $0.18/kWh · "+world.Data.Staff.Count(world.IsOnShift)+" on shift\nStaff morale "+(world.Data.Staff.Count==0?"—":world.Data.Staff.Average(s=>s.Morale).ToString("0"))+" / 100 · "+world.Data.Security.Dogs.Count+" dogs\n"+(world.DispatchOnline?"Staffed dispatch console online":"Dispatch console unstaffed or offline")+"\nEffective perimeter reinforcement "+(world.PerimeterIntegrity*100).ToString("0.0")+"%";
            gridSwitch.text=world.Data.Security.GridEnabled?"Isolate electricity":"Restore electricity";
            Fixture f=game.SelectedFixture;
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
    }
}
