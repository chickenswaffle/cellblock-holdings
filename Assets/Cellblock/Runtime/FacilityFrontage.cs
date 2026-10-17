using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        // Public presentation stays beyond the editable property and resident navigation.
        void PublicFrontage(Transform site)
        {
            Color stone=new Color(.62f,.63f,.58f),wood=new Color(.38f,.29f,.20f);
            Box(site,"Public sidewalk",new Vector3(0,.015f,-35.4f),new Vector3(130,.12f,3.8f),"paving",stone);
            for(int i=-15;i<=15;i++)Box(site,"Sidewalk expansion joint",new Vector3(i*4,.081f,-35.4f),new Vector3(.025f,.008f,3.75f),"joint",new Color(.38f,.40f,.38f));
            Box(site,"Public curb",new Vector3(0,.16f,-33.45f),new Vector3(130,.27f,.22f),"cap",stone);
            for(int index=0;index<5;index++)
            {
                float x=-25+index*13;
                Box(site,"Street planter",new Vector3(x,.30f,-38),new Vector3(2.6f,.65f,1.3f),"cap",stone);
                Box(site,"Planter soil",new Vector3(x,.63f,-38),new Vector3(2.35f,.05f,1.05f),"soil",new Color(.24f,.22f,.16f));
                for(int shrub=0;shrub<3;shrub++)Sphere(site,"Clipped shrub",new Vector3(x-.8f+shrub*.8f,.82f,-38),new Vector3(.95f,.65f,.85f),"leaf-street",new Color(.28f,.38f,.25f));
                for(int slat=0;slat<5;slat++)Box(site,"Bench slat",new Vector3(x+4,.60f,-37.5f+slat*.12f),new Vector3(2,.07f,.09f),"wood",wood);
                Box(site,"Bench back",new Vector3(x+4,.97f,-37.59f),new Vector3(2,.55f,.075f),"wood",wood);
                for(int side=-1;side<=1;side+=2)Box(site,"Bench iron frame",new Vector3(x+4+side*.72f,.31f,-37.25f),new Vector3(.09f,.58f,.56f),"steel",Steel);
                Cylinder(site,"Public litter bin",new Vector3(x+6,.45f,-37.3f),new Vector3(.55f,.45f,.55f),"street-bin",new Color(.24f,.29f,.29f));
            }
        }
    }
}
