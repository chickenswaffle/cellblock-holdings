using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Presentation only. The existing Home buttons keep their save identity, metadata and callbacks.
    // The hero and action stack are regrouped once per Home tree, never rebuilt on refresh.
    public sealed partial class GameHud
    {
        bool homePresentationBuilt;
        VisualElement homePresentationColumn, homePresentationHero, homePresentationActions;
        Label homePresentationTitle, homePresentationTagline, homePresentationAssurance;
        HomeSceneVeil homePresentationVeil;

        void BuildHomePresentation()
        {
            if (homePresentationBuilt || root == null) return;
            homePresentationBuilt = true;
            StyleSheet style = Resources.Load<StyleSheet>("HomePresentation");
            if (style != null) root.styleSheets.Add(style);
            root.AddToClassList("home-presentation");
            homePresentationVeil = new HomeSceneVeil();
            homePresentationVeil.AddToClassList("home-scene-veil");
        }

        // Call after LayoutFrontend so this also runs when no World exists on the first-use screen.
        void RefreshHomePresentation(World world)
        {
            if (root == null || menu == null) return;
            if (!homePresentationBuilt) BuildHomePresentation();
            if (menu.style.display.value == DisplayStyle.None || homeContent == null || homeContent.parent != menu) return;
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            bool wideShort = width >= 680 && height < 560;
            bool narrowShort = width < 680 && height < 480;
            root.EnableInClassList("home-presentation-wide-short", wideShort);
            root.EnableInClassList("home-presentation-narrow-short", narrowShort);
            root.EnableInClassList("home-presentation-tall-narrow", width < 700 && height >= 560);

            if (homePresentationVeil.parent != menu)
                menu.Insert(menu.IndexOf(homeContent), homePresentationVeil);

            VisualElement column = homeContent.Q<VisualElement>(className: "home-column");
            menu.EnableInClassList("home-presentation-library", column == null);
            if (column == null) return;
            if (homePresentationColumn != column) InstallHomePresentation(column);
            homePresentationTitle.text = narrowShort ? "CELLBLOCK HOLDINGS" : "CELLBLOCK\nHOLDINGS";
            homePresentationTagline.text = narrowShort ? "Build the institution. Run the company." : "Build the institution.\nRun the company.";
            int interval = game.Preferences == null ? 30 : game.Preferences.AutosaveSeconds;
            homePresentationAssurance.text = "AUTOSAVE EVERY " + (interval < 60 ? interval + " SEC" : interval / 60 + " MIN") + "  /  LOADS PAUSED";
        }

        void InstallHomePresentation(VisualElement column)
        {
            homePresentationColumn = column;
            homePresentationHero = Element("home-presentation-hero");
            homePresentationActions = Element("home-presentation-actions");
            // Preserve the actual button objects and each metadata label from ShowHomeMenu.
            VisualElement[] original = column.Children().ToArray();
            foreach (VisualElement child in original)
            {
                child.RemoveFromHierarchy();
                if (child.ClassListContains("home-eyebrow") || child.ClassListContains("home-title") || child.ClassListContains("home-tagline")) homePresentationHero.Add(child);
                else homePresentationActions.Add(child);
            }
            column.Add(homePresentationHero); column.Add(homePresentationActions);
            homePresentationTitle = homePresentationHero.Q<Label>(className: "home-title");
            homePresentationTagline = homePresentationHero.Q<Label>(className: "home-tagline");
            Label eyebrow = homePresentationHero.Q<Label>(className: "home-eyebrow");
            if (eyebrow != null) eyebrow.text = "PRISON MANAGEMENT / YOUR HOLDING";
            homePresentationAssurance = homePresentationActions.Q<Label>(className: "home-assurance");
            Button resume = homePresentationActions.Q<Button>("home-continue");
            if (resume != null)
            {
                var arrow = Text("→", "home-presentation-arrow"); arrow.pickingMode = PickingMode.Ignore;
                Label kicker = resume.Q<Label>(className: "home-action-kicker");
                arrow.style.display = kicker != null && kicker.text == "YOUR FIRST HOLDING" ? DisplayStyle.None : DisplayStyle.Flex;
                resume.Add(arrow);
            }
            VisualElement mast = homeContent.Q<VisualElement>(className: "home-mast");
            if (mast != null)
            {
                var brand = Element("home-presentation-brand");
                var seal = Text("CH", "home-presentation-seal"); brand.Add(seal);
                Label wordmark = mast.Q<Label>(className: "home-wordmark");
                if (wordmark != null) { wordmark.RemoveFromHierarchy(); wordmark.text = "CELLBLOCK HOLDINGS"; brand.Add(wordmark); }
                mast.Insert(0, brand);
            }
            Label foot = homeContent.Q<Label>(className: "home-foot-line");
            if (foot != null) foot.text = "SPACES SHAPE THE DAY. PEOPLE SHAPE THE STORY.";
            Label credit = homeContent.Q<Label>(className: "home-art-credit");
            if (credit != null) credit.text = "CELLBLOCK HOLDINGS / FACILITY STUDY";
        }

        // A restrained ink veil keeps the real game-rendered image visible and the copy readable.
        // Uses UI mesh gradients rather than generated imagery, post-processing or a second camera.
        sealed class HomeSceneVeil : VisualElement
        {
            internal HomeSceneVeil() { pickingMode = PickingMode.Ignore; generateVisualContent += Paint; }
            void Paint(MeshGenerationContext context)
            {
                Rect r = contentRect; if (r.width <= 0 || r.height <= 0) return;
                MeshWriteData mesh = context.Allocate(12, 18);
                Color ink = new Color(.018f, .043f, .058f, .94f), clear = new Color(.018f, .043f, .058f, 0);
                Quad(mesh, new Rect(r.xMin, r.yMin, r.width * .84f, r.height), ink, clear, ink, clear, 0);
                Color top = new Color(.008f, .025f, .037f, .58f);
                Quad(mesh, new Rect(r.xMin, r.yMin, r.width, Mathf.Min(140, r.height * .3f)), top, top, clear, clear, 4);
                Color bottom = new Color(.008f, .025f, .037f, .70f);
                float depth = Mathf.Min(120, r.height * .25f);
                Quad(mesh, new Rect(r.xMin, r.yMax - depth, r.width, depth), clear, clear, bottom, bottom, 8);
            }
            static void Quad(MeshWriteData mesh, Rect r, Color topLeft, Color topRight, Color bottomLeft, Color bottomRight, ushort start)
            {
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMin, Vertex.nearZ), tint = topLeft });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMin, Vertex.nearZ), tint = topRight });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMax, r.yMax, Vertex.nearZ), tint = bottomRight });
                mesh.SetNextVertex(new Vertex { position = new Vector3(r.xMin, r.yMax, Vertex.nearZ), tint = bottomLeft });
                mesh.SetNextIndex(start); mesh.SetNextIndex((ushort)(start + 1)); mesh.SetNextIndex((ushort)(start + 2));
                mesh.SetNextIndex((ushort)(start + 2)); mesh.SetNextIndex((ushort)(start + 3)); mesh.SetNextIndex(start);
            }
        }
#if UNITY_EDITOR
        public bool PreviewHomePresentationReady { get { return homePresentationBuilt && homePresentationColumn != null && homePresentationColumn.panel != null && homePresentationVeil != null && homePresentationVeil.parent == menu; } }
        public string PreviewHomePresentationLayout { get { return "hero=" + homePresentationHero?.worldBound + "; actions=" + homePresentationActions?.worldBound + "; title=" + homePresentationTitle?.worldBound; } }
#endif
    }
}
