using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        readonly Stack<Plan> doorEdits=new Stack<Plan>();
        LineRenderer doorOutline;Material doorGuideMaterial;
        public bool CanUndoDoor {get{return doorEdits.Count>0;}}
        bool DoorClickAvailable {get{return World!=null&&(Tool==BuildTool.Door||(Draft!=null&&Draft.Walls.Count>0));}}
        void FindDoorWall(Vector2 screen,out Wall selected,out Point point)
        {
            selected=null;point=default(Point);float nearest=float.MaxValue;Ray ray=cameraRig.ScreenPointToRay(screen);
            var candidates=Draft==null?World.Data.Walls:Draft.Walls.Concat(World.Data.Walls.Where(w=>!Draft.RemoveWalls.Contains(w.Id)));
            foreach(Wall wall in candidates)foreach(Segment segment in Geometry.Segments(wall,false))
            {
                Point delta=segment.B-segment.A;float hit;float h=view.WallHeight;
                if(HitBox(ray,FacilityView.V((segment.A+segment.B)*.5f,h*.5f+.12f),Quaternion.Euler(0,-Mathf.Atan2(delta.Y,delta.X)*Mathf.Rad2Deg,0),new Vector3(delta.Length,h,.40f),out hit)&&hit<nearest-.001f)
                {selected=wall;nearest=hit;Vector3 p=ray.GetPoint(hit);point=new Point(p.x,p.z);}
            }
            if(selected!=null)return;Point ground;if(!Ground(screen,out ground))return;float close=.8f;
            foreach(Wall wall in candidates)
            {
                Point candidate=Geometry.OnLength(wall,Geometry.NearestFraction(wall,ground));float distance=Geometry.Distance(candidate,ground);
                if(distance<close){close=distance;selected=wall;point=candidate;}
            }
        }
        void PreviewDoor(Vector2 screen)
        {
            if(!DoorClickAvailable){HideDoorPreview();return;}
            Wall wall;Point click;FindDoorWall(screen,out wall,out click);if(wall==null){HideDoorPreview();return;}
            string notice=World.Notice;Plan candidate=World.DoorAt(Draft,wall,click);World.Notice=notice;
            float at=Geometry.NearestFraction(wall,click),length=Geometry.Length(wall);
            Point a=Geometry.OnLength(wall,at-.8f/Mathf.Max(.001f,length)),b=Geometry.OnLength(wall,at+.8f/Mathf.Max(.001f,length));
            if(doorOutline==null)
            {
                doorGuideMaterial=new Material(Resources.Load<Material>("LuminaireMaterial")){name="Door location preview"};
                var go=new GameObject("Click-to-place doorway outline");go.transform.SetParent(transform,false);doorOutline=go.AddComponent<LineRenderer>();
                doorOutline.sharedMaterial=doorGuideMaterial;doorOutline.useWorldSpace=true;doorOutline.positionCount=5;doorOutline.startWidth=doorOutline.endWidth=.065f;doorOutline.numCornerVertices=3;doorOutline.shadowCastingMode=ShadowCastingMode.Off;doorOutline.receiveShadows=false;
            }
            Color color=candidate==null?new Color(.95f,.28f,.18f):new Color(.20f,.93f,.72f);doorGuideMaterial.color=color;doorGuideMaterial.EnableKeyword("_EMISSION");doorGuideMaterial.SetColor("_EmissionColor",color*.8f);
            Vector3 left=FacilityView.V(a,.12f),right=FacilityView.V(b,.12f);Vector3 normal=Vector3.Cross((right-left).normalized,Vector3.up)*.23f;
            if(Vector3.Dot(cameraRig.transform.position-(left+right)*.5f,normal)<0)normal=-normal;
            left+=normal;right+=normal;Vector3 top=Vector3.up*Mathf.Min(2.2f,view.WallHeight);
            doorOutline.SetPositions(new[]{left,left+top,right+top,right,left});doorOutline.gameObject.SetActive(true);
            PlacementSummary=candidate==null?"Door needs space here · try another position":"Click to add a 1.6 m door · quote updates before approval";
        }
        bool PlaceDoor(Vector2 screen)
        {
            Wall wall;Point click;FindDoorWall(screen,out wall,out click);
            if(wall==null){Message("Click a visible wall to add a door. Drag to move around the plan.");return false;}
            Plan candidate=World.DoorAt(Draft,wall,click);if(candidate==null){Message(World.Notice);return false;}
            Quote quote=World.Quote(candidate);if(!quote.Valid&&(Draft==null||CurrentQuote==null||CurrentQuote.Valid)){Message(quote.Message);return false;}
            Plan before=Draft;PlacingConnection=false;SetDraft(candidate);doorEdits.Push(before);Instruction="Door added. Click another wall to add more, or approve the complete project.";HideDoorPreview();hud.Refresh(true);return true;
        }
        public void UndoDoor()
        {
            if(doorEdits.Count==0)return;Plan previous=doorEdits.Pop();if(previous==null){Cancel();return;}SetDraft(previous);HideDoorPreview();hud.Refresh(true);
        }
        void HideDoorPreview(){if(doorOutline!=null)doorOutline.gameObject.SetActive(false);}
        void ResetDoorEditing(){doorEdits.Clear();HideDoorPreview();}
        void DisposeDoorPreview(){if(doorGuideMaterial!=null)Destroy(doorGuideMaterial);}
#if UNITY_EDITOR
        public bool PreviewClickDoorAt(Point point){Vector3 screen=cameraRig.WorldToScreenPoint(FacilityView.V(point,view.WallHeight*.5f));return PlaceDoor(screen);}
        public void PreviewHoverDoorAt(Point point){PreviewDoor(cameraRig.WorldToScreenPoint(FacilityView.V(point,view.WallHeight*.5f)));}
        public bool PreviewDoorOutlineVisible {get{return doorOutline!=null&&doorOutline.gameObject.activeSelf;}}
#endif
    }
}
