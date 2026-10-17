using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement eventFeed,eventSheet,eventDossier;
        ScrollView eventScroll;
        Label eventFeedTitle,eventFeedSummary,eventDossierText;
        string transferConfirmation="";
        World eventWorld;
        void BuildFacilityEvents()
        {
            var style=Resources.Load<StyleSheet>("FacilityEvents");if(style!=null)root.styleSheets.Add(style);
            eventFeed=Panel("event-feed");eventFeedTitle=Text("FACILITY JOURNAL","event-kicker");eventFeed.Add(eventFeedTitle);
            eventFeedSummary=Text("","event-summary");eventFeed.Add(eventFeedSummary);
            eventFeed.Add(Action("Goals & incidents",ShowFacilityEvents,"quiet"));
            eventDossier=Element("event-dossier");eventDossierText=Text("","body");eventDossier.Add(eventDossierText);
            eventDossier.Add(Action("Open case & facility journal",ShowFacilityEvents,"quiet"));personCard.Add(eventDossier);
            eventSheet=Panel("event-overlay");eventSheet.style.display=DisplayStyle.None;
            var card=Element("event-sheet");eventSheet.Add(card);
            var head=Element("event-head");head.Add(Text("FACILITY JOURNAL","card-title"));head.Add(Action("Close",()=>CloseFacilityEventSheet(),"quiet"));card.Add(head);
            card.Add(Text("Time is held while this journal is open. Decisions take effect immediately; deadlines use the game clock.","muted"));
            eventScroll=new ScrollView(ScrollViewMode.Vertical);eventScroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;eventScroll.AddToClassList("event-scroll");card.Add(eventScroll);
        }
        public void ShowFacilityEvents()
        {
            if(game.World==null||game.Draft!=null){game.Message("Finish the current quote before opening the journal.");return;}
            CloseSheet();eventWorld=game.World;facilityEventModalOpen=true;transferConfirmation="";RebuildFacilityJournal();eventSheet.style.display=DisplayStyle.Flex;eventSheet.BringToFront();
        }
        partial void CloseFacilityEventSheet(){facilityEventModalOpen=false;if(eventSheet!=null)eventSheet.style.display=DisplayStyle.None;}
        void RefreshFacilityEvents(World world,Resident resident)
        {
            world.EnsureFacilityStories();
            if(facilityEventModalOpen&&!ReferenceEquals(eventWorld,world))CloseFacilityEventSheet();
            bool hidden=guideOpen||(menu!=null&&menu.style.display.value!=DisplayStyle.None)||game.Draft!=null;
            eventFeed.style.display=hidden?DisplayStyle.None:DisplayStyle.Flex;
            eventFeed.style.width=compact?250:320;
            var incidents=world.Stories.Incidents.Where(e=>e.Phase!="Resolved").OrderBy(e=>e.StartMinute).ToList();
            FacilityIncident active=incidents.FirstOrDefault(e=>e.StartMinute<=world.Data.Minute)??incidents.FirstOrDefault();
            FacilityGoal goal=world.Stories.Goals.FirstOrDefault(g=>g.CompletedMinute<0);
            eventFeedTitle.text=active==null?"FACILITY JOURNAL":active.Kind.ToUpperInvariant()+" · "+(active.StartMinute>world.Data.Minute?"UPCOMING":active.Phase.ToUpperInvariant());
            eventFeedSummary.text=active!=null?active.Title+"\n"+(active.StartMinute>world.Data.Minute?JusticeRules.When(active.StartMinute):"Review by "+JusticeRules.When(active.DeadlineMinute)):goal==null?"All current goals complete.":goal.Title+" · "+goal.Progress+" / "+goal.Target;
            NotableResident notable=resident==null?null:world.Stories.Notables.FirstOrDefault(n=>n.ResidentId==resident.Id);
            CapitalCase capital=resident==null?null:world.CapitalCaseFor(resident.Id);
            eventDossier.style.display=notable!=null||capital!=null?DisplayStyle.Flex:DisplayStyle.None;
            eventDossierText.text=(notable==null?"":notable.Role+" · influence "+notable.Influence+"/100\n"+notable.Motive)+(capital==null?"":"\nCapital case · "+capital.Stage+"\nReviews "+capital.Reviews+"/2 · "+(capital.Counsel?"independent counsel funded":"counsel available"));
        }
        void JournalAction(VisualElement parent,string label,System.Action action,bool enabled=true)
        {Button button=Action(label,()=>{action();RebuildFacilityJournal();Refresh(true);},"quiet");button.SetEnabled(enabled);parent.Add(button);}
        void RebuildFacilityJournal()
        {
            World w=game.World;if(w==null)return;w.EnsureFacilityStories();eventScroll.Clear();
            eventScroll.Add(Text("PUBLIC CONFIDENCE · "+w.Stories.Reputation+" / 100","section-label"));
            eventScroll.Add(Text("Contract challenger: "+w.Stories.Rival+"\n"+w.Stories.RivalMotive,"body"));
            eventScroll.Add(Text("FACILITY GOALS","section-label"));
            foreach(var g in w.Stories.Goals)
            {
                var card=Element("event-card");eventScroll.Add(card);card.Add(Text((g.CompletedMinute>=0?"✓ ":"")+g.Title,"project-name"));card.Add(Text(g.Description,"body"));card.Add(Text(g.Progress+" / "+g.Target+" · grant "+Money(g.RewardCents)+(g.CompletedMinute>=0?" received":" at completion"),"muted"));
            }
            eventScroll.Add(Text("INCIDENTS & OVERSIGHT","section-label"));
            foreach(var incident in w.Stories.Incidents.AsEnumerable().Reverse().Take(12))
            {
                string id=incident.Id;var card=Element("event-card");eventScroll.Add(card);card.Add(Text(incident.Title,"project-name"));card.Add(Text(incident.Body,"body"));card.Add(Text(incident.Phase+" · "+JusticeRules.When(incident.StartMinute)+"\n"+(incident.Phase=="Resolved"?incident.Result:"Deadline "+JusticeRules.When(incident.DeadlineMinute)),"muted"));
                if(incident.Phase=="Resolved")continue;
                if(incident.Kind=="Inspection")JournalAction(card,"Prepare records · $350",()=>w.RespondToIncident(id,"Prepare records"),incident.Choice==""&&w.Data.CashCents>=35000);
                if(incident.Kind=="Protest")
                {
                    JournalAction(card,"Publish welfare report",()=>w.RespondToIncident(id,"Publish report"),incident.Choice=="");
                    JournalAction(card,"Arrange independent visit · $600",()=>w.RespondToIncident(id,"Independent visit"),incident.Choice==""&&w.Data.CashCents>=60000);
                }
                if(incident.Kind=="Pressure")JournalAction(card,"Arrange mediation · $200",()=>w.RespondToIncident(id,"Mediation"),incident.Choice==""&&w.Data.CashCents>=20000);
                if(incident.Choice!="")card.Add(Text("Response: "+incident.Choice+". Actual conditions still determine the outcome.","body"));
            }
            eventScroll.Add(Text("CAPITAL CASES / DEATH ROW","section-label"));
            if(w.Stories.CapitalCases.Count==0)eventScroll.Add(Text("No capital cases. This designation requires a pre-existing capital conviction; the owner cannot impose it. The secure modernization start includes an authored case.","body"));
            foreach(var capital in w.Stories.CapitalCases)
            {
                string id=capital.ResidentId;var card=Element("event-card");eventScroll.Add(card);card.Add(Text(capital.Name+" · "+capital.Stage,"project-name"));
                card.Add(Text("Appellate reviews "+capital.Reviews+" / 2 · "+(capital.Counsel?"independent counsel":"standard review")+"\n"+capital.Outcome,"body"));
                if(capital.Stage=="Death row"||capital.Stage=="Appeal pending"||capital.Stage=="Stayed")card.Add(Text("Next review: "+JusticeRules.When(capital.ReviewMinute),"muted"));
                JournalAction(card,"Independent counsel · $1,200",()=>w.CapitalAction(id,"Counsel"),!capital.Counsel&&capital.Reviews<2&&capital.Stage!="Executed"&&capital.Stage!="Commuted"&&w.Data.CashCents>=120000);
                if(capital.Stage=="Escorting")JournalAction(card,"Postpone transfer",()=>w.CapitalAction(id,"Postpone"));
                if(capital.Stage=="Final order")
                {
                    card.Add(Text("Transfer eligible after "+JusticeRules.When(capital.FinalOrderMinute)+". An on-shift officer, reachable route, free transport bay and open gate are required.","body"));
                    JournalAction(card,"Petition for stay · $250",()=>w.CapitalAction(id,"Stay"),!capital.StayUsed&&w.Data.CashCents>=25000);
                    JournalAction(card,transferConfirmation==id?"Confirm court-ordered transfer":"Review final transfer order",()=>{if(transferConfirmation!=id){transferConfirmation=id;return;}w.CapitalAction(id,"Transfer");transferConfirmation="";},w.Data.Minute>=capital.FinalOrderMinute);
                    if(transferConfirmation==id)card.Add(Text("This transfer leads to an irreversible, non-graphical off-site disposition under the final order. The owner does not choose or carry out the method.","body"));
                }
            }
            if(Application.isEditor||Debug.isDebugBuild)
            {
                eventScroll.Add(Text("REVIEW SCENARIOS","section-label"));
                JournalAction(eventScroll,"Stage state inspection",()=>w.ScheduleFacilityIncident("Inspection",0,"State"));
                JournalAction(eventScroll,"Stage federal inspection",()=>w.ScheduleFacilityIncident("Inspection",0,"Federal"));
                JournalAction(eventScroll,"Stage peaceful demonstration",()=>w.ScheduleFacilityIncident("Protest",0));
                JournalAction(eventScroll,"Stage resident pressure",()=>w.ScheduleFacilityIncident("Pressure",0));
            }
        }
    }
}
