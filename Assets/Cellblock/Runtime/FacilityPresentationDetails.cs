using UnityEngine;

namespace Cellblock.Unity
{
    // Small authored craft details, built with the installed furnishing and baked by the existing material pass.
    // They neither change its footprint nor claim service, power, occupancy, inventory or upgrade state.
    public sealed partial class FacilityView
    {
        const string CraftMetal = "presentation-brushed-metal", CraftPaper = "presentation-paper", CraftInk = "presentation-ink";
        static readonly Color CraftMetalColor = new Color(.52f, .58f, .57f);
        static readonly Color CraftPaperColor = new Color(.81f, .80f, .73f);
        static readonly Color CraftInkColor = new Color(.12f, .17f, .18f);
        bool craftMaterialsReady;

        void PrepareCraftMaterials()
        {
            if (craftMaterialsReady) return;
            craftMaterialsReady = true;
            Material metal = Material(CraftMetal, CraftMetalColor);
            if (metal.HasProperty("_Smoothness")) metal.SetFloat("_Smoothness", .58f);
            if (metal.HasProperty("_Metallic")) metal.SetFloat("_Metallic", .62f);
            Material paper = Material(CraftPaper, CraftPaperColor);
            if (paper.HasProperty("_Smoothness")) paper.SetFloat("_Smoothness", .08f);
            Material ink = Material(CraftInk, CraftInkColor);
            if (ink.HasProperty("_Smoothness")) ink.SetFloat("_Smoothness", .16f);
        }

        // Hook from FixtureView after the base fixture has been built and before the existing scene Combine.
        // Call for palette == null (or parent == structures for strictly installed fixtures), never a plan ghost.
        void AddPresentationDetail(Transform fixtureRoot, string kind)
        {
            if (fixtureRoot == null || fixtureRoot.Find("Presentation craft details") != null) return;
            if (kind != "Bed" && kind != "Desk" && kind != "Table" && kind != "ServingCounter" && kind != "Bench" &&
                kind != "Chair" && kind != "CCTV" && kind != "Floodlight" && kind != "Payphone") return;
            PrepareCraftMaterials();
            Transform detail = new GameObject("Presentation craft details").transform;
            detail.SetParent(fixtureRoot, false);
            switch (kind)
            {
                case "Bed": BedCraft(detail); break;
                case "Desk": DeskCraft(detail); break;
                case "Table": DiningCraft(detail); break;
                case "ServingCounter": ServingCraft(detail); break;
                case "Bench": SeatCraft(detail, true); break;
                case "Chair": SeatCraft(detail, false); break;
                case "CCTV": CameraCraft(detail); break;
                case "Floodlight": FloodlightCraft(detail); break;
                case "Payphone": PayphoneCraft(detail); break;
            }
        }

        void CraftStud(Transform parent, Vector3 at, float diameter = .028f, bool vertical = false)
        {
            Transform stud = Cylinder(parent, "Captive metal fixing", at, new Vector3(diameter, .0035f, diameter), CraftMetal, CraftMetalColor);
            if (!vertical) stud.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void BedCraft(Transform root)
        {
            // Folded hems remain on the existing blanket instead of widening the bed or raising its sleeping plane.
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "Bound blanket hem", new Vector3(side * .416f, .611f, .30f), new Vector3(.018f, .010f, 1.12f), "blanket-fold", new Color(.42f, .52f, .53f));
                Box(root, "Blanket tuck", new Vector3(side * .431f, .548f, .31f), new Vector3(.012f, .08f, 1.10f), "blanket", new Color(.34f, .44f, .46f));
                foreach (float end in new[] { -.95f, .95f })
                    CraftStud(root, new Vector3(side * .31f, .50f, end + (end < 0 ? -.022f : .022f)), .025f);
            }
            Box(root, "Storage lid reveal", new Vector3(0, .225f, .524f), new Vector3(.59f, .009f, .005f), CraftInk, CraftInkColor);
            Box(root, "Recessed storage label holder", new Vector3(-.19f, .13f, .525f), new Vector3(.13f, .052f, .007f), CraftMetal, CraftMetalColor);
            Box(root, "Storage label insert", new Vector3(-.19f, .13f, .530f), new Vector3(.105f, .033f, .003f), CraftPaper, CraftPaperColor);
        }

        void DeskCraft(Transform root)
        {
            // Familiar controls and a legible paper stack make the existing casework station read as a workplace.
            for (int row = 0; row < 3; row++) for (int key = 0; key < 7; key++)
                Box(root, "Keyboard keycap", new Vector3(-.375f + key * .053f, .881f, -.18f + row * .034f), new Vector3(.042f, .006f, .025f), CraftInk, CraftInkColor);
            Box(root, "Keyboard space bar", new Vector3(-.20f, .881f, -.219f), new Vector3(.17f, .006f, .021f), CraftInk, CraftInkColor);
            for (int line = 0; line < 4; line++)
                Box(root, "Printed casework form line", new Vector3(.525f, .872f, -.19f + line * .045f), new Vector3(line == 0 ? .17f : .19f, .002f, line == 0 ? .012f : .004f), CraftInk, CraftInkColor);
            Box(root, "Form margin", new Vector3(.442f, .872f, -.12f), new Vector3(.003f, .002f, .20f), CraftInk, CraftInkColor);
            Box(root, "Paper stack edge", new Vector3(.54f, .860f, -.303f), new Vector3(.245f, .004f, .003f), CraftPaper, CraftPaperColor);
            Transform pen = Cylinder(root, "Desk pen", new Vector3(.714f, .847f, -.12f), new Vector3(.010f, .084f, .010f), CraftInk, CraftInkColor);
            pen.localRotation = Quaternion.Euler(90, 0, -8);
            for (int vent = 0; vent < 5; vent++)
                Box(root, "Monitor rear ventilation", new Vector3(-.36f + vent * .08f, 1.18f, .315f), new Vector3(.012f, .105f, .004f), CraftInk, CraftInkColor);
            Box(root, "Monitor underside trim", new Vector3(-.20f, .982f, .263f), new Vector3(.48f, .012f, .011f), CraftMetal, CraftMetalColor);
            Box(root, "Tidy monitor cable", new Vector3(-.23f, .93f, .35f), new Vector3(.012f, .17f, .012f), CraftInk, CraftInkColor);
            foreach (float y in new[] { .22f, .42f, .62f })
            {
                Box(root, "Drawer separation", new Vector3(.56f, y - .08f, -.415f), new Vector3(.47f, .007f, .004f), CraftInk, CraftInkColor);
                CraftStud(root, new Vector3(.43f, y, -.436f), .019f);
                CraftStud(root, new Vector3(.69f, y, -.436f), .019f);
            }
        }

        void DiningCraft(Transform root)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 tray = new Vector3(side * .47f, .869f, side * .19f);
                // A stamped compartment and rolled rim refine the tray without inventing a served meal.
                foreach (float edge in new[] { -1f, 1f })
                {
                    Box(root, "Rolled tray rim", tray + new Vector3(0, 0, edge * .139f), new Vector3(.39f, .008f, .010f), CraftMetal, CraftMetalColor);
                    Box(root, "Rolled tray end", tray + new Vector3(edge * .196f, 0, 0), new Vector3(.010f, .008f, .278f), CraftMetal, CraftMetalColor);
                }
                Box(root, "Tray compartment ridge", tray + new Vector3(.063f, 0, 0), new Vector3(.009f, .008f, .267f), CraftMetal, CraftMetalColor);
                Box(root, "Tray small compartment", tray + new Vector3(.127f, 0, 0), new Vector3(.122f, .008f, .008f), CraftMetal, CraftMetalColor);
                // PrimitiveType.Cylinder is two units high: the existing .065 Y scale puts its rim at .975.
                Cylinder(root, "Hollow cup inset", new Vector3(side * .69f, .9765f, -side * .23f), new Vector3(.061f, .0015f, .061f), CraftInk, CraftInkColor);
                foreach (float anchor in new[] { -.65f, .65f })
                    CraftStud(root, new Vector3(anchor, .493f, side * .9f), .032f, true);
            }
        }

        void SeatCraft(Transform root, bool bench)
        {
            float inset = bench ? .70f : .22f;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float y in new[] { .66f, .80f }) CraftStud(root, new Vector3(side * inset, y, .216f), .032f);
                foreach (float z in new[] { -.15f, .15f }) CraftStud(root, new Vector3(side * inset, .487f, z), .025f, true);
                Box(root, "Welded seat angle", new Vector3(side * inset, .39f, .23f), new Vector3(.09f, .08f, .025f), CraftMetal, CraftMetalColor);
            }
        }

        void ServingCraft(Transform root)
        {
            // Hardware remains on the existing cabinet and sneeze guard: the serving access side stays clear.
            Box(root, "Brushed counter edge", new Vector3(0, .930f, -.421f), new Vector3(1.94f, .025f, .009f), CraftMetal, CraftMetalColor);
            for (int door = 0; door < 3; door++)
            {
                float x = -.61f + door * .61f;
                for (int vent = 0; vent < 3; vent++)
                    Box(root, "Cabinet ventilation slot", new Vector3(x, .215f + vent * .035f, -.411f), new Vector3(.31f, .009f, .005f), CraftInk, CraftInkColor);
                foreach (float y in new[] { .568f, .672f }) CraftStud(root, new Vector3(x + .18f, y, -.425f), .023f);
                Box(root, "Cabinet toe-kick reveal", new Vector3(x, .092f, -.400f), new Vector3(.55f, .021f, .004f), CraftInk, CraftInkColor);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                CraftStud(root, new Vector3(side * .90f, 1.09f, .267f), .027f);
                CraftStud(root, new Vector3(side * .90f, 1.43f, .267f), .027f);
                Box(root, "Counter corner join", new Vector3(side * .97f, .91f, -.13f), new Vector3(.014f, .05f, .46f), CraftMetal, CraftMetalColor);
            }
        }

        void CameraCraft(Transform root)
        {
            Box(root, "Camera weather hood", new Vector3(0, 2.586f, .15f), new Vector3(.27f, .017f, .46f), CraftMetal, CraftMetalColor);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(root, "Camera hood folded edge", new Vector3(side * .129f, 2.56f, .15f), new Vector3(.012f, .055f, .45f), CraftMetal, CraftMetalColor);
                Box(root, "Camera trunnion bracket", new Vector3(side * .137f, 2.405f, .09f), new Vector3(.026f, .10f, .16f), "steel", Steel);
                foreach (float z in new[] { -.115f, .115f }) CraftStud(root, new Vector3(side * .115f, .087f, z), .034f, true);
            }
            Cylinder(root, "Camera tilt pivot", new Vector3(0, 2.355f, .09f), new Vector3(.15f, .055f, .15f), "steel", Steel);
            Transform bezel = Cylinder(root, "Optical lens bezel", new Vector3(0, 2.48f, .391f), new Vector3(.126f, .003f, .126f), CraftMetal, CraftMetalColor);
            bezel.localRotation = Quaternion.Euler(90, 0, 0);
            Transform glass = Cylinder(root, "Recessed optical element", new Vector3(0, 2.48f, .398f), new Vector3(.096f, .001f, .096f), "screen", new Color(.045f, .08f, .09f));
            glass.localRotation = Quaternion.Euler(90, 0, 0);
            Box(root, "Protected camera conduit", new Vector3(0, 1.29f, -.052f), new Vector3(.018f, 2.30f, .018f), CraftInk, CraftInkColor);
            Box(root, "Camera conduit saddle", new Vector3(0, 1.80f, -.059f), new Vector3(.080f, .025f, .018f), CraftMetal, CraftMetalColor);
        }

        void FloodlightCraft(Transform root)
        {
            foreach (float x in new[] { -.125f, .125f }) foreach (float z in new[] { -.125f, .125f })
                CraftStud(root, new Vector3(x, .207f, z), .035f, true);
            Cylinder(root, "Mast foundation collar", new Vector3(0, .238f, 0), new Vector3(.15f, .025f, .15f), CraftMetal, CraftMetalColor);
            Box(root, "Mast service hatch", new Vector3(0, .63f, -.048f), new Vector3(.071f, .24f, .012f), CraftMetal, CraftMetalColor);
            foreach (float side in new[] { -1f, 1f })
            {
                // Use a unit-scale mount: attaching metre-sized details to the existing scaled cube would shrink them twice.
                Transform lamp = new GameObject("Floodlight housing craft").transform; lamp.SetParent(root, false);
                lamp.localPosition = new Vector3(side * .18f, 3.05f, .04f); lamp.localRotation = Quaternion.Euler(25, side * 15, 0);
                for (int fin = 0; fin < 3; fin++)
                    Box(lamp, "Heat sink fin", new Vector3(0, -.06f + fin * .06f, -.093f), new Vector3(.25f, .018f, .035f), "steel", Steel);
                foreach (float x in new[] { -.135f, .135f })
                    Box(lamp, "Lamp bezel side", new Vector3(x, 0, .087f), new Vector3(.014f, .19f, .012f), CraftMetal, CraftMetalColor);
                foreach (float y in new[] { -.09f, .09f })
                    Box(lamp, "Lamp bezel edge", new Vector3(0, y, .087f), new Vector3(.27f, .014f, .012f), CraftMetal, CraftMetalColor);
            }
        }

        void PayphoneCraft(Transform root)
        {
            // The actual handset, keypad, cord, display and service state remain owned by BuildPayphone / CharacterPhone.
            foreach (float side in new[] { -1f, 1f }) foreach (float y in new[] { .87f, 1.53f })
                CraftStud(root, new Vector3(side * .267f, y, -.157f), .027f);
            Box(root, "Phone service plate", new Vector3(.05f, .925f, -.154f), new Vector3(.19f, .052f, .008f), CraftMetal, CraftMetalColor);
            Box(root, "Service plate engraved rule", new Vector3(.05f, .932f, -.159f), new Vector3(.12f, .006f, .002f), CraftInk, CraftInkColor);
            Box(root, "Service plate engraved line", new Vector3(.03f, .914f, -.159f), new Vector3(.08f, .004f, .002f), CraftInk, CraftInkColor);
            foreach (float y in new[] { .42f, .69f })
                Box(root, "Pedestal folded seam", new Vector3(.183f, y, .08f), new Vector3(.006f, .014f, .16f), CraftMetal, CraftMetalColor);
        }
    }
}
