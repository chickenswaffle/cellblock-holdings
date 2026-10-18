using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement incidentCard;
        Label incidentKicker, incidentTitle, incidentDetail;
        Button incidentWatch, incidentClock, incidentAlarm;
        StatMeter incidentProgress;
        bool incidentVisible;
        void BuildIncidentHud()
        {
            root.styleSheets.Add(Resources.Load<StyleSheet>("Incidents"));
            incidentCard = Panel("incident-card"); incidentCard.style.display = DisplayStyle.None;
            incidentKicker = Text("LIVE INCIDENT", "incident-kicker"); incidentTitle = Text("", "incident-title"); incidentDetail = Text("", "incident-detail");
            incidentCard.Add(incidentKicker); incidentCard.Add(incidentTitle); incidentCard.Add(incidentDetail);
            incidentProgress = new StatMeter("RESPONSE"); incidentCard.Add(incidentProgress);
            var actions = Element("incident-actions"); incidentCard.Add(actions);
            incidentWatch = Action("Watch action", () => { if (game.FollowingIncident) game.ReleaseIncidentCamera(); else { inspectorOpen = false; cameraOpen = false; game.FollowIncident(); Layout(); } }, "primary"); actions.Add(incidentWatch);
            incidentClock = Action("Pause", () => { if (game.World != null) game.World.Data.Speed = game.World.Data.Speed == 0 ? 1 : 0; Refresh(true); }, "quiet"); actions.Add(incidentClock);
            incidentAlarm = Action("Raise alarm", () => { if (game.World?.Data.Operations.Alarm == true) ShowControl(); else game.World?.SetAlarm(true); Refresh(true); }, "danger"); actions.Add(incidentAlarm);
        }
        public void ShowIncidentWorld() { CloseOverlays(); inspectorOpen = false; cameraOpen = false; menu.style.display = DisplayStyle.None; Layout(); }
        void RefreshIncidentHud(World world)
        {
            if (incidentCard == null) return;
            bool escape = world.Escape.Phase != "None" || world.IncidentReport.Phase == "Radioing control" || world.IncidentReport.Until > world.Data.Minute;
            bool fight = world.Fight.Active || world.Fight.Until > world.Data.Minute;
            incidentVisible = escape || fight;
            if (!incidentVisible) { incidentCard.style.display = DisplayStyle.None; return; }
            incidentKicker.text = (game.IncidentDemo ? "LIVE DEMONSTRATION" : "LIVE INCIDENT") + (world.Data.Speed == 0 ? " / PAUSED — RESUME TO CONTINUE" : " / " + world.Data.Speed + "×");
            if (escape)
            {
                var report = world.IncidentReport; Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == (world.Escape.Phase != "None" ? world.Escape.ResidentId : report.Resident));
                StaffMember observer = world.Data.Staff.FirstOrDefault(s => s.Id == report.Guard);
                string controls = "Alarm " + (world.Data.Operations.Alarm ? "on" : "off") + ", gate " + (world.Data.Operations.GateOpen ? "open" : "closed") + ", lockdown " + (world.Data.Operations.Lockdown ? "active" : "clear") + ".";
                incidentTitle.text = "ESCAPE / " + (resident == null ? "Custody response" : resident.Name);
                incidentDetail.text = report.Phase == "Radioing control" ? (observer?.Name ?? "Officer") + " is transmitting. Lockdown follows the completed radio call." :
                    report.Outcome != "" && world.Escape.Phase == "None" ? report.Outcome + ". " + (report.Phase == "Broadcast" ? controls + " Review restrictions in Control." : "No control broadcast was completed. Review the headcount and perimeter in Control.") :
                    report.Phase == "Broadcast" ? "RADIO RECEIVED / " + controls + " Officers must reach the resident in person." :
                    world.Escape.Detected ? "Detected by " + world.Escape.DetectedBy + ". Waiting for an observing guard or camera operator to report." : "The resident is moving toward the vehicle gate. Guards need sight or camera coverage to notice.";
                float value = report.Phase == "Broadcast" ? 100 : report.Phase == "Radioing control" ? 100f * (world.Data.Minute - report.PhaseMinute) / World.RadioMinutes : 0;
                incidentProgress.SetScore(report.Phase == "Radioing control" ? "RADIO TRANSMISSION" : "CONTROL NOTIFICATION", value, MeterMeaning.Neutral, report.Phase == "Broadcast" ? "RECEIVED" : Mathf.RoundToInt(value) + "%");
                // The physical struggle leads the card while it lasts (EscapeStruggle.cs).
                string struggle = world.Escape.Phase != "None" ? world.EscapeStruggleDetail() : "";
                if (struggle != "")
                {
                    incidentDetail.text = struggle + (report.Phase == "Radioing control" ? " " + (observer?.Name ?? "An officer") + " is still on the radio." : "");
                    EscapeStruggle hold = world.Struggle;
                    if (hold.Active)
                        incidentProgress.SetScore("STRUGGLE / " + hold.Phase.ToUpperInvariant(), hold.Phase == "Grappling" ? 35 : hold.Phase == "Surrendered" ? 55 : hold.Phase == "Down" ? 70 : 88,
                            MeterMeaning.Neutral, hold.Phase == "Grappling" ? "EXCHANGE " + (hold.Exchanges + 1) : hold.Phase.ToUpperInvariant());
                }
            }
            else
            {
                var scene = world.Fight; Resident a = world.Data.Residents.FirstOrDefault(r => r.Id == scene.A), b = world.Data.Residents.FirstOrDefault(r => r.Id == scene.B);
                StaffMember guard = world.Data.Staff.FirstOrDefault(s => s.Id == scene.Responder);
                incidentTitle.text = (a?.Name ?? "Resident") + " / " + (b?.Name ?? "Resident");
                incidentDetail.text = scene.Phase == "Resolved" ? scene.Outcome : scene.Phase == "Converging" ? "A confrontation is building. Both residents are walking to the meeting point." :
                    scene.Phase == "Separating" ? (guard?.Name ?? "Officer") + " reached them and is separating the pair." :
                    scene.Phase == "Scuffle" ? "A scuffle has begun. " + (guard == null ? "No free officer has reached the scene." : guard.Name + " is approaching on foot.") :
                    "Words are turning hostile. " + (guard == null ? "No available officer has noticed yet." : guard.Name + " is responding.");
                float value = scene.Phase == "Converging" ? 10 : scene.Phase == "Tension" ? 30 : scene.Phase == "Scuffle" ? 55 : scene.Phase == "Separating" ? 75 : 100;
                incidentProgress.SetScore("SCENE / " + scene.Phase.ToUpperInvariant(), value, MeterMeaning.Neutral, scene.Phase.ToUpperInvariant());
            }
            incidentWatch.text = game.FollowingIncident ? "Release camera" : "Watch action"; incidentClock.text = world.Data.Speed == 0 ? "Resume 1×" : "Pause";
            incidentAlarm.text = world.Data.Operations.Alarm ? "Control panel" : "Raise alarm";
        }
        void LayoutIncidentHud(float left, float right, float top, float bottom, bool blocked)
        {
            if (incidentCard == null) return;
            incidentCard.style.display = incidentVisible && !blocked ? DisplayStyle.Flex : DisplayStyle.None;
            incidentCard.style.left = left; incidentCard.style.right = StyleKeyword.Auto; incidentCard.style.top = top + 88;
            incidentCard.style.maxHeight = StyleKeyword.Null;
            float available = root.resolvedStyle.width - left - right - (inspectorOpen ? inspector.resolvedStyle.width + 18 : cameraOpen ? 350 : 0);
            incidentCard.style.width = Mathf.Min(530, Mathf.Max(250, available));
            if (!incidentVisible || blocked) return;
            commandOverview.style.display = DisplayStyle.None; eventFeed.style.display = DisplayStyle.None;
            if (!compact || !game.FollowingIncident) return;
            // Watching a scene on a landscape phone leaves a deliberate world rectangle beside
            // the card. Run after character layout, which restores the ordinary header each pass.
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            float cardWidth = Mathf.Min(width - left - right, Mathf.Clamp((width - left - right) * .4f, 300, 380));
            float worldWidth = Mathf.Max(0, width - left - right - cardWidth - 12);
            incidentCard.style.left = StyleKeyword.Auto; incidentCard.style.right = right;
            incidentCard.style.top = top; incidentCard.style.width = cardWidth;
            incidentCard.style.maxHeight = height - top - bottom;
            navigation.style.display = DisplayStyle.None;
            header.style.right = right + cardWidth + 12;
            ReduceCharacterHeader(worldWidth);
            status.style.right = right + cardWidth + 12; status.style.bottom = bottom;
            status.style.maxWidth = worldWidth;
            bool covered = worldWidth < 230;
            if (covered) { header.style.display = DisplayStyle.None; status.style.display = DisplayStyle.None; }
            float statusHeight = status.resolvedStyle.height;
            if (float.IsNaN(statusHeight) || statusHeight <= 0) statusHeight = 48;
            float worldTop = top + (covered ? 0 : 88), worldBottom = bottom + (covered ? 0 : statusHeight + 12);
            WorldViewCenter = new Vector2((left + worldWidth * .5f) / width,
                (worldBottom + Mathf.Max(0, height - worldTop - worldBottom) * .5f) / height);
        }
#if UNITY_EDITOR
        public string PreviewIncidentDescription { get { return incidentDetail == null ? "" : incidentDetail.text; } }
        public bool PreviewIncidentPanelVisible { get { return incidentCard != null && incidentCard.resolvedStyle.display != DisplayStyle.None; } }
        public bool PreviewIncidentPanelFits { get { return incidentCard.worldBound.xMin >= root.worldBound.xMin && incidentCard.worldBound.xMax <= root.worldBound.xMax && incidentCard.worldBound.yMax <= root.worldBound.yMax - (compact && game.FollowingIncident ? 10 : 68) && incidentWatch.worldBound.height >= 43 && incidentClock.worldBound.height >= 43 && incidentAlarm.worldBound.height >= 43; } }
        public bool PreviewIncidentActorVisible(string id)
        {
            Vector3 actor; var view = game.GetComponentInChildren<FacilityView>();
            if (view == null || !view.TryGetPersonPosition(id, out actor)) return false;
            // Check feet and head against both the screen and the actual card rectangle.
            foreach (Vector3 point in new[] { actor, actor + Vector3.up * 1.9f })
            {
                Vector3 screen = game.ArtCamera.WorldToScreenPoint(point);
                Vector2 panel = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
                if (screen.z <= 0 || !root.worldBound.Contains(panel) || incidentCard.worldBound.Contains(panel)) return false;
            }
            return true;
        }
        public bool PreviewIncidentDockVisible { get { return navigation.resolvedStyle.display != DisplayStyle.None; } }
#endif
    }
}
