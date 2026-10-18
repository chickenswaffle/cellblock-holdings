using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        Transform DetailedHead(Transform torso,int seed,string skinName,Color skin,bool staff)
        {
            Transform head = new GameObject("Face / gaze pivot").transform;
            head.SetParent(torso, false); head.localPosition = new Vector3(0, .72f, 0);
            float width = .84f + seed % 5 * .020f;
            string shadowName = "socket-" + seed % 4, hairName = "hair-" + seed % 3, lipName = "lip-" + seed % 4;
            Color shadow = Color.Lerp(skin, Color.black, .18f);
            Color lip = Color.Lerp(skin, new Color(.37f, .20f, .17f), .23f);
            Color hair = seed % 3 == 0 ? new Color(.12f, .105f, .09f) : seed % 3 == 1 ? new Color(.28f, .22f, .15f) : new Color(.34f, .33f, .29f);
            HumanHeadShell(head, width, skinName, skin);
            // Eyes sit on the curved face and are bounded by eyelids, with no protruding eyeball.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .041f * width;
                HumanEyeOpening(head, x, .023f, width, "eye-white", new Color(.78f, .78f, .72f));
                Vector3 iris = HumanFacePoint(x, .023f, width, .0045f);
                Vector3 irisAxis = (HumanFacePoint(x + .004f, .023f, width) - HumanFacePoint(x - .004f, .023f, width)) / 2;
                HumanOval(head, "Iris", iris, irisAxis, Vector3.up * .0045f, "iris-" + seed % 3,
                    seed % 3 == 0 ? new Color(.18f, .26f, .27f) : new Color(.20f, .15f, .10f), .0004f);
                HumanOval(head, "Pupil", iris + Vector3.forward * .0008f, irisAxis * .43f, Vector3.up * .0022f, "ink", new Color(.19f, .20f, .18f), .0002f);
                for (int lid = -1; lid <= 1; lid += 2)
                {
                    var path = new Vector3[7];
                    for (int i = 0; i < path.Length; i++)
                    {
                        float t = i / (float)(path.Length - 1);
                        path[i] = HumanFacePoint(x + Mathf.Lerp(-.0155f, .0155f, t), .023f + lid * Mathf.Sin(t * Mathf.PI) * .005f, width, .004f);
                    }
                    HumanRibbon(head, lid > 0 ? "Upper eyelid" : "Lower eyelid", path, lid > 0 ? .0036f : .0026f, skinName, skin, Vector3.forward);
                }
                HumanRibbon(head, "Natural eyebrow", new[] {
                    HumanFacePoint(x - .017f, .043f - side * .002f, width, .003f),
                    HumanFacePoint(x, .047f, width, .003f),
                    HumanFacePoint(x + .017f, .043f + side * .002f, width, .003f)
                }, .0042f, hairName, hair, Vector3.forward);
                Vector3 ear = new Vector3(side * .109f * width, -.009f, -.003f);
                HumanOval(head, "Ear pinna", ear, Vector3.forward * (-side * .017f), Vector3.up * .033f, skinName, skin, .005f);
                var helix = new Vector3[17];
                for (int i = 0; i < helix.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / (helix.Length - 1);
                    helix[i] = ear + new Vector3(side * .008f, Mathf.Sin(angle) * .029f, Mathf.Cos(angle) * .014f);
                }
                HumanRibbon(head, "Ear helix", helix, .004f, skinName, skin, Vector3.right * side);
                HumanOval(head, "Ear concha", ear + Vector3.right * side * .006f, Vector3.forward * (-side * .006f), Vector3.up * .014f, shadowName, shadow, .001f);
                HumanOval(head, "Nostril", new Vector3(side * .011f, -.036f, .126f), Vector3.right * .0034f, Vector3.up * .0018f, shadowName, shadow, .0005f);
            }
            HumanLoft(head, "Nose bridge and alar contour", new[] {
                new Vector3(0, -.041f, .115f), new Vector3(.012f, 0, .010f),
                new Vector3(0, -.029f, .122f), new Vector3(.018f, 0, .018f),
                new Vector3(0, -.005f, .111f), new Vector3(.010f, 0, .017f),
                new Vector3(0, .033f, .096f), new Vector3(.008f, 0, .006f)
            }, 8, skinName, skin);
            HumanRibbon(head, "Upper lip contour", new[] {
                HumanFacePoint(-.022f, -.062f, width, .002f), HumanFacePoint(-.010f, -.059f, width, .004f),
                HumanFacePoint(0, -.061f, width, .004f), HumanFacePoint(.010f, -.059f, width, .004f), HumanFacePoint(.022f, -.062f, width, .002f)
            }, .004f, lipName, lip, Vector3.forward);
            HumanRibbon(head, "Lower lip contour", new[] {
                HumanFacePoint(-.020f, -.064f, width, .002f), HumanFacePoint(0, -.068f, width, .004f), HumanFacePoint(.020f, -.064f, width, .002f)
            }, .004f, lipName, lip, Vector3.forward);
            if (seed % 6 != 0) HumanHair(head, seed, width, hairName, hair);
            if (seed % 5 == 0) HumanBeard(head, width, hairName, hair);
            if (seed % 7 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * .041f * width;
                    HumanRibbon(head, "Glasses frame", new[] {
                        new Vector3(x - .021f, .035f, .105f), new Vector3(x + .021f, .035f, .105f),
                        new Vector3(x + .019f, .010f, .106f), new Vector3(x - .019f, .010f, .106f), new Vector3(x - .021f, .035f, .105f)
                    }, .0027f, "rubber", new Color(.11f, .13f, .13f), Vector3.forward);
                    HumanRibbon(head, "Glasses temple", new[] {
                        new Vector3(side * .063f, .031f, .104f), new Vector3(side * .106f * width, .023f, .040f), new Vector3(side * .108f * width, .013f, -.018f)
                    }, .003f, "rubber", new Color(.11f, .13f, .13f), Vector3.up);
                }
                HumanRibbon(head, "Glasses bridge", new[] { new Vector3(-.020f, .030f, .107f), new Vector3(0, .034f, .111f), new Vector3(.020f, .030f, .107f) }, .0028f, "rubber", Steel, Vector3.forward);
            }
            Combine(head); return head;
        }
        void DressCharacter(Transform body,int seed,bool staff,string role,string cloth,Color uniform)
        {
            Color seam = new Color(.28f, .29f, .25f), hardware = new Color(.55f, .57f, .51f);
            for (int side = -1; side <= 1; side += 2)
            {
                HumanPolygon(body, "Folded shirt collar", new[] {
                    new Vector3(side * .018f, 1.462f, .060f), new Vector3(side * .070f, 1.445f, .075f),
                    new Vector3(side * .099f, 1.413f, .094f), new Vector3(side * .059f, 1.382f, .124f), new Vector3(side * .025f, 1.420f, .113f)
                }, cloth, uniform);
                HumanClothFold(body, "Waist fabric fold", new[] {
                    HumanShirtPoint(side * .145f, 1.077f), HumanShirtPoint(side * .100f, 1.099f), HumanShirtPoint(side * .045f, 1.110f)
                }, .012f, cloth, uniform);
                HumanClothFold(body, "Side fabric fold", new[] {
                    HumanShirtPoint(side * .160f, 1.200f), HumanShirtPoint(side * .137f, 1.180f), HumanShirtPoint(side * .128f, 1.159f)
                }, .009f, cloth, uniform);
                HumanClothFold(body, "Back waist fold", new[] {
                    HumanShirtPoint(side * .135f, 1.084f, .003f, true), HumanShirtPoint(side * .080f, 1.106f, .003f, true), HumanShirtPoint(side * .032f, 1.108f, .003f, true)
                }, .012f, cloth, uniform);
                Box(body, "Belt loop", new Vector3(side * .113f, .977f, .119f), new Vector3(.016f, .040f, .009f), "trousers", new Color(.30f, .34f, .33f));
            }
            HumanPolygon(body, "Button placket", new[] {
                HumanShirtPoint(-.011f, 1.080f), HumanShirtPoint(.011f, 1.080f), HumanShirtPoint(.011f, 1.402f), HumanShirtPoint(-.011f, 1.402f)
            }, cloth, uniform);
            HumanRibbon(body, "Placket stitch", new[] { HumanShirtPoint(.011f, 1.085f, .005f), HumanShirtPoint(.011f, 1.260f, .005f), HumanShirtPoint(.011f, 1.398f, .005f) }, .0018f, "cloth-seam", seam, Vector3.forward);
            for (int i = 0; i < 4; i++)
            {
                Vector3 button = HumanShirtPoint(0, 1.115f + i * .075f, .007f);
                HumanOval(body, "Shirt button", button, Vector3.right * .0035f, Vector3.up * .0035f, "button", new Color(.33f, .34f, .30f));
            }
            HumanRibbon(body, "Pocket stitching", new[] {
                new Vector3(-.140f, 1.355f, .137f), new Vector3(-.140f, 1.276f, .137f), new Vector3(-.090f, 1.262f, .140f), new Vector3(-.040f, 1.276f, .137f), new Vector3(-.040f, 1.355f, .137f)
            }, .0018f, "cloth-seam", seam, Vector3.forward);
            HumanPolygon(body, "Pocket flap", new[] {
                new Vector3(-.140f, 1.358f, .139f), new Vector3(-.040f, 1.358f, .139f), new Vector3(-.045f, 1.337f, .145f), new Vector3(-.090f, 1.331f, .146f), new Vector3(-.135f, 1.337f, .145f)
            }, cloth, uniform);
            HumanRibbon(body, "Shirt hem stitching", new[] {
                HumanShirtPoint(-.145f, .996f), HumanShirtPoint(-.075f, .990f), HumanShirtPoint(0, .988f), HumanShirtPoint(.075f, .990f), HumanShirtPoint(.145f, .996f)
            }, .003f, "cloth-seam", seam, Vector3.forward);
            HumanRibbon(body, "Back shoulder yoke", new[] {
                HumanShirtPoint(-.145f, 1.355f, .003f, true), HumanShirtPoint(-.065f, 1.375f, .003f, true), HumanShirtPoint(0, 1.377f, .003f, true),
                HumanShirtPoint(.065f, 1.375f, .003f, true), HumanShirtPoint(.145f, 1.355f, .003f, true)
            }, .0025f, "cloth-seam", seam, Vector3.back);
            Box(body, "Trouser fly", new Vector3(.015f, .91f, .123f), new Vector3(.005f, .105f, .005f), "cloth-seam", seam);
            if (!staff)
            {
                Box(body, "Resident cloth identification", new Vector3(-.082f, 1.315f, .149f), new Vector3(.094f, .040f, .006f), "resident-tag", new Color(.75f, .73f, .64f));
                for (int i = 0; i < 5; i++) Box(body, "Woven identification marks", new Vector3(-.116f + i * .017f, 1.315f, .154f), new Vector3(.007f, .018f, .002f), "ink", new Color(.19f, .20f, .18f));
                return;
            }
            if (role == "Officer")
            {
                Box(body, "Radio", new Vector3(-.165f, 1.325f, .115f), new Vector3(.065f, .108f, .043f), "rubber", new Color(.09f, .12f, .14f));
                Cylinder(body, "Radio antenna", new Vector3(-.165f, 1.425f, .115f), new Vector3(.009f, .045f, .009f), "rubber", Color.black);
                for (int i = 0; i < 3; i++) Box(body, "Radio speaker slot", new Vector3(-.165f, 1.330f + i * .012f, .139f), new Vector3(.039f, .003f, .002f), "button", seam);
                HumanRibbon(body, "Radio cable", new[] {
                    new Vector3(-.145f, 1.350f, .137f), HumanShirtPoint(-.115f, 1.230f, .010f), HumanShirtPoint(-.127f, 1.110f, .011f), new Vector3(-.155f, .994f, .126f)
                }, .005f, "rubber", Color.black, Vector3.forward);
                Box(body, "Duty pouch", new Vector3(.18f, .97f, .04f), new Vector3(.085f, .13f, .095f), "rubber", new Color(.10f, .13f, .15f));
                Box(body, "Pouch flap", new Vector3(.18f, 1.016f, .092f), new Vector3(.079f, .034f, .012f), "rubber", Color.black);
                Box(body, "Key clip", new Vector3(-.16f, .91f, .08f), new Vector3(.027f, .058f, .014f), "steel", hardware);
                for (int i = 0; i < 2; i++) Box(body, "Clipped key", new Vector3(-.168f + i * .015f, .867f, .09f), new Vector3(.008f, .044f, .005f), "steel", hardware);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(body, "Shoulder epaulet", new Vector3(side * .156f, 1.411f, 0), new Vector3(.082f, .019f, .105f), "uniform-epaulet", new Color(.12f, .19f, .24f));
                    for (int stripe = 0; stripe < 1 + seed % 3; stripe++)
                        Box(body, "Shoulder insignia", new Vector3(side * (.136f + stripe * .015f), 1.423f, .015f), new Vector3(.006f, .004f, .052f), "badge", new Color(.81f, .77f, .60f));
                }
            }
            else if (role == "Technician")
            {
                Box(body, "Service belt pouch", new Vector3(.17f, .96f, .07f), new Vector3(.115f, .15f, .08f), "rubber", new Color(.10f, .13f, .15f));
                for (int i = 0; i < 2; i++) Box(body, "Stowed service tool", new Vector3(.145f + i * .037f, .969f, .12f), new Vector3(.013f, .13f, .012f), "steel", hardware);
                Box(body, "Pen clip", new Vector3(-.060f, 1.364f, .150f), new Vector3(.008f, .041f, .007f), "rubber", Color.black);
            }
            else if (role == "Builder")
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    HumanRibbon(body, "Reflective vest shoulder strip", new[] {
                        HumanShirtPoint(side * .104f, 1.404f, .009f), HumanShirtPoint(side * .108f, 1.265f, .009f), HumanShirtPoint(side * .108f, 1.084f, .009f)
                    }, .026f, "reflective", new Color(.94f, .94f, .80f), Vector3.forward);
                    HumanRibbon(body, "Reflective vest back strip", new[] {
                        HumanShirtPoint(side * .104f, 1.404f, .009f, true), HumanShirtPoint(side * .108f, 1.265f, .009f, true), HumanShirtPoint(side * .108f, 1.084f, .009f, true)
                    }, .026f, "reflective", new Color(.94f, .94f, .80f), Vector3.back);
                }
            }
            else if (role == "Caseworker" || role == "Inspector")
            {
                HumanRibbon(body, "Staff pass lanyard", new[] {
                    new Vector3(-.045f, 1.453f, .071f), HumanShirtPoint(-.035f, 1.330f, .007f), HumanShirtPoint(0, 1.226f, .009f),
                    HumanShirtPoint(.035f, 1.330f, .007f), new Vector3(.045f, 1.453f, .071f)
                }, .006f, "rubber", Color.black, Vector3.forward);
                Box(body, "Staff pass", new Vector3(0, 1.195f, .143f), new Vector3(.049f, .071f, .005f), "resident-tag", new Color(.75f, .73f, .64f));
                Box(body, "Pass photograph", new Vector3(-.009f, 1.205f, .147f), new Vector3(.016f, .023f, .002f), "ink", new Color(.19f, .20f, .18f));
            }
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

    }
}
