using System.Collections.Generic;
using UnityEngine;
namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        void DoorPlanFrame(Transform parent,Cellblock.Core.Wall wall)
        {
            float length=Cellblock.Core.Geometry.Length(wall),height=Mathf.Min(2.2f,WallHeight)+.14f;
            var a=Cellblock.Core.Geometry.OnLength(wall,wall.Door-wall.DoorWidth*.5f/length);
            var b=Cellblock.Core.Geometry.OnLength(wall,wall.Door+wall.DoorWidth*.5f/length);
            Color color=new Color(.12f,.72f,.57f);
            Beam(parent,a,b,height,.06f,.48f,"door-outline",color);
            foreach(var point in new[]{a,b})Box(parent,"Planned door jamb",V(point,(height+.14f)*.5f),new Vector3(.065f,height-.14f,.48f),"door-outline",color);
        }
        readonly Dictionary<Vector3,Mesh> bevels=new Dictionary<Vector3,Mesh>();
        static bool SoftEdge(string name,string material,Vector3 size)
        {
            if(material=="ghost"||material=="planned"||material=="handle"||Mathf.Min(size.x,size.y,size.z)<.025f)return false;
            return name=="Mattress"||name=="Folded blanket"||name=="Worktop edge"||name=="Laminate surface"||name=="Fixed bench"||name=="Drawer cabinet"||name=="Underbed storage"||name=="Meal tray"||name=="Monitor"||name=="Keyboard"||name=="Weatherproof camera"||name=="Lower body"||name=="Cabin"||name.Contains("cushion")||name.Contains("Cushion");
        }
        Transform SoftBox(Transform parent,string name,Vector3 position,Vector3 size,string material,Color color)
        {
            Mesh mesh;if(!bevels.TryGetValue(size,out mesh)){mesh=BeveledMesh(size);bevels.Add(size,mesh);}
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Material(material,color);return go.transform;
        }
        internal static Mesh BeveledMesh(Vector3 size,float maxRadius=.045f)
        {
            Vector3 half=size*.5f;float radius=Mathf.Min(maxRadius,Mathf.Min(size.x,size.y,size.z)*.20f);Vector3 inset=half-Vector3.one*radius;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            System.Action<Vector3[],Vector3> polygon=(points,normal)=>{
                if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)System.Array.Reverse(points);
                int start=vertices.Count;
                foreach(Vector3 point in points){vertices.Add(point);normals.Add(normal);uv.Add(Mathf.Abs(normal.y)>.65f?new Vector2(point.x,point.z):Mathf.Abs(normal.z)>Mathf.Abs(normal.x)?new Vector2(point.x,point.y):new Vector2(point.z,point.y));}
                for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
            };
            // Six broad faces, twelve edge chamfers and eight clipped corners. Every bevel
            // stays within the original dimensions; contact, routing and selection stay exact.
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int b=(axis+1)%3,c=(axis+2)%3;var p=new Vector3[4];int[] sb={-1,1,1,-1},sc={-1,-1,1,1};
                for(int i=0;i<4;i++){p[i][axis]=half[axis]*sign;p[i][b]=inset[b]*sb[i];p[i][c]=inset[c]*sc[i];}
                Vector3 normal=Vector3.zero;normal[axis]=sign;polygon(p,normal);
            }
            for(int axis=0;axis<3;axis++)for(int bSign=-1;bSign<=1;bSign+=2)for(int cSign=-1;cSign<=1;cSign+=2)
            {
                int b=(axis+1)%3,c=(axis+2)%3;var p=new Vector3[4];
                for(int i=0;i<4;i++){p[i][axis]=inset[axis]*(i<2?-1:1);p[i][b]=(i==0||i==3?half[b]:inset[b])*bSign;p[i][c]=(i==0||i==3?inset[c]:half[c])*cSign;}
                Vector3 normal=Vector3.zero;normal[b]=bSign;normal[c]=cSign;polygon(p,normal.normalized);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                polygon(new[]{new Vector3(half.x*x,inset.y*y,inset.z*z),new Vector3(inset.x*x,half.y*y,inset.z*z),new Vector3(inset.x*x,inset.y*y,half.z*z)},new Vector3(x,y,z).normalized);
            var mesh=new Mesh{name="Cellblock furniture bevel"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        Texture2D SurfaceNormal(string surface)
        {
            string key="normal-"+surface;Texture2D texture;if(textures.TryGetValue(key,out texture))return texture;
            const int size=128;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=Height(surface,(x+1)%size,y)-Height(surface,(x+size-1)%size,y),dy=Height(surface,x,(y+1)%size)-Height(surface,x,(y+size-1)%size);
                Vector3 n=new Vector3(-dx,-dy,1).normalized;pixels[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,1);
            }
            texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Cellblock micro relief "+surface,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=2};texture.SetPixels32(pixels);texture.Apply(true,true);textures.Add(key,texture);return texture;
        }
        static float Height(string surface,int x,int y)
        {
            uint seed=unchecked((uint)(x*73856093^y*19349663));seed^=seed>>13;seed*=1274126177u;float noise=(seed&255)/255f;
            if(surface=="tile")return x%64<2||y%64<2?0:.35f;
            if(surface=="brick")return y%16<2||(x+(y/16%2)*32)%64<2?0:.42f+noise*.08f;
            if(surface=="wood")return Mathf.Sin(x*Mathf.PI/8+Mathf.Sin(y*Mathf.PI/32)*.8f)*.06f+noise*.025f;
            if(surface=="fabric")return ((x%2)+(y%2))*.035f;
            return noise*.06f;
        }
    }
}
