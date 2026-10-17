using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        Transform DetailedHead(Transform torso,int seed,string skinName,Color skin,bool staff)
        {
            Transform head=new GameObject("Face / gaze pivot").transform;head.SetParent(torso,false);head.localPosition=new Vector3(0,.72f,0);
            float width=.94f+(seed%5)*.025f;
            Sphere(head,"Cranium",new Vector3(0,.014f,-.008f),new Vector3(.207f*width,.265f,.221f),skinName,skin);
            Sphere(head,"Jaw and cheeks",new Vector3(0,-.055f,.025f),new Vector3(.175f*width,.145f,.174f),skinName,skin);
            Color lip=Color.Lerp(skin,new Color(.37f,.20f,.17f),.26f);
            for(int side=-1;side<=1;side+=2)
            {
                Sphere(head,"Ear",new Vector3(side*.105f*width,-.008f,0),new Vector3(.041f,.071f,.039f),skinName,skin);
                Sphere(head,"Eye socket",new Vector3(side*.044f,.018f,.093f),new Vector3(.053f,.031f,.025f),"socket-"+seed%4,Color.Lerp(skin,Color.black,.20f));
                Sphere(head,"Eye",new Vector3(side*.044f,.020f,.106f),new Vector3(.043f,.019f,.014f),"eye-white",new Color(.84f,.83f,.76f));
                Sphere(head,"Iris",new Vector3(side*.044f,.020f,.114f),new Vector3(.014f,.016f,.006f),"iris-"+seed%3,seed%3==0?new Color(.18f,.26f,.27f):new Color(.20f,.15f,.10f));
                Box(head,"Eyebrow",new Vector3(side*.044f,.044f,.105f),new Vector3(.046f,.009f,.009f),"hair-"+seed%3,new Color(.15f,.12f,.10f));
                Sphere(head,"Nostril",new Vector3(side*.012f,-.029f,.129f),new Vector3(.008f,.006f,.005f),"socket-"+seed%4,Color.Lerp(skin,Color.black,.20f));
            }
            Sphere(head,"Nose bridge",new Vector3(0,-.005f,.116f),new Vector3(.029f,.065f,.037f),skinName,skin);
            Sphere(head,"Nose tip",new Vector3(0,-.025f,.133f),new Vector3(.037f,.025f,.031f),skinName,skin);
            Box(head,"Upper lip",new Vector3(0,-.057f,.112f),new Vector3(.051f,.007f,.009f),"lip-"+seed%4,lip);
            Sphere(head,"Lower lip",new Vector3(0,-.064f,.109f),new Vector3(.045f,.010f,.009f),"lip-"+seed%4,lip);
            Color hair=seed%3==0?new Color(.12f,.105f,.09f):seed%3==1?new Color(.28f,.22f,.15f):new Color(.34f,.33f,.29f);
            if(seed%6!=0)
            {
                Sphere(head,"Hair cut",new Vector3(0,.108f,-.016f),new Vector3(.214f*width,seed%4==0?.105f:.077f,.219f),"hair-"+seed%3,hair);
                if(seed%4==1)for(int i=0;i<5;i++)Sphere(head,"Hair texture",new Vector3(-.078f+i*.039f,.142f,-.01f),new Vector3(.058f,.042f,.14f),"hair-"+seed%3,hair);
                if(seed%4==2)Sphere(head,"Combed part",new Vector3(-.032f,.148f,.005f),new Vector3(.095f,.035f,.185f),"hair-"+seed%3,hair);
            }
            if(seed%5==0)Sphere(head,"Trimmed beard",new Vector3(0,-.088f,.047f),new Vector3(.160f,.073f,.128f),"hair-"+seed%3,hair);
            if(seed%7==0)
            {
                foreach(float side in new[]{-1f,1f})
                {
                    Box(head,"Glasses upper rim",new Vector3(side*.045f,.035f,.124f),new Vector3(.060f,.006f,.007f),"rubber",new Color(.11f,.13f,.13f));
                    Box(head,"Glasses lower rim",new Vector3(side*.045f,.007f,.124f),new Vector3(.060f,.006f,.007f),"rubber",new Color(.11f,.13f,.13f));
                    foreach(float edge in new[]{-.03f,.03f})Box(head,"Glasses rim",new Vector3(side*.045f+edge,.021f,.124f),new Vector3(.006f,.032f,.007f),"rubber",new Color(.11f,.13f,.13f));
                }
                Box(head,"Glasses bridge",new Vector3(0,.029f,.126f),new Vector3(.031f,.006f,.006f),"rubber",Steel);
            }
            Combine(head);return head;
        }
        void DressCharacter(Transform body,int seed,bool staff,string role,string cloth,Color uniform)
        {
            foreach(float side in new[]{-1f,1f})
            {
                Transform collar=Box(body,"Folded collar",new Vector3(side*.055f,1.43f,.105f),new Vector3(.09f,.06f,.024f),cloth,uniform);collar.localRotation=Quaternion.Euler(0,0,side*22);
                Box(body,"Sleeve cuff seam",new Vector3(side*.19f,1.29f,.068f),new Vector3(.04f,.06f,.025f),"cloth-seam",new Color(.28f,.29f,.25f));
            }
            for(int i=0;i<4;i++)Sphere(body,"Shirt button",new Vector3(.012f,1.12f+i*.075f,.132f),Vector3.one*.014f,"button",new Color(.33f,.34f,.30f));
            Box(body,"Hem seam",new Vector3(0,.985f,.12f),new Vector3(.30f,.012f,.01f),"cloth-seam",new Color(.28f,.29f,.25f));
            Box(body,"Trouser fly",new Vector3(.015f,.91f,.123f),new Vector3(.009f,.12f,.01f),"cloth-seam",new Color(.28f,.29f,.25f));
            if(!staff)
            {
                Box(body,"Resident cloth identification",new Vector3(-.082f,1.32f,.145f),new Vector3(.10f,.045f,.008f),"resident-tag",new Color(.75f,.73f,.64f));
                for(int i=0;i<3;i++)Box(body,"Identification marks",new Vector3(-.11f+i*.026f,1.32f,.151f),new Vector3(.014f,.021f,.003f),"ink",new Color(.19f,.20f,.18f));
                return;
            }
            if(role=="Officer")
            {
                Box(body,"Radio",new Vector3(-.19f,1.35f,.11f),new Vector3(.075f,.12f,.05f),"rubber",new Color(.09f,.12f,.14f));
                Cylinder(body,"Radio antenna",new Vector3(-.19f,1.46f,.11f),new Vector3(.012f,.06f,.012f),"rubber",Color.black);
                Box(body,"Duty pouch",new Vector3(.18f,.97f,.04f),new Vector3(.09f,.14f,.10f),"rubber",new Color(.10f,.13f,.15f));
                Box(body,"Key clip",new Vector3(-.16f,.90f,.08f),new Vector3(.04f,.08f,.018f),"steel",new Color(.55f,.57f,.51f));
                foreach(float side in new[]{-1f,1f})Box(body,"Shoulder epaulet",new Vector3(side*.16f,1.43f,0),new Vector3(.10f,.025f,.12f),"uniform-epaulet",new Color(.12f,.19f,.24f));
            }
            if(role=="Technician")Box(body,"Service belt",new Vector3(.17f,.96f,.07f),new Vector3(.13f,.19f,.09f),"wood",new Color(.40f,.31f,.20f));
        }
        void CreateCharacterNameTag(Actor actor)
        {
            var tag=new GameObject("Character identification");tag.transform.SetParent(actor.Model,false);tag.transform.localPosition=new Vector3(0,2.06f,0);tag.layer=29;
            actor.NameTag=tag.AddComponent<TextMesh>();actor.NameTag.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");actor.NameTag.fontSize=40;actor.NameTag.characterSize=.055f;actor.NameTag.anchor=TextAnchor.MiddleCenter;actor.NameTag.color=new Color(.94f,.93f,.85f);tag.GetComponent<MeshRenderer>().sharedMaterial=actor.NameTag.font.material;tag.SetActive(false);
        }
        void UpdateCharacterNameTag(Actor actor,string name,bool visible)
        {
            if(actor.NameTag==null)return;actor.NameTag.gameObject.SetActive(visible);if(!visible)return;
            if(actor.NameTag.text!=name)actor.NameTag.text=name;
            Camera camera=Camera.main;if(camera!=null)actor.NameTag.transform.rotation=camera.transform.rotation;
        }
        void AnimateCharacterDetail(Actor actor,Resident resident,World world,bool walking,float dt)
        {
            if(actor.Head==null||world.Data.Speed==0)return;
            float phase=world.Data.Minute*.028f+actor.Gait*.04f;
            float yaw=walking?Mathf.Sin(phase*.8f)*3:Mathf.Sin(phase*.6f)*11;
            actor.Head.localRotation=Quaternion.Slerp(actor.Head.localRotation,Quaternion.Euler(walking?-2:Mathf.Sin(phase*.4f)*2,yaw,0),1-Mathf.Exp(-dt*3));
        }
    }
}
