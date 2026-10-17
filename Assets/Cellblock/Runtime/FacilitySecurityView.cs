using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        sealed class DogView { public Transform Root, Tail; public Transform[] Legs; public Vector3 Previous; }
        readonly Dictionary<string, DogView> dogs = new Dictionary<string, DogView>();
        readonly Dictionary<string, Light> floodlights = new Dictionary<string, Light>();
        Transform securityRoot;
        World securityWorld;
        void SecurityFixture(Transform root, Fixture f)
        {
            int level = f.UpgradeLevel;
            if (f.Kind == "Floodlight")
            {
                Box(root,"Concrete plinth",new Vector3(0,.10f,0),new Vector3(.38f,.2f,.38f),"concrete",Concrete);
                Cylinder(root,"Galvanized mast",new Vector3(0,1.6f,0),new Vector3(.09f,1.5f,.09f),"steel",Steel);
                for(int side=-1;side<=1;side+=2)
                {
                    Transform lamp=Box(root,"Directional flood housing",new Vector3(side*.18f,3.05f,.04f),new Vector3(.30f,.22f,.17f),"rubber",Steel);
                    lamp.localRotation=Quaternion.Euler(25,side*15,0);
                    Box(lamp,"LED lens",new Vector3(0,0,.095f),new Vector3(.26f,.17f,.015f),"lamp",new Color(.94f,.88f,.71f));
                }
            }
            if(f.Kind=="PerimeterWire")
            {
                for(int side=-1;side<=1;side+=2)Cylinder(root,"Barrier post",new Vector3(side*1.44f,1,0),new Vector3(.075f,1,.075f),"steel",Steel);
                for(int i=0;i<12;i++)
                {
                    Transform a=Box(root,"Woven barrier",new Vector3(-1.4f+i*.25f,1,0),new Vector3(.012f,2.05f,.012f),"wire",new Color(.49f,.53f,.53f));
                    a.localRotation=Quaternion.Euler(0,0,15);
                }
                for(int i=0;i<9;i++)Box(root,"Horizontal wire",new Vector3(0,.15f+i*.22f,0),new Vector3(2.9f,.014f,.014f),"wire",Steel);
                if(level>0)for(int i=0;i<15;i++)
                {
                    Transform barb=Box(root,"Reinforced wire top",new Vector3(-1.4f+i*.2f,2.08f,0),new Vector3(.014f,.24f,.014f),"steel",Steel); barb.localRotation=Quaternion.Euler(0,0,i%2==0?50:-50);
                }
                if(level==2)Box(root,"Gate sensor relay",new Vector3(1.4f,.9f,.10f),new Vector3(.16f,.25f,.12f),"control-green",Teal);
            }
            if(f.Kind=="Kennel")
            {
                Box(root,"Raised dry deck",new Vector3(0,.10f,0),new Vector3(2.3f,.2f,1.7f),"wood",new Color(.49f,.40f,.29f));
                Box(root,"Shelter rear",new Vector3(0,.75f,-.7f),new Vector3(2.2f,1.3f,.12f),"plaster",Concrete);
                foreach(float x in new[]{-1.05f,1.05f})Box(root,"Shelter side",new Vector3(x,.75f,0),new Vector3(.10f,1.3f,1.5f),"wood",new Color(.49f,.40f,.29f));
                Box(root,"Insulated roof",new Vector3(0,1.45f,0),new Vector3(2.4f,.16f,1.8f),"steel",Steel);
                Cylinder(root,"Water bowl",new Vector3(.6f,.24f,.55f),new Vector3(.32f,.06f,.32f),"steel",Steel);
                Box(root,"Rest mat",new Vector3(-.3f,.23f,-.2f),new Vector3(1.1f,.06f,.7f),"linen",new Color(.31f,.36f,.30f));
            }
            if(f.Kind=="GuardTower")
            {
                foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-1.1f,1.1f})
                { Box(root,"Foundation",new Vector3(x,.1f,z),new Vector3(.42f,.2f,.42f),"concrete",Concrete); Box(root,"Tower support",new Vector3(x,2,z),new Vector3(.18f,4,.18f),"steel",Steel); }
                for(int side=-1;side<=1;side+=2)
                {
                    Transform brace=Box(root,"Diagonal bracing",new Vector3(side*1.1f,1.9f,0),new Vector3(.10f,4.2f,.10f),"steel",Steel);brace.localRotation=Quaternion.Euler(30,0,0);
                    foreach(float y in new[]{4.18f,4.55f,4.95f})Box(root,"Tubular safety rail",new Vector3(side*1.25f,y,0),new Vector3(.055f,.055f,2.6f),"steel",Steel);
                    foreach(float z in new[]{-1.25f,0,1.25f})Box(root,"Rail stanchion",new Vector3(side*1.25f,4.55f,z),new Vector3(.06f,.85f,.06f),"steel",Steel);
                }
                Box(root,"Lookout deck",new Vector3(0,4,0),new Vector3(2.8f,.20f,2.8f),"wood",new Color(.44f,.39f,.30f));
                foreach(float x in new[]{-1.25f,1.25f})foreach(float z in new[]{-1.25f,1.25f})Box(root,"Canopy upright",new Vector3(x,5.1f,z),new Vector3(.065f,2.1f,.065f),"steel",Steel);
                if(FullHeight)Box(root,"Weather canopy",new Vector3(0,6.15f,0),new Vector3(2.8f,.12f,2.8f),"steel",Steel);
                foreach(float x in new[]{-.4f,.4f})Box(root,"Access ladder rail",new Vector3(x,2,-1.35f),new Vector3(.055f,4,.055f),"steel",Steel);
                for(int i=0;i<15;i++)Box(root,"Ladder rung",new Vector3(0,.3f+i*.25f,-1.35f),new Vector3(.8f,.045f,.045f),"steel",Steel);
                foreach(float y in new[]{4.18f,4.55f,4.95f})Box(root,"Rear safety rail",new Vector3(0,y,1.25f),new Vector3(2.5f,.055f,.055f),"steel",Steel);
                foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-1.1f,1.1f})
                    foreach(float bolt in new[]{-.13f,.13f})Cylinder(root,"Anchor bolt",new Vector3(x+bolt,.23f,z),new Vector3(.04f,.045f,.04f),"steel",Steel);
                Box(root,"Lookout storage chest",new Vector3(-.8f,4.35f,.5f),new Vector3(.5f,.5f,.6f),"steel",new Color(.27f,.31f,.28f));
                if(level>0)
                {
                    Box(root,"Searchlight crossbar",new Vector3(0,5.0f,0),new Vector3(2.4f,.10f,.14f),"steel",Steel);
                    Box(root,"Searchlight power cable",new Vector3(1.14f,2.5f,.08f),new Vector3(.028f,4.9f,.028f),"rubber",Color.black);
                    Box(root,"Lookout radio",new Vector3(.75f,4.2f,-.6f),new Vector3(.2f,.22f,.13f),"rubber",new Color(.12f,.15f,.15f));
                    Cylinder(root,"Radio antenna",new Vector3(.81f,4.5f,-.6f),new Vector3(.014f,.22f,.014f),"steel",Steel);
                }
                if(level==2){Box(root,"Weatherproof console",new Vector3(-.65f,4.45f,-.55f),new Vector3(.65f,.70f,.45f),"control-green",Teal);Box(root,"Cooling unit",new Vector3(1,4.7f,-.8f),new Vector3(.45f,.35f,.35f),"plaster",Concrete);}
            }
            if(f.Kind=="Armory")
            {
                Box(root,"Locked equipment cabinet",new Vector3(0,.93f,0),new Vector3(1.05f,1.85f,.65f),"steel",new Color(.27f,.33f,.32f));
                foreach(float x in new[]{-.25f,.25f}){Box(root,"Reinforced door",new Vector3(x,.95f,.34f),new Vector3(.48f,1.75f,.035f),"steel",Steel);Box(root,"Lock",new Vector3(x,.95f,.38f),new Vector3(.04f,.17f,.04f),"rubber",Color.black);}
            }
            if(f.Kind=="ElectricalPanel")
            {
                Box(root,"Service distribution cabinet",new Vector3(0,.77f,0),new Vector3(.5f,1.45f,.30f),"steel",Steel);
                Box(root,"Insulated service cable",new Vector3(.19f,.25f,-.14f),new Vector3(.04f,.5f,.04f),"rubber",Color.black);
                for(int i=0;i<4+level*2;i++)Box(root,"Circuit breaker",new Vector3((i%2-.5f)*.13f,.6f+i/2*.18f,.17f),new Vector3(.08f,.09f,.04f),"rubber",Color.black);
            }
            if(f.Kind=="ClimateUnit")
            {
                Box(root,level==0?"Ventilation unit":"Air conditioner",new Vector3(0,1.15f,0),new Vector3(.82f,.55f,.42f),"plaster",Concrete);
                for(int i=0;i<6;i++)Box(root,"Air grille",new Vector3(0,.97f+i*.06f,.22f),new Vector3(.65f,.012f,.015f),"steel",Steel);
                if(level>0)Box(root,"Temperature display",new Vector3(.28f,1.32f,.22f),new Vector3(.10f,.05f,.015f),"handle",Teal);
            }
            if(f.Kind=="ControlComputer")
            {
                Box(root,"Console desk",new Vector3(0,.78f,0),new Vector3(1.05f,.08f,.65f),"wood",new Color(.43f,.37f,.29f));
                foreach(float x in new[]{-.42f,.42f})Box(root,"Desk support",new Vector3(x,.38f,0),new Vector3(.07f,.75f,.5f),"steel",Steel);
                Box(root,"Monitor stand",new Vector3(0,.96f,-.1f),new Vector3(.08f,.30f,.08f),"steel",Steel);
                Box(root,"Monitor",new Vector3(0,1.15f,-.1f),new Vector3(.64f,.38f,.06f),"rubber",Steel);
                Box(root,"Dispatch screen",new Vector3(0,1.15f,-.06f),new Vector3(.57f,.30f,.01f),"control-green",new Color(.12f,.31f,.29f));
                Box(root,"Keyboard",new Vector3(0,.84f,.17f),new Vector3(.55f,.025f,.19f),"rubber",Steel);
            }
            if(level>0)
            {
                Box(root,"Installed upgrade identification",new Vector3(0,.35f,.25f),new Vector3(.16f,.10f,.025f),level==1?"upgrade-brass":"handle",level==1?Ochre:Teal);
                if(f.Kind=="CCTV"){Box(root,"Network enclosure",new Vector3(0,1.0f,-.12f),new Vector3(.22f,.30f,.12f),"steel",Steel);if(level==2)Sphere(root,"IR array",new Vector3(0,2.48f,.405f),new Vector3(.18f,.16f,.018f),"alarm",new Color(.4f,.13f,.11f));}
                if(f.Kind=="Bed")Box(root,"Comfort topper",new Vector3(0,.61f,0),new Vector3(.83f,.10f,1.8f),"linen",new Color(.62f,.68f,.61f));
            }
        }
        void RefreshSecurity(World world,bool force)
        {
            if(securityRoot==null)securityRoot=Root("Security teams and live lights");
            if(securityWorld!=world)
            {
                Clear(securityRoot);dogs.Clear();floodlights.Clear();searchlights.Clear();securityWorld=world;
            }
            foreach(string id in floodlights.Keys.Where(id=>!world.Data.Fixtures.Any(f=>f.Id==id)).ToArray()){Destroy(floodlights[id].gameObject);floodlights.Remove(id);}
            foreach(Fixture f in world.Data.Fixtures.Where(f=>f.Kind=="Floodlight"))
            {
                Light lamp;if(!floodlights.TryGetValue(f.Id,out lamp))
                {lamp=new GameObject("Live security floodlight").AddComponent<Light>();lamp.transform.SetParent(securityRoot,false);lamp.type=LightType.Point;lamp.color=new Color(1,.88f,.67f);lamp.shadows=LightShadows.None;floodlights.Add(f.Id,lamp);}
                lamp.transform.position=V(f.Position,f.Kind=="GuardTower"?4.8f:3.15f);lamp.range=world.LightRange(f);lamp.intensity=f.UpgradeLevel==0?8:f.UpgradeLevel==1?20:24;lamp.enabled=world.Night&&world.Data.Operations.Lights&&world.EquipmentOnline(f)&&(f.Kind!="GuardTower"||world.StaffAtPost(f,"Tower"));
            }
            RefreshSearchlights(world,force);
            foreach(string id in dogs.Keys.Where(id=>!world.Data.Security.Dogs.Any(d=>d.Id==id)).ToArray()){Destroy(dogs[id].Root.gameObject);dogs.Remove(id);}
            foreach(SecurityDog dog in world.Data.Security.Dogs)
            {
                DogView visual;if(!dogs.TryGetValue(dog.Id,out visual))
                {
                    var r=new GameObject("K9 · "+dog.Id).transform;r.SetParent(securityRoot,false);
                    Capsule(r,"Dog torso",new Vector3(0,.63f,0),new Vector3(.34f,.42f,.31f),"dog-tan",new Color(.42f,.30f,.18f)).localRotation=Quaternion.Euler(90,0,0);
                    Sphere(r,"Dog head",new Vector3(0,.85f,.40f),new Vector3(.28f,.30f,.32f),"dog-tan",new Color(.42f,.30f,.18f));
                    Box(r,"Muzzle",new Vector3(0,.78f,.58f),new Vector3(.18f,.15f,.23f),"rubber",new Color(.12f,.11f,.10f));
                    foreach(float x in new[]{-.09f,.09f})Box(r,"Pointed ear",new Vector3(x,1.05f,.37f),new Vector3(.08f,.24f,.10f),"rubber",new Color(.18f,.16f,.13f));
                    Box(r,"K9 harness",new Vector3(0,.69f,.06f),new Vector3(.37f,.32f,.28f),"uniform-staff-Officer",new Color(.19f,.27f,.33f));Combine(r);
                    var legs=new Transform[4];for(int i=0;i<4;i++){legs[i]=new GameObject("Dog leg").transform;legs[i].SetParent(r,false);legs[i].localPosition=new Vector3(i%2==0?-.12f:.12f,.58f,i<2?.26f:-.28f);Capsule(legs[i],"Leg",new Vector3(0,-.24f,0),new Vector3(.08f,.24f,.09f),"dog-tan",new Color(.42f,.30f,.18f));Combine(legs[i]);}
                    Transform tail=Capsule(r,"Tail",new Vector3(0,.65f,-.59f),new Vector3(.09f,.26f,.09f),"dog-tan",new Color(.42f,.30f,.18f));tail.localRotation=Quaternion.Euler(55,0,0);
                    visual=new DogView{Root=r,Legs=legs,Tail=tail,Previous=V(dog.Position,.14f)};dogs.Add(dog.Id,visual);r.position=visual.Previous;
                }
                Vector3 target=V(dog.Position,.14f),before=visual.Root.position;
                visual.Root.position=force?target:Vector3.MoveTowards(before,target,Time.unscaledDeltaTime*Mathf.Max(1,world.Data.Speed)*2.5f);
                Vector3 delta=visual.Root.position-before;bool walking=delta.sqrMagnitude>.00001f;
                if(walking)visual.Root.rotation=Quaternion.Slerp(visual.Root.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-Time.unscaledDeltaTime*10));
                float phase=Time.unscaledTime*Mathf.Max(1,world.Data.Speed)*7;
                for(int i=0;i<4;i++)visual.Legs[i].localRotation=Quaternion.Euler(walking?Mathf.Sin(phase+(i==0||i==3?0:Mathf.PI))*28:0,0,0);
                if(world.Data.Speed>0)visual.Tail.localRotation=Quaternion.Euler(55,Mathf.Sin(phase*.4f)*12,0);
            }
        }
        void AnimateSecurityOfficer(Actor actor,StaffMember staff,World world,bool snap)
        {
            Fixture tower=staff.SecurityRole=="Tower"?world.Data.Fixtures.FirstOrDefault(f=>f.Id==staff.SecurityPostId):null;
            float climb=tower!=null&&staff.PostArrivedMinute>=0?Mathf.Clamp01((world.Data.Minute-staff.PostArrivedMinute)/5f):0;
            Vector3 offset=Vector3.zero;
            if(tower!=null&&climb>0)
            {
                Point ladder=tower.Position+(Geometry.AccessPoint(tower)-tower.Position)*.86f;
                // Climb vertically at the real access side, then step onto the deck.
                Vector3 stand=Quaternion.Euler(0,tower.Rotation,0)*new Vector3(.65f,0,.55f);
                Vector3 platform=climb<1?V(ladder,.14f+4.1f*climb):V(tower.Position,4.24f)+stand;offset=actor.Root.InverseTransformPoint(platform);
            }
            actor.Model.localPosition=Vector3.Lerp(actor.Model.localPosition,offset,snap?1:1-Mathf.Exp(-Time.unscaledDeltaTime*6));
            if(climb>0&&climb<1)
            {
                float phase=Time.unscaledTime*Mathf.Max(1,world.Data.Speed)*3;
                ArmPose(actor,0,new Vector3(-.23f,1.67f+Mathf.Sin(phase)*.16f,.27f),1);ArmPose(actor,1,new Vector3(.23f,1.67f-Mathf.Sin(phase)*.16f,.27f),1);
                for(int i=0;i<2;i++)actor.Knees[i].localRotation=Quaternion.Euler(-25-Mathf.Sin(phase+i*Mathf.PI)*20,0,0);
            }
            if(actor.SecurityProp==null&&staff.SecurityRole=="Armed")
            {
                actor.SecurityProp=new GameObject("Qualified armed duty / slung equipment").transform;actor.SecurityProp.SetParent(actor.Model,false);
                Box(actor.SecurityProp,"Equipment stock",new Vector3(.24f,1.03f,-.14f),new Vector3(.10f,.30f,.09f),"rubber",Color.black);
                Box(actor.SecurityProp,"Secured barrel",new Vector3(.24f,1.35f,-.14f),new Vector3(.04f,.40f,.04f),"steel",Steel);Combine(actor.SecurityProp);
            }
            if(actor.SecurityProp!=null)actor.SecurityProp.gameObject.SetActive(staff.SecurityRole=="Armed"&&world.IsOnShift(staff));
            AnimateTowerWatch(actor,staff,tower,world,climb,snap);
        }
    }
}
