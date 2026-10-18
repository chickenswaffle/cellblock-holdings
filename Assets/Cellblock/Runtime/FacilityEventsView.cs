using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        readonly HashSet<string> eventActorIds=new HashSet<string>();
        readonly List<string> retiredEventActors=new List<string>();
#if UNITY_EDITOR
        public int PreviewVisitorActorCount {get{return eventActorIds.Count;}}
#endif
        World eventViewWorld;
        readonly Dictionary<string,Transform> visitorProps=new Dictionary<string,Transform>();
        void RefreshFacilityEventsView(World world,bool force)
        {
            if(!ReferenceEquals(eventViewWorld,world)){visitorProps.Clear();eventActorIds.Clear();eventViewWorld=world;}
            foreach(FacilityIncident incident in world.Stories.Incidents.Where(e=>e.Phase!="Resolved"&&e.StartMinute<=world.Data.Minute))
            {
                if(incident.Kind=="Inspection")
                {
                    Actor inspector;bool created=!actors.TryGetValue(incident.Id,out inspector);if(created){inspector=Person(incident.Id,true,"Inspector");eventActorIds.Add(incident.Id);}
                    inspector.Seen=true;inspector.Root.gameObject.SetActive(true);inspector.Motion.Position=incident.Visitor;inspector.Motion.WalkSpeed=.95f;
                    inspector.PoseArms=incident.Phase=="Reviewing";MoveActor(inspector,inspector.Motion,world,force||created);
                    if(inspector.PoseArms){inspector.Limbs[0].localRotation=Quaternion.Euler(-35,0,-8);inspector.Forearms[0].localRotation=Quaternion.Euler(-65,0,0);inspector.Limbs[1].localRotation=Quaternion.Euler(-25,0,8);inspector.Forearms[1].localRotation=Quaternion.Euler(-70,0,0);}
                    if(!visitorProps.ContainsKey(incident.Id))
                    {
                        Transform board=Box(inspector.Hands[0],"Inspection clipboard",new Vector3(0,0,.08f),new Vector3(.25f,.025f,.34f),"clipboard",new Color(.31f,.23f,.16f));
                        Box(board,"Inspection notes",new Vector3(0,.014f,0),new Vector3(.21f,.005f,.29f),"paper",new Color(.91f,.89f,.80f));visitorProps[incident.Id]=board;
                    }
                    UpdateCharacterNameTag(inspector,incident.VisitorName,true);
                }
                if(incident.Kind=="Protest")for(int i=0;i<8;i++)RefreshCampaigner(world,incident,i,force);
            }
            RefreshPressureAuditor(world,force);
            retiredEventActors.Clear();
            foreach(string id in eventActorIds)
            {
                Actor old;if(!actors.TryGetValue(id,out old)||old.Seen)continue;
                ForgetActorMotion(old);Clear(old.Root);Destroy(old.Root.gameObject);actors.Remove(id);visitorProps.Remove(id);retiredEventActors.Add(id);
            }
            foreach(string id in retiredEventActors)eventActorIds.Remove(id);
            foreach(CapitalCase capital in world.Stories.CapitalCases)
            {
                string key="capital/"+capital.ResidentId;CourtVanView van;
                if(capital.Transport.Phase=="Idle"){if(courtVans.TryGetValue(key,out van))van.Root.gameObject.SetActive(false);continue;}
                bool created=!courtVans.TryGetValue(key,out van);if(created){van=BuildCourtVan();van.Root.name="Sentencing authority transport";courtVans[key]=van;}
                van.Root.gameObject.SetActive(true);Vector3 target=V(capital.Transport.Position,.10f),before=van.Root.position;
                van.Root.position=force||created?target:Vector3.Lerp(before,target,1-Mathf.Exp(-Time.unscaledDeltaTime*16));
                Vector3 delta=van.Root.position-before;if(delta.sqrMagnitude>.001f)van.Root.rotation=Quaternion.Slerp(van.Root.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-Time.unscaledDeltaTime*10));
                foreach(Transform wheel in van.Wheels)wheel.Rotate(0,delta.magnitude*180,0,Space.Self);
            }
        }
        void RefreshPressureAuditor(World world,bool force)
        {
            if(world.Population.Audit.Phase!="In progress")return;
            const string id="population-auditor";Actor inspector;bool created=!actors.TryGetValue(id,out inspector);
            if(created){inspector=Person(id,true,"Inspector");eventActorIds.Add(id);}
            inspector.Seen=true;inspector.Root.gameObject.SetActive(true);inspector.Motion.Position=world.Population.Audit.Inspector;inspector.Motion.WalkSpeed=.95f;
            inspector.PoseArms=inspector.Speed<.06f;MoveActor(inspector,inspector.Motion,world,force||created);
            if(inspector.PoseArms)
            {
                inspector.Limbs[0].localRotation=Quaternion.Euler(-35,0,-8);inspector.Forearms[0].localRotation=Quaternion.Euler(-65,0,0);
                inspector.Limbs[1].localRotation=Quaternion.Euler(-25,0,8);inspector.Forearms[1].localRotation=Quaternion.Euler(-70,0,0);
            }
            if(!visitorProps.ContainsKey(id))
            {
                Transform board=Box(inspector.Hands[0],"Audit clipboard",new Vector3(0,0,.08f),new Vector3(.25f,.025f,.34f),"clipboard",new Color(.31f,.23f,.16f));
                Box(board,"Audit notes",new Vector3(0,.014f,0),new Vector3(.21f,.005f,.29f),"paper",new Color(.91f,.89f,.80f));visitorProps[id]=board;
            }
            UpdateCharacterNameTag(inspector,world.Standard.Oversight,true);
        }
        void RefreshCampaigner(World world,FacilityIncident incident,int index,bool force)
        {
            string id=incident.Id+"/public/"+index;Actor actor;bool created=!actors.TryGetValue(id,out actor);if(created){actor=Person(id,true,"Visitor");eventActorIds.Add(id);}
            actor.Seen=true;actor.Root.gameObject.SetActive(true);
            // Public sidewalk is outside the playable site. These visitors never enter resident navigation.
            float elapsed=world.Data.Minute-incident.StartMinute;float arrival=Mathf.Clamp01((elapsed-index*2)/32f);
            float departing=Mathf.Clamp01((world.Data.Minute-(incident.DeadlineMinute-35))/30f);
            Vector3 gathering=new Vector3(-12+index*1.25f,.14f,-34.1f-(index%3)*.65f);
            Vector3 target=Vector3.Lerp(new Vector3(-35-index*.8f,.14f,gathering.z),gathering,arrival);target.x+=departing*35;
            Vector3 before=actor.Root.position;actor.Root.position=force||created?target:Vector3.Lerp(before,target,1-Mathf.Exp(-Time.unscaledDeltaTime*9));
            bool walking=arrival<1||departing>0;Vector3 delta=actor.Root.position-before;
            Quaternion heading=walking&&delta.sqrMagnitude>.0001f?Quaternion.LookRotation(delta):Quaternion.Euler(0,180+(index%3-1)*14,0);
            actor.Root.rotation=Quaternion.Slerp(actor.Root.rotation,heading,1-Mathf.Exp(-Time.unscaledDeltaTime*7));
            if(world.Data.Speed>0)actor.Gait+=Time.unscaledDeltaTime*(walking?6:1.2f);
            for(int leg=2;leg<4;leg++)actor.Limbs[leg].localRotation=Quaternion.Euler(walking?Mathf.Sin(actor.Gait+(leg%2)*Mathf.PI)*23:0,0,0);
            actor.Limbs[0].localRotation=Quaternion.Euler(-135,0,-8);actor.Forearms[0].localRotation=Quaternion.Euler(-12,0,0);
            actor.Limbs[1].localRotation=Quaternion.Euler(walking?Mathf.Sin(actor.Gait)*20:-12,0,5);
            if(!visitorProps.ContainsKey(id))
            {
                Transform sign=new GameObject("Campaign placard").transform;sign.SetParent(actor.Hands[0],false);sign.localRotation=Quaternion.Euler(135,0,0);
                Box(sign,"Wooden handle",new Vector3(0,.14f,0),new Vector3(.035f,.65f,.035f),"clipboard",new Color(.42f,.32f,.21f));
                Box(sign,"Placard",new Vector3(0,.63f,0),new Vector3(.84f,.52f,.035f),"placard",new Color(.90f,.85f,.69f));
                var label=new GameObject("Public message").AddComponent<TextMesh>();label.transform.SetParent(sign,false);label.transform.localPosition=new Vector3(0,.63f,.025f);label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=32;label.characterSize=.035f;label.color=new Color(.18f,.23f,.25f);label.text=index%2==0?"DIGNITY\n& CARE":"OPEN THE\nRECORDS";visitorProps[id]=sign;
            }
        }
    }
}
