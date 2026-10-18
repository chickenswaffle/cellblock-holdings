using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class FacilityView
    {
        readonly Dictionary<string, Transform> characterPhoneProps = new Dictionary<string, Transform>();
        void BuildPayphone(Transform root)
        {
            Color shell = new Color(.29f, .35f, .36f), dark = new Color(.055f, .07f, .075f), trim = new Color(.61f, .63f, .59f);
            Box(root, "Payphone pedestal", new Vector3(0, .63f, .08f), new Vector3(.36f, 1.26f, .19f), "steel", shell);
            Box(root, "Steel phone housing", new Vector3(0, 1.2f, 0), new Vector3(.60f, .80f, .30f), "steel", shell);
            Box(root, "Institutional call panel", new Vector3(.05f, 1.22f, -.163f), new Vector3(.30f, .50f, .025f), "phone-metal", trim);
            Box(root, "Call display", new Vector3(.055f, 1.37f, -.18f), new Vector3(.21f, .09f, .02f), "phone-display", new Color(.20f, .49f, .48f));
            for (int row = 0; row < 4; row++) for (int col = 0; col < 3; col++)
                Box(root, "Key", new Vector3(-.015f + col * .065f, 1.25f - row * .062f, -.185f), new Vector3(.042f, .036f, .018f), "phone-keys", dark);
            Box(root, "Receiver cradle", new Vector3(-.205f, 1.24f, -.20f), new Vector3(.09f, .31f, .075f), "phone-rubber", dark);
            Box(root, "Receiver earpiece", new Vector3(-.205f, 1.38f, -.22f), new Vector3(.13f, .075f, .095f), "phone-rubber", dark);
            Box(root, "Receiver mouthpiece", new Vector3(-.205f, 1.10f, -.22f), new Vector3(.13f, .075f, .095f), "phone-rubber", dark);
            for (int i = 0; i < 12; i++)
                Box(root, "Armored cord", new Vector3(-.20f + Mathf.Sin(i * .8f) * .012f, 1.03f - i * .025f, -.19f), new Vector3(.023f, .04f, .023f), "phone-cord", trim);
            Box(root, "Phone sign", new Vector3(0, 1.66f, .01f), new Vector3(.57f, .11f, .20f), "phone-sign", new Color(.18f, .34f, .36f));
            Box(root, "Hearing hood left", new Vector3(-.31f, 1.27f, -.05f), new Vector3(.025f, .70f, .36f), "steel", shell);
            Box(root, "Hearing hood right", new Vector3(.31f, 1.27f, -.05f), new Vector3(.025f, .70f, .36f), "steel", shell);
        }
        public void RefreshCharacterPhones(World world, string screenSubject)
        {
            foreach (var pair in characterPhoneProps) if (pair.Value != null) pair.Value.gameObject.SetActive(false);
            foreach (var pair in actors)
            {
                Actor actor = pair.Value; if (!actor.Seen || actor.Root == null) continue;
                string id = pair.Key, owner = id;
                Crew crew = world.Data.Crews.FirstOrDefault(c => id == c.Id + "/builder-0" || id == c.Id + "/builder-1");
                if (crew != null)
                {
                    owner = crew.Id;
                    string actorHolding = world.CharacterControlActive && world.CharacterId.StartsWith(crew.Id + "/builder-") ? world.CharacterId : crew.Id + "/builder-0";
                    if (id != actorHolding) continue;
                }
                if (!world.CharacterUsingPhone(owner) && owner != screenSubject) continue;
                Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == owner);
                StaffMember staff = world.Data.Staff.FirstOrDefault(s => s.Id == owner);
                if (world.CharacterHasPriorityIncident(owner)) continue;
                var access = world.PhoneAccess(owner); if (!access.CanOpen) continue;
                Transform prop;
                if (!characterPhoneProps.TryGetValue(id, out prop) || prop == null)
                {
                    prop = new GameObject("Actual character phone").transform; prop.SetParent(actor.Hands[0], false);
                    Box(prop, "Handset", new Vector3(0, 0, .035f), new Vector3(.085f, .16f, .025f), "phone-rubber", new Color(.055f, .07f, .075f));
                    Box(prop, "Screen", new Vector3(0, .01f, .020f), new Vector3(.067f, .12f, .005f), "phone-display", new Color(.20f, .49f, .48f));
                    characterPhoneProps[id] = prop;
                }
                prop.gameObject.SetActive(true); actor.PoseArms = true;
                float blend = world.Data.Speed == 0 ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
                bool calling = world.CharacterUsingPhone(owner);
                actor.Limbs[0].localRotation = Quaternion.Slerp(actor.Limbs[0].localRotation, Quaternion.Euler(calling ? -42 : -62, -15, -12), blend);
                actor.Forearms[0].localRotation = Quaternion.Slerp(actor.Forearms[0].localRotation, Quaternion.Euler(calling ? -145 : -75, 0, 0), blend);
            }
            foreach (string id in characterPhoneProps.Keys.Where(id => characterPhoneProps[id] == null || !actors.ContainsKey(id)).ToArray()) characterPhoneProps.Remove(id);
        }
    }
}
