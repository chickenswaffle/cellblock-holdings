using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        sealed class SearchlightView
        {
            public Transform Root, Pan, Head;
            public Light Light;
            public Mesh Mesh;
            public MeshRenderer Beam;
            public float NextRefresh;
            public int Minute=-1, Revision=-1, Level;
            public float Bearing=-1, ClearDistance, VisualBearing=float.NaN;
            public Vector3[] Vertices;
            public Color[] Colors;
            public readonly List<float> Angles=new List<float>();
        }
        readonly Dictionary<string,SearchlightView> searchlights=new Dictionary<string,SearchlightView>();
        void AnimateTowerWatch(Actor actor,StaffMember staff,Fixture tower,World world,float climb,bool snap)
        {
            bool watch=tower!=null&&climb>=1&&world.StaffAtPost(tower,"Tower");
            if(actor.WatchProp==null&&watch)
            {
                actor.WatchProp=new GameObject("Lookout binoculars").transform;actor.WatchProp.SetParent(actor.Model,false);
                foreach(float side in new[]{-.057f,.057f})
                {
                    Cylinder(actor.WatchProp,"Binocular barrel",new Vector3(side,1.67f,.225f),new Vector3(.073f,.075f,.073f),"rubber",new Color(.09f,.12f,.12f)).localRotation=Quaternion.Euler(90,0,0);
                    Cylinder(actor.WatchProp,"Binocular objective",new Vector3(side,1.67f,.303f),new Vector3(.060f,.01f,.060f),"glass",new Color(.11f,.23f,.28f)).localRotation=Quaternion.Euler(90,0,0);
                }
                Box(actor.WatchProp,"Binocular bridge",new Vector3(0,1.67f,.22f),new Vector3(.13f,.035f,.05f),"rubber",new Color(.09f,.12f,.12f));Combine(actor.WatchProp);
            }
            if(actor.WatchProp!=null)actor.WatchProp.gameObject.SetActive(watch);
            if(!watch){actor.Model.localRotation=Quaternion.Slerp(actor.Model.localRotation,Quaternion.identity,snap?1:1-Mathf.Exp(-Time.unscaledDeltaTime*9));return;}
            float bearing=world.SearchlightBearing(tower);
            actor.Model.localRotation=Quaternion.Inverse(actor.Root.rotation)*Quaternion.Euler(0,bearing,0);
            if(actor.Head!=null)actor.Head.localRotation=Quaternion.identity;
            ArmPose(actor,0,new Vector3(-.10f,1.60f,.23f),snap?1:1-Mathf.Exp(-Time.unscaledDeltaTime*9));
            ArmPose(actor,1,new Vector3(.10f,1.60f,.23f),snap?1:1-Mathf.Exp(-Time.unscaledDeltaTime*9));
        }
        Material SearchlightMaterial()
        {
            const string key="Searchlight beam";Material material;if(materials.TryGetValue(key,out material))return material;
            Shader shader=Resources.Load<Shader>("SearchlightBeam");
            if(shader==null)throw new System.InvalidOperationException("The retained SearchlightBeam shader is missing.");
            material=new Material(shader){name=key};materials.Add(key,material);return material;
        }
        SearchlightView CreateSearchlight(Fixture tower)
        {
            var root=new GameObject("Tower searchlight · "+tower.Id).transform;root.SetParent(securityRoot,false);
            Transform pan=new GameObject("Motorized yaw bearing").transform;pan.SetParent(root,false);pan.localPosition=new Vector3(0,5.13f,0);
            Cylinder(pan,"Turntable",Vector3.zero,new Vector3(.24f,.09f,.24f),"steel",Steel);
            foreach(float side in new[]{-.29f,.29f})Box(pan,"Lamp yoke",new Vector3(side,.2f,0),new Vector3(.065f,.4f,.16f),"steel",Steel);
            Combine(pan);
            Transform head=new GameObject("Tilting searchlight head").transform;head.SetParent(pan,false);head.localPosition=new Vector3(0,.24f,0);
            SoftBox(head,"Searchlight housing",Vector3.zero,new Vector3(.53f,.42f,.62f),"steel",new Color(.3f,.35f,.35f));
            for(int i=0;i<5;i++)Box(head,"Cooling fin",new Vector3(0,-.19f+i*.09f,-.34f),new Vector3(.49f,.026f,.07f),"rubber",new Color(.12f,.15f,.15f));
            Cylinder(head,"Lens surround",new Vector3(0,0,.34f),new Vector3(.38f,.035f,.38f),"rubber",new Color(.12f,.15f,.15f)).localRotation=Quaternion.Euler(90,0,0);
            Cylinder(head,"Searchlight lens",new Vector3(0,0,.38f),new Vector3(.31f,.014f,.31f),"lamp",new Color(.94f,.91f,.72f)).localRotation=Quaternion.Euler(90,0,0);
            foreach(float side in new[]{-.3f,.3f})Cylinder(head,"Elevation hinge",new Vector3(side,0,0),new Vector3(.13f,.055f,.13f),"steel",Steel).localRotation=Quaternion.Euler(0,0,90);
            Combine(head);
            Light light=new GameObject("Local searchlight / no additional shadows").AddComponent<Light>();light.transform.SetParent(head,false);light.transform.localPosition=new Vector3(0,0,.42f);light.type=LightType.Spot;light.shadows=LightShadows.None;light.color=new Color(1,.91f,.72f);light.intensity=45;light.innerSpotAngle=20;light.spotAngle=38;
            var beam=new GameObject("Wall-clipped beam and ground pool");beam.transform.SetParent(root,false);beam.layer=29;
            var mesh=new Mesh{name="Generated clipped searchlight",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();beam.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=beam.AddComponent<MeshRenderer>();renderer.sharedMaterial=SearchlightMaterial();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var view=new SearchlightView{Root=root,Pan=pan,Head=head,Light=light,Mesh=mesh,Beam=renderer,Level=tower.UpgradeLevel};
            searchlights.Add(tower.Id,view);return view;
        }
        void RefreshSearchlights(World world,bool force)
        {
            foreach(string id in searchlights.Keys.Where(id=>!world.Data.Fixtures.Any(f=>f.Id==id&&f.Kind=="GuardTower"&&f.UpgradeLevel>0)).ToArray())
            {Clear(searchlights[id].Root);Destroy(searchlights[id].Root.gameObject);searchlights.Remove(id);}
            foreach(Fixture tower in world.Data.Fixtures.Where(f=>f.Kind=="GuardTower"&&f.UpgradeLevel>0))
            {
                SearchlightView view;if(!searchlights.TryGetValue(tower.Id,out view))view=CreateSearchlight(tower);
                view.Root.position=V(tower.Position,0);
                bool active=world.SearchlightOnline(tower);view.Beam.enabled=active;view.Light.enabled=active&&view.ClearDistance>2;
                float targetBearing=world.SearchlightBearing(tower);
                float bearing=force||world.Data.Speed==0||float.IsNaN(view.VisualBearing)?targetBearing:Mathf.LerpAngle(view.VisualBearing,targetBearing,1-Mathf.Exp(-Time.unscaledDeltaTime*18*Mathf.Sqrt(world.Data.Speed)));
                view.VisualBearing=bearing;view.Pan.localRotation=Quaternion.Euler(0,bearing,0);
                float range=world.SearchlightRange(tower),half=world.SearchlightHalfAngle(tower);
                view.Light.spotAngle=half*2;view.Light.innerSpotAngle=half;
                view.Head.localRotation=Quaternion.Euler(Mathf.Atan2(5.37f,range*.65f)*Mathf.Rad2Deg,0,0);
                if(!active)continue;
                if(!force&&view.Minute==world.Data.Minute&&view.Revision==world.GeometryRevision&&Mathf.Abs(view.Bearing-bearing)<.01f&&view.Level==tower.UpgradeLevel)continue;
                if(!force&&Time.unscaledTime<view.NextRefresh)continue;
                view.NextRefresh=Time.unscaledTime+.10f;view.Minute=world.Data.Minute;view.Revision=world.GeometryRevision;view.Bearing=bearing;view.Level=tower.UpgradeLevel;
                var angles=view.Angles;angles.Clear();for(int i=0;i<=44;i++)angles.Add(-half+i/44f*half*2);
                float shortest=range;
                foreach(Wall wall in world.Data.Walls)foreach(Segment segment in Geometry.Segments(wall,true))
                {
                    // Sample every wall/opening endpoint and both clearance tangents as well
                    // as the uniform fan, so narrow barriers cannot fall between broad rays.
                    foreach(Point end in new[]{segment.A,segment.B})
                    {
                        Point d=end-tower.Position;if(d.Length>range+.2f)continue;
                        float relative=Mathf.DeltaAngle(bearing,Mathf.Atan2(d.X,d.Y)*Mathf.Rad2Deg),margin=Mathf.Atan2(.20f,d.Length)*Mathf.Rad2Deg;
                        foreach(float offset in new[]{-margin-.02f,-margin+.02f,0,margin-.02f,margin+.02f})
                            if(relative+offset>-half&&relative+offset<half)angles.Add(relative+offset);
                    }
                    shortest=Mathf.Min(shortest,Geometry.DistanceToSegment(tower.Position,segment.A,segment.B)-.65f);
                }
                angles.Sort();for(int i=angles.Count-1;i>0;i--)if(angles[i]-angles[i-1]<.001f)angles.RemoveAt(i);
                int count=angles.Count,apex=count*2+1;
                if(view.Vertices==null||view.Vertices.Length!=apex+1)
                {
                    view.Mesh.Clear();view.Vertices=new Vector3[apex+1];view.Colors=new Color[apex+1];
                    var triangles=new List<int>();
                    for(int i=0;i<count-1;i++)triangles.AddRange(new[]{0,1+i*2,3+i*2,1+i*2,2+i*2,4+i*2,1+i*2,4+i*2,3+i*2,apex,2+i*2,4+i*2});
                    view.Mesh.vertices=view.Vertices;view.Mesh.SetTriangles(triangles,0);
                }
                view.Vertices[0]=new Vector3(0,.19f,0);view.Colors[0]=new Color(1,.92f,.65f,.035f);
                for(int i=0;i<count;i++)
                {
                    float angle=(bearing+angles[i])*Mathf.Deg2Rad;Point direction=new Point(Mathf.Sin(angle),Mathf.Cos(angle));float clear=world.SearchlightReach(tower,bearing+angles[i]);
                    float edge=Mathf.Sin((angles[i]+half)/(half*2)*Mathf.PI);
                    view.Vertices[1+i*2]=new Vector3(direction.X*clear*.65f,.19f,direction.Y*clear*.65f);view.Vertices[2+i*2]=new Vector3(direction.X*clear,.19f,direction.Y*clear);
                    view.Colors[1+i*2]=new Color(1,.92f,.65f,.055f*edge);view.Colors[2+i*2]=new Color(1,.92f,.65f,0);
                }
                view.Vertices[apex]=new Vector3(0,5.37f,0);view.Colors[apex]=new Color(1,.95f,.78f,.018f);
                // The clipped ground pool represents coverage. A conservative local Spot
                // supplies surface highlights only inside the nearest wall in ANY direction.
                shortest=Mathf.Max(0,shortest);
                view.ClearDistance=shortest;view.Light.range=Mathf.Sqrt(shortest*shortest+5.37f*5.37f);view.Light.enabled=shortest>2;
                view.Mesh.vertices=view.Vertices;view.Mesh.colors=view.Colors;view.Mesh.RecalculateBounds();
            }
        }
#if UNITY_EDITOR
        public int PreviewSearchlightCount {get{return searchlights.Count;}}
        public int PreviewActiveBeamCount {get{return searchlights.Values.Count(v=>v.Beam.enabled);}}
        public float PreviewSearchlightBearing(string id){return searchlights[id].Pan.eulerAngles.y;}
        public bool PreviewBeamRaysClear(World world)
        {
            foreach(var pair in searchlights)
            {
                Fixture tower=world.Data.Fixtures.First(f=>f.Id==pair.Key);var v=pair.Value;if(!v.Beam.enabled||v.Vertices==null)continue;
                foreach(Vector3 point in v.Vertices)
                    if(point.y<1&&!world.SightLines.Clear(tower.Position,tower.Position+new Point(point.x,point.z)))return false;
            }
            return true;
        }
#endif
    }
}
