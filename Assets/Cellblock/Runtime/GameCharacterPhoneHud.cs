using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // A retained screen for the selected character's actual device, terminal or issued handset.
    // Opening it changes input ownership; all call progress remains on the core game clock.
    public sealed partial class GameHud
    {
        enum CharacterPhoneApp { Messages, Contacts, Tasks, Items }
        sealed class PhoneMessageRow
        {
            internal VisualElement Root, Details, Options;
            internal Button Heading;
            internal Label Text, Stamp;
            internal readonly Button[] Choices = new Button[4];
            internal string Id = "";
            internal readonly string[] PressedChoiceIds = new string[4];
        }
        sealed class PhoneContactRow
        {
            internal VisualElement Root;
            internal Label Name, Detail, Blocker;
            internal Button Call;
            internal string Id = "", PressedId = "";
        }
        sealed class PhoneTaskRow
        {
            internal VisualElement Root;
            internal Button Heading;
            internal Label Detail, Due;
            internal string Id = "";
        }
        sealed class PhoneItemRow
        {
            internal VisualElement Root;
            internal Label Name, Detail;
        }

        VisualElement characterPhone, characterPhoneTabs, characterPhoneApps, characterPhoneUnavailable;
        VisualElement characterPhoneCall, characterPhoneQuote, characterPhoneFooter;
        Label characterPhoneName, characterPhoneDevice, characterPhoneClock, characterPhoneAccessText, characterPhoneLocation, characterPhoneStashLocation, characterPhoneFeedback;
        Label characterPhoneAppTitle, characterPhoneAppHint, characterPhoneCallTitle, characterPhoneCallResult;
        Label characterPhoneQuoteTitle, characterPhoneQuoteTerms, characterPhoneQuoteBlocker, characterPhoneItemsSummary;
        Label characterPhoneEmptyMessages, characterPhoneEmptyContacts, characterPhoneEmptyTasks, characterPhoneEmptyItems;
        Button characterPhoneConfirm, characterPhoneCancelQuote, characterPhoneEndCall, characterPhoneItemsPrevious, characterPhoneItemsNext, characterPhoneResume, characterPhoneRetrieve;
        StatMeter characterPhoneCallProgress;
        readonly ScrollView[] characterPhonePages = new ScrollView[4];
        readonly Button[] characterPhoneAppButtons = new Button[4];
        readonly PhoneMessageRow[] characterPhoneMessages = new PhoneMessageRow[16];
        readonly PhoneContactRow[] characterPhoneContacts = new PhoneContactRow[7];
        readonly PhoneTaskRow[] characterPhoneTasks = new PhoneTaskRow[12];
        readonly PhoneItemRow[] characterPhoneItems = new PhoneItemRow[12];
        CharacterPhoneApp characterPhoneApp;
        string characterPhoneSubject = "", characterPhoneExpandedMessage = "", characterPhoneExpandedTask = "", characterPhoneQuoteId = "";
        string characterPhoneQuoteChannel = "";
        int characterPhoneItemPage, characterPhoneQuoteDuration, characterPhoneQuoteRate;
        long characterPhoneQuoteCost;
        World characterPhoneWorld;
        int characterPhoneRouteRevision = -1;
        Point characterPhoneRouteFrom, characterPhoneRouteTo;
        string characterPhoneRouteText = "";
        string characterPhoneActionFeedback = "";
        bool characterPhoneShort;
        public bool CharacterPhoneOpen { get; private set; }

        void BuildCharacterPhone()
        {
            if (characterPhone != null) return;
            StyleSheet styles = Resources.Load<StyleSheet>("CharacterPhone");
            if (styles != null) root.styleSheets.Add(styles);
            characterPhone = Element("character-phone"); characterPhone.name = "character-phone";
            characterPhone.style.display = DisplayStyle.None;
            var heading = Element("character-phone-heading");
            var identity = Element("character-phone-identity");
            characterPhoneClock = Text("", "character-phone-clock"); identity.Add(characterPhoneClock);
            characterPhoneName = Text("", "character-phone-name"); identity.Add(characterPhoneName); heading.Add(identity);
            heading.Add(Action("Close", CloseCharacterPhone, "character-phone-close")); characterPhone.Add(heading);
            characterPhoneDevice = Text("", "character-phone-device"); characterPhone.Add(characterPhoneDevice);

            characterPhoneCall = Element("character-phone-call");
            var callRow = Element("character-phone-call-row"); var callStatus = Element("character-phone-call-status");
            characterPhoneCallTitle = Text("", "character-phone-call-title"); callStatus.Add(characterPhoneCallTitle);
            characterPhoneCallProgress = new StatMeter("CALL"); callStatus.Add(characterPhoneCallProgress); callRow.Add(callStatus);
            characterPhoneEndCall = Action("End call", EndCharacterPhoneCall, "character-phone-end"); callRow.Add(characterPhoneEndCall); characterPhoneCall.Add(callRow);
            characterPhoneCallResult = Text("", "character-phone-muted"); characterPhoneCall.Add(characterPhoneCallResult);
            characterPhone.Add(characterPhoneCall);

            characterPhoneUnavailable = new ScrollView(ScrollViewMode.Vertical); characterPhoneUnavailable.AddToClassList("character-phone-unavailable");
            ((ScrollView)characterPhoneUnavailable).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            characterPhoneUnavailable.Add(Text("PHONE ACCESS", "character-phone-section"));
            characterPhoneAccessText = Text("", "character-phone-copy"); characterPhoneUnavailable.Add(characterPhoneAccessText);
            characterPhoneStashLocation = Text("", "character-phone-location"); characterPhoneUnavailable.Add(characterPhoneStashLocation);
            characterPhoneRetrieve = Action("Retrieve stashed phone", RetrieveOwnCharacterPhone, "character-phone-retrieve"); characterPhoneUnavailable.Add(characterPhoneRetrieve);
            characterPhoneLocation = Text("", "character-phone-location"); characterPhoneUnavailable.Add(characterPhoneLocation);
            characterPhone.Add(characterPhoneUnavailable);

            characterPhoneTabs = Element("character-phone-tabs");
            string[] names = { "Messages", "Contacts", "Tasks", "Items" };
            for (int i = 0; i < names.Length; i++)
            {
                CharacterPhoneApp app = (CharacterPhoneApp)i;
                characterPhoneAppButtons[i] = Action(names[i], () => ShowCharacterPhoneApp(app), "character-phone-tab");
                characterPhoneTabs.Add(characterPhoneAppButtons[i]);
            }
            characterPhone.Add(characterPhoneTabs);
            characterPhoneApps = Element("character-phone-apps");
            characterPhoneAppTitle = Text("", "character-phone-section"); characterPhoneApps.Add(characterPhoneAppTitle);
            characterPhoneAppHint = Text("", "character-phone-muted"); characterPhoneApps.Add(characterPhoneAppHint);
            for (int i = 0; i < characterPhonePages.Length; i++)
            {
                var page = new ScrollView(ScrollViewMode.Vertical); page.AddToClassList("character-phone-page"); page.name = "character-phone-page-" + names[i].ToLowerInvariant();
                page.horizontalScrollerVisibility = ScrollerVisibility.Hidden; characterPhonePages[i] = page; characterPhoneApps.Add(page);
            }
            BuildCharacterPhoneMessages(); BuildCharacterPhoneContacts(); BuildCharacterPhoneTasks(); BuildCharacterPhoneItems();
            characterPhone.Add(characterPhoneApps);

            characterPhoneQuote = new ScrollView(ScrollViewMode.Vertical); characterPhoneQuote.AddToClassList("character-phone-quote");
            ((ScrollView)characterPhoneQuote).horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            characterPhoneQuote.Add(Text("REVIEW CALL", "character-phone-section"));
            characterPhoneQuoteTitle = Text("", "character-phone-quote-title"); characterPhoneQuote.Add(characterPhoneQuoteTitle);
            characterPhoneQuoteTerms = Text("", "character-phone-copy"); characterPhoneQuote.Add(characterPhoneQuoteTerms);
            characterPhoneQuoteBlocker = Text("", "character-phone-blocker"); characterPhoneQuote.Add(characterPhoneQuoteBlocker);
            characterPhone.Add(characterPhoneQuote);

            characterPhoneFooter = Element("character-phone-footer");
            characterPhoneFeedback = Text("", "character-phone-blocker"); characterPhoneFooter.Add(characterPhoneFeedback);
            characterPhoneResume = Action("Resume game clock", () => { if (game.World != null) { game.ClearCharacterInput(); ResetCharacterSticks(); game.World.Data.Speed = 1; Refresh(true); } }, "character-phone-resume");
            characterPhoneFooter.Add(characterPhoneResume);
            var quoteActions = Element("character-phone-quote-actions"); quoteActions.name = "character-phone-quote-actions";
            characterPhoneCancelQuote = Action("Back", () => { characterPhoneQuoteId = ""; RefreshCharacterPhone(game.World); }, "character-phone-back");
            characterPhoneConfirm = Action("Start call", ConfirmCharacterPhoneCall, "character-phone-confirm");
            quoteActions.Add(characterPhoneCancelQuote); quoteActions.Add(characterPhoneConfirm); characterPhoneFooter.Add(quoteActions);
            var back = Action("Back to character", CloseCharacterPhone, "character-phone-return"); back.name = "character-phone-return"; characterPhoneFooter.Add(back);
            characterPhoneFooter.Add(Text("Time continues here. Close to move.", "character-phone-footer-note"));
            characterPhone.Add(characterPhoneFooter); root.Add(characterPhone);
        }

        void BuildCharacterPhoneMessages()
        {
            ScrollView page = characterPhonePages[(int)CharacterPhoneApp.Messages];
            characterPhoneEmptyMessages = Text("No messages yet. Decisions and recent experiences appear here.", "character-phone-empty"); page.Add(characterPhoneEmptyMessages);
            for (int i = 0; i < characterPhoneMessages.Length; i++)
            {
                var row = new PhoneMessageRow(); characterPhoneMessages[i] = row; row.Root = Element("character-phone-card");
                row.Heading = Action("", () => { characterPhoneExpandedMessage = characterPhoneExpandedMessage == row.Id ? "" : row.Id; RefreshCharacterPhone(game.World); }, "character-phone-card-heading"); row.Root.Add(row.Heading);
                row.Stamp = Text("", "character-phone-stamp"); row.Root.Add(row.Stamp);
                row.Details = Element("character-phone-details"); row.Text = Text("", "character-phone-copy"); row.Details.Add(row.Text);
                row.Options = Element("character-phone-options");
                for (int j = 0; j < row.Choices.Length; j++)
                {
                    int option = j;
                    row.Choices[j] = Action("", () => {
                        string id = string.IsNullOrEmpty(row.PressedChoiceIds[option]) ? row.Id : row.PressedChoiceIds[option]; row.PressedChoiceIds[option] = "";
                        ChooseCharacterPhoneMessage(id, option);
                    }, "character-phone-choice");
                    row.Choices[j].RegisterCallback<PointerDownEvent>(e => row.PressedChoiceIds[option] = row.Id, TrickleDown.TrickleDown);
                    row.Choices[j].RegisterCallback<KeyDownEvent>(e => row.PressedChoiceIds[option] = "", TrickleDown.TrickleDown);
                    row.Options.Add(row.Choices[j]);
                }
                row.Details.Add(row.Options); row.Root.Add(row.Details); page.Add(row.Root);
            }
        }
        void BuildCharacterPhoneContacts()
        {
            ScrollView page = characterPhonePages[(int)CharacterPhoneApp.Contacts];
            characterPhoneEmptyContacts = Text("No contacts available for this character.", "character-phone-empty"); page.Add(characterPhoneEmptyContacts);
            for (int i = 0; i < characterPhoneContacts.Length; i++)
            {
                var row = new PhoneContactRow(); characterPhoneContacts[i] = row; row.Root = Element("character-phone-card");
                row.Name = Text("", "character-phone-contact-name"); row.Detail = Text("", "character-phone-muted"); row.Blocker = Text("", "character-phone-blocker");
                row.Call = Action("Review call", () => { string id = row.PressedId == "" ? row.Id : row.PressedId; row.PressedId = ""; OpenCharacterPhoneQuote(id); }, "character-phone-contact-call");
                row.Call.RegisterCallback<PointerDownEvent>(e => row.PressedId = row.Id, TrickleDown.TrickleDown);
                row.Call.RegisterCallback<KeyDownEvent>(e => row.PressedId = "", TrickleDown.TrickleDown);
                row.Root.Add(row.Name); row.Root.Add(row.Detail); row.Root.Add(row.Blocker); row.Root.Add(row.Call); page.Add(row.Root);
            }
        }
        void BuildCharacterPhoneTasks()
        {
            ScrollView page = characterPhonePages[(int)CharacterPhoneApp.Tasks];
            characterPhoneEmptyTasks = Text("No outstanding tasks. Follow the current routine or assignment.", "character-phone-empty"); page.Add(characterPhoneEmptyTasks);
            for (int i = 0; i < characterPhoneTasks.Length; i++)
            {
                var row = new PhoneTaskRow(); characterPhoneTasks[i] = row; row.Root = Element("character-phone-card");
                row.Heading = Action("", () => { characterPhoneExpandedTask = characterPhoneExpandedTask == row.Id ? "" : row.Id; RefreshCharacterPhone(game.World); }, "character-phone-card-heading");
                row.Due = Text("", "character-phone-stamp"); row.Detail = Text("", "character-phone-copy"); row.Root.Add(row.Heading); row.Root.Add(row.Due); row.Root.Add(row.Detail); page.Add(row.Root);
            }
        }
        void BuildCharacterPhoneItems()
        {
            ScrollView page = characterPhonePages[(int)CharacterPhoneApp.Items];
            characterPhoneItemsSummary = Text("", "character-phone-wallet"); page.Add(characterPhoneItemsSummary);
            characterPhoneEmptyItems = Text("No recorded personal items.", "character-phone-empty"); page.Add(characterPhoneEmptyItems);
            for (int i = 0; i < characterPhoneItems.Length; i++)
            {
                var row = new PhoneItemRow(); characterPhoneItems[i] = row; row.Root = Element("character-phone-card");
                row.Name = Text("", "character-phone-contact-name"); row.Detail = Text("", "character-phone-muted"); row.Root.Add(row.Name); row.Root.Add(row.Detail); page.Add(row.Root);
            }
            var navigation = Element("character-phone-item-pages");
            characterPhoneItemsPrevious = Action("Previous", () => { characterPhoneItemPage = Mathf.Max(0, characterPhoneItemPage - 1); RefreshCharacterPhone(game.World); page.scrollOffset = Vector2.zero; }, "character-phone-back");
            characterPhoneItemsNext = Action("More items", () => { characterPhoneItemPage++; RefreshCharacterPhone(game.World); page.scrollOffset = Vector2.zero; }, "character-phone-back");
            navigation.Add(characterPhoneItemsPrevious); navigation.Add(characterPhoneItemsNext); page.Add(navigation);
        }

        public void ToggleCharacterPhone()
        {
            if (CharacterPhoneOpen) { CloseCharacterPhone(); return; }
            if (game == null || game.World == null || !game.CharacterDirect || !game.CharacterCanPlay || string.IsNullOrEmpty(game.CharacterPhoneSubjectId)) return;
            if (characterPhone == null) BuildCharacterPhone();
            CharacterPhoneOpen = true; characterPhoneSubject = ""; characterPhoneQuoteId = ""; characterPhoneActionFeedback = "";
            RefreshCharacterPhone(game.World); characterPhone.BringToFront(); Layout();
        }
        public void CloseCharacterPhone()
        {
            CharacterPhoneOpen = false; characterPhoneQuoteId = "";
            if (characterPhone != null) characterPhone.style.display = DisplayStyle.None;
            if (root != null) Layout();
        }
        public void ShowCharacterTasks() { if (!CharacterPhoneOpen) ToggleCharacterPhone(); if (CharacterPhoneOpen) ShowCharacterPhoneApp(CharacterPhoneApp.Tasks); }
        void ShowCharacterPhoneApp(CharacterPhoneApp app)
        {
            characterPhoneApp = app; characterPhoneQuoteId = ""; characterPhoneActionFeedback = ""; RefreshCharacterPhone(game.World);
        }
        void ChooseCharacterPhoneMessage(string cardId, int option)
        {
            if (!CharacterPhoneOpen || game.World == null) return;
            bool chosen = game.World.ChoosePhoneMessage(characterPhoneSubject, cardId, option);
            characterPhoneActionFeedback = chosen ? "" : game.World.Notice;
            if (chosen) { SaveCharacterPhoneAction(); characterPhoneExpandedMessage = ""; }
            Refresh(true);
        }
        void OpenCharacterPhoneQuote(string contactId)
        {
            if (game.World == null) return;
            CharacterPhoneContact contact = game.World.PhoneContacts(characterPhoneSubject).FirstOrDefault(c => c.Id == contactId);
            if (contact == null) return;
            CharacterPhoneAccess access = game.World.PhoneAccess(characterPhoneSubject);
            characterPhoneActionFeedback = ""; characterPhoneQuoteId = contact.Id; characterPhoneQuoteCost = contact.PrepaidCents; characterPhoneQuoteDuration = contact.Duration;
            characterPhoneQuoteChannel = access.Channel; characterPhoneQuoteRate = access.RateCents; RefreshCharacterPhone(game.World);
            ((ScrollView)characterPhoneQuote).scrollOffset = Vector2.zero;
        }
        void ConfirmCharacterPhoneCall()
        {
            if (game.World == null || characterPhoneQuoteId == "") return;
            CharacterPhoneContact contact = game.World.PhoneContacts(characterPhoneSubject).FirstOrDefault(c => c.Id == characterPhoneQuoteId);
            CharacterPhoneAccess access = game.World.PhoneAccess(characterPhoneSubject);
            if (contact == null) { characterPhoneQuoteId = ""; RefreshCharacterPhone(game.World); return; }
            if (contact.PrepaidCents != characterPhoneQuoteCost || contact.Duration != characterPhoneQuoteDuration || access.Channel != characterPhoneQuoteChannel || access.RateCents != characterPhoneQuoteRate)
            { OpenCharacterPhoneQuote(contact.Id); characterPhoneActionFeedback = "The quote changed. Review the updated terms and confirm again."; RefreshCharacterPhone(game.World); return; }
            bool started = game.World.StartCharacterCall(characterPhoneSubject, characterPhoneQuoteId);
            characterPhoneActionFeedback = started ? "" : game.World.Notice;
            if (started) { SaveCharacterPhoneAction(); characterPhoneQuoteId = ""; }
            Refresh(true);
        }
        void EndCharacterPhoneCall()
        {
            if (game.World != null && game.World.CancelCharacterCall(characterPhoneSubject)) SaveCharacterPhoneAction();
            Refresh(true);
        }
        void RetrieveOwnCharacterPhone()
        {
            if (game.World == null) return;
            bool retrieved = game.World.RetrieveCharacterPhone(characterPhoneSubject);
            characterPhoneActionFeedback = retrieved ? "" : game.World.Notice;
            if (retrieved) SaveCharacterPhoneAction();
            Refresh(true);
        }
        void SaveCharacterPhoneAction()
        {
            if (!game.Save(false)) characterPhoneActionFeedback = "Action applied. Save failed; retry Save from the menu.";
        }

        void RefreshCharacterPhone(World world)
        {
            if (characterPhone == null) return;
            if (!CharacterPhoneOpen) { characterPhone.style.display = DisplayStyle.None; return; }
            if (world == null || !game.CharacterDirect || !game.CharacterCanPlay || string.IsNullOrEmpty(game.CharacterPhoneSubjectId)) { CloseCharacterPhone(); return; }
            string subject = game.CharacterPhoneSubjectId;
            if (world != characterPhoneWorld || subject != characterPhoneSubject)
            {
                characterPhoneWorld = world; characterPhoneSubject = subject; characterPhoneQuoteId = ""; characterPhoneExpandedMessage = ""; characterPhoneExpandedTask = ""; characterPhoneItemPage = 0; characterPhoneRouteRevision = -1; characterPhoneActionFeedback = "";
                foreach (ScrollView page in characterPhonePages) page.scrollOffset = Vector2.zero;
            }
            CharacterPhoneAccess access = world.PhoneAccess(subject);
            characterPhone.style.display = DisplayStyle.Flex;
            characterPhoneName.text = game.CharacterLabel;
            characterPhoneClock.text = "DAY " + world.Day.ToString("00") + " / " + (world.Data.Minute / 60 % 24).ToString("00") + ":" + (world.Data.Minute % 60).ToString("00") + (world.Data.Speed == 0 ? " · PAUSED" : " · " + world.Data.Speed + "×");
            characterPhoneDevice.text = access.Equipment == "" ? "Phone access" : access.Equipment;
            characterPhoneDevice.EnableInClassList("character-phone-illicit", access.Illicit);
            characterPhoneUnavailable.style.display = access.CanOpen ? DisplayStyle.None : DisplayStyle.Flex;
            characterPhoneAccessText.text = access.Blocker;
            RefreshCharacterPhoneStash(world, subject, access);
            characterPhoneLocation.text = !access.Present ? "Return to management and select someone on site." : access.HasCallAnchor ? DescribeCharacterPayphone(world, subject, access) :
                access.HasStashedPhone ? "A stashed item stays in the cell until collected in person." : "Place a Payphone in a resident area and enable monitored phone service in Operations.";
            characterPhoneTabs.style.display = access.CanOpen && characterPhoneQuoteId == "" ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneApps.style.display = access.CanOpen && characterPhoneQuoteId == "" ? DisplayStyle.Flex : DisplayStyle.None;
            if (!access.CanOpen) characterPhoneQuoteId = "";
            characterPhoneQuote.style.display = access.CanOpen && characterPhoneQuoteId != "" ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneFooter.Q<VisualElement>("character-phone-quote-actions").style.display = characterPhoneQuoteId == "" ? DisplayStyle.None : DisplayStyle.Flex;
            characterPhoneFooter.Q<Button>("character-phone-return").style.display = characterPhoneQuoteId == "" ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneResume.style.display = world.Data.Speed == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneFeedback.text = characterPhoneActionFeedback; characterPhoneFeedback.style.display = characterPhoneActionFeedback == "" ? DisplayStyle.None : DisplayStyle.Flex;
            RefreshCharacterPhoneCall(world, subject);
            if (!access.CanOpen) return;
            RefreshCharacterPhoneQuote(world, access);
            RefreshCharacterPhoneMessages(world, subject); RefreshCharacterPhoneContacts(world, subject); RefreshCharacterPhoneTasks(world, subject); RefreshCharacterPhoneItems(world, subject, access);
            string[] headings = { "MESSAGES", "CONTACTS", "TASKS", "POSSESSIONS" };
            string[] hints = { "Open one message to read and reply.", "Review the duration and price before calling.", "Open a task for its details.", "Held items and cell stashes remain distinct." };
            characterPhoneAppTitle.text = headings[(int)characterPhoneApp]; characterPhoneAppHint.text = game.CharacterDirect ? hints[(int)characterPhoneApp] : "Take direct control to reply or call.";
            for (int i = 0; i < characterPhonePages.Length; i++)
            { characterPhonePages[i].style.display = i == (int)characterPhoneApp ? DisplayStyle.Flex : DisplayStyle.None; characterPhoneAppButtons[i].EnableInClassList("selected", i == (int)characterPhoneApp); }
        }
        void RefreshCharacterPhoneStash(World world, string subject, CharacterPhoneAccess access)
        {
            Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == subject);
            ContrabandItem stash = resident == null ? null : world.ItemsOf(resident).Where(i => i.Kind == "Phone" && i.Place == "Stash").OrderBy(i => Geometry.Distance(resident.Position, i.Position)).FirstOrDefault();
            bool visible = stash != null && access.HasStashedPhone;
            characterPhoneRetrieve.style.display = characterPhoneStashLocation.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            float distance = Geometry.Distance(resident.Position, stash.Position);
            bool reachable = distance <= 1.6f && world.SightLines.Clear(resident.Position, stash.Position);
            characterPhoneStashLocation.text = "Your phone stash · " + Mathf.CeilToInt(distance) + " m away\nCell location " + stash.Position.X.ToString("0.0") + ", " + stash.Position.Y.ToString("0.0") + (reachable ? ". Within reach." : ". Close this panel and return there.");
            characterPhoneRetrieve.SetEnabled(game.CharacterDirect && !access.HasHeldPhone && reachable && world.FreeTime(resident));
            characterPhoneRetrieve.text = access.HasHeldPhone ? "Phone already held" : "Retrieve stashed phone";
        }
        string DescribeCharacterPayphone(World world, string subject, CharacterPhoneAccess access)
        {
            Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == subject);
            if (resident == null) return "Payphone at " + access.CallAnchor.X.ToString("0.0") + ", " + access.CallAnchor.Y.ToString("0.0") + ".";
            if (access.AnchorDistance <= 1.4f) return "The nearest payphone is within reach. Its monitored phone service must be enabled in Operations.";
            if (characterPhoneRouteRevision == world.GeometryRevision && Geometry.Distance(characterPhoneRouteFrom, resident.Position) < .5f && Geometry.Distance(characterPhoneRouteTo, access.CallAnchor) < .1f) return characterPhoneRouteText;
            characterPhoneRouteRevision = world.GeometryRevision; characterPhoneRouteFrom = resident.Position; characterPhoneRouteTo = access.CallAnchor;
            List<Point> route = world.Routes.Find(resident.Position, access.CallAnchor);
            if (route == null || route.Count == 0) return characterPhoneRouteText = "Nearest installed payphone has no available walking route. Check doors and access.";
            float distance = Geometry.Distance(resident.Position, route[0]);
            for (int i = 1; i < route.Count; i++) distance += Geometry.Distance(route[i - 1], route[i]);
            return characterPhoneRouteText = "Nearest payphone · " + Mathf.CeilToInt(distance) + " m walk\nLocation " + access.CallAnchor.X.ToString("0.0") + ", " + access.CallAnchor.Y.ToString("0.0") + ". Close this panel to walk there.";
        }
        void RefreshCharacterPhoneCall(World world, string subject)
        {
            CharacterCall call = world.CurrentCharacterCall(subject);
            bool active = call != null && call.Status == "Calling";
            bool recent = call != null && (active || world.Data.Minute - call.EndedMinute < 60);
            characterPhoneCall.style.display = recent ? DisplayStyle.Flex : DisplayStyle.None;
            if (!recent) return;
            characterPhoneCallTitle.text = call.ContactName + " · " + (active ? "Connected" : call.Status);
            characterPhoneCallProgress.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneCallProgress.SetScore("CALL TIME", call.Elapsed, MeterMeaning.Neutral, call.Elapsed + " / " + call.Duration + " MIN", scale: call.Duration);
            characterPhoneCallResult.text = active ? (world.Data.Speed == 0 ? "Paused · resume the game clock to continue." : call.Channel == "Monitored" ? "Stay at the payphone until the call ends." : "Call continues on the game clock.") : call.Result;
            characterPhoneCallResult.style.display = call.Result != "" || active && !characterPhoneShort ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneEndCall.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
        }
        void RefreshCharacterPhoneQuote(World world, CharacterPhoneAccess access)
        {
            if (characterPhoneQuoteId == "") return;
            CharacterPhoneContact contact = world.PhoneContacts(characterPhoneSubject).FirstOrDefault(c => c.Id == characterPhoneQuoteId);
            characterPhoneQuoteTitle.text = contact == null ? "Contact unavailable" : contact.Name;
            characterPhoneQuoteTerms.text = characterPhoneQuoteDuration + " game minutes\n" + (characterPhoneQuoteCost > 0 ?
                characterPhoneQuoteRate + "¢ / minute · " + JusticeRules.Dollars(characterPhoneQuoteCost) + " prepaid\nUnused time is not refunded if you hang up or the call is interrupted.\nTrust balance: " + JusticeRules.Dollars(access.BalanceCents) :
                "No facility call charge." + (characterPhoneQuoteChannel == "Contraband" ? "\nIllicit phone use adds suspicion and can be intercepted or seized." : ""));
            characterPhoneQuoteBlocker.text = world.Data.Speed == 0 ? "Resume the game clock before starting." : contact == null ? "This contact is no longer available." : contact.Blocker;
            characterPhoneConfirm.SetEnabled(contact != null && contact.CanCall && world.Data.Speed != 0);
            characterPhoneConfirm.text = characterPhoneQuoteCost > 0 ? "Start · " + JusticeRules.Dollars(characterPhoneQuoteCost) : "Start call";
        }
        void RefreshCharacterPhoneMessages(World world, string subject)
        {
            List<CharacterPhoneMessage> messages = world.PhoneMessages(subject); characterPhoneEmptyMessages.style.display = messages.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            int decisions = messages.Count(m => m.Kind == "Decision"); characterPhoneAppButtons[0].text = decisions > 0 ? "Messages " + decisions : "Messages";
            for (int i = 0; i < characterPhoneMessages.Length; i++)
            {
                PhoneMessageRow row = characterPhoneMessages[i]; row.Root.style.display = i < messages.Count ? DisplayStyle.Flex : DisplayStyle.None; if (i >= messages.Count) { row.Id = ""; continue; }
                CharacterPhoneMessage message = messages[i]; row.Id = message.Id; bool expanded = row.Id == characterPhoneExpandedMessage;
                row.Heading.text = (expanded ? "− " : "+ ") + message.Title; row.Heading.EnableInClassList("selected", expanded);
                row.Stamp.text = message.Kind == "Decision" ? "REPLY BY " + JusticeRules.When(message.ExpiresMinute) : message.Kind.ToUpperInvariant() + " · " + JusticeRules.When(message.Minute);
                row.Text.text = message.Text; row.Details.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                row.Options.style.display = message.Options.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                for (int j = 0; j < row.Choices.Length; j++)
                { row.Choices[j].style.display = j < message.Options.Count ? DisplayStyle.Flex : DisplayStyle.None; if (j < message.Options.Count) row.Choices[j].text = message.Options[j]; row.Choices[j].SetEnabled(message.Actionable && game.CharacterDirect); }
            }
        }
        void RefreshCharacterPhoneContacts(World world, string subject)
        {
            List<CharacterPhoneContact> contacts = world.PhoneContacts(subject); characterPhoneEmptyContacts.style.display = contacts.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < characterPhoneContacts.Length; i++)
            {
                PhoneContactRow row = characterPhoneContacts[i]; row.Root.style.display = i < contacts.Count ? DisplayStyle.Flex : DisplayStyle.None; if (i >= contacts.Count) { row.Id = ""; continue; }
                CharacterPhoneContact contact = contacts[i]; row.Id = contact.Id; row.Name.text = contact.Name; row.Detail.text = contact.Kind + " · " + contact.Detail;
                row.Blocker.text = contact.Blocker; row.Blocker.style.display = contact.Blocker == "" ? DisplayStyle.None : DisplayStyle.Flex;
                row.Call.text = contact.PrepaidCents > 0 ? "Review · " + contact.Duration + " min / " + JusticeRules.Dollars(contact.PrepaidCents) : "Review · " + contact.Duration + " min";
                row.Call.SetEnabled(contact.CanCall && game.CharacterDirect);
            }
        }
        void RefreshCharacterPhoneTasks(World world, string subject)
        {
            List<CharacterPhoneTask> tasks = world.PhoneTasks(subject); characterPhoneEmptyTasks.style.display = tasks.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < characterPhoneTasks.Length; i++)
            {
                PhoneTaskRow row = characterPhoneTasks[i]; row.Root.style.display = i < tasks.Count ? DisplayStyle.Flex : DisplayStyle.None; if (i >= tasks.Count) { row.Id = ""; continue; }
                CharacterPhoneTask task = tasks[i]; row.Id = task.Id; bool expanded = row.Id == characterPhoneExpandedTask;
                row.Heading.text = (expanded ? "− " : "+ ") + task.Title; row.Detail.text = task.Detail; row.Detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                row.Due.text = task.Kind.ToUpperInvariant() + (task.DueMinute >= 0 ? " · " + JusticeRules.When(task.DueMinute) : "");
            }
        }
        void RefreshCharacterPhoneItems(World world, string subject, CharacterPhoneAccess access)
        {
            Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == subject);
            List<ContrabandItem> items = resident == null ? new List<ContrabandItem>() : world.ItemsOf(resident).OrderBy(i => i.Place == "Held" ? 0 : 1).ThenBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
            TrustAccount account = resident == null ? null : world.AccountOf(resident);
            characterPhoneItemsSummary.text = account == null ? access.Equipment : "Trust " + JusticeRules.Dollars(account.BalanceCents) + " · " + account.Soups + " soups\n" + items.Count(i => i.Place == "Held") + " held · " + items.Count(i => i.Place == "Stash") + " in cell stash";
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)characterPhoneItems.Length)); characterPhoneItemPage = Mathf.Clamp(characterPhoneItemPage, 0, pages - 1);
            characterPhoneEmptyItems.style.display = resident != null && items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < characterPhoneItems.Length; i++)
            {
                int index = characterPhoneItemPage * characterPhoneItems.Length + i; PhoneItemRow row = characterPhoneItems[i]; row.Root.style.display = index < items.Count ? DisplayStyle.Flex : DisplayStyle.None; if (index >= items.Count) continue;
                ContrabandItem item = items[index]; ContrabandKind kind = ContrabandRules.Kind(item.Kind);
                row.Name.text = (kind == null ? item.Kind : kind.Name) + " · " + (item.Place == "Held" ? "Held" : "Cell stash");
                row.Detail.text = "Contraband · " + item.Source + " · " + item.Uses + " use" + (item.Uses == 1 ? "" : "s") + (item.Kind == "Phone" && item.Place != "Held" ? "\nUnavailable until collected in person." : "");
            }
            characterPhoneItemsPrevious.style.display = pages > 1 ? DisplayStyle.Flex : DisplayStyle.None; characterPhoneItemsNext.style.display = pages > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneItemsPrevious.SetEnabled(characterPhoneItemPage > 0); characterPhoneItemsNext.SetEnabled(characterPhoneItemPage + 1 < pages);
        }

        void LayoutCharacterPhone(float left, float right, float top, float bottom)
        {
            if (characterPhone == null) return;
            if (!CharacterPhoneOpen) { characterPhone.style.display = DisplayStyle.None; return; }
            float availableWidth = Mathf.Max(1, root.resolvedStyle.width - left - right);
            float availableHeight = Mathf.Max(1, root.resolvedStyle.height - top - bottom);
            characterPhone.style.right = right; characterPhone.style.top = top; characterPhone.style.width = Mathf.Min(410, availableWidth); characterPhone.style.height = Mathf.Min(720, availableHeight);
            characterPhoneShort = availableHeight < 480; characterPhone.EnableInClassList("character-phone-short", characterPhoneShort);
        }
#if UNITY_EDITOR
        public bool PreviewCharacterPhoneFits { get { if (characterPhone == null || !CharacterPhoneOpen) return false; Rect b = characterPhone.worldBound, r = root.worldBound; return b.xMin >= r.xMin - 1 && b.xMax <= r.xMax + 1 && b.yMin >= r.yMin - 1 && b.yMax <= r.yMax + 1; } }
        public string PreviewCharacterPhoneText { get { return characterPhoneDevice == null ? "" : characterPhoneDevice.text + " | " + characterPhoneAccessText.text + " | " + characterPhoneLocation.text; } }
        public void PreviewCharacterPhoneApp(int app) { ShowCharacterPhoneApp((CharacterPhoneApp)Mathf.Clamp(app, 0, 3)); }
        public bool PreviewCharacterPhoneTargetsFit { get { return characterPhone != null && characterPhone.Query<Button>().ToList().Where(b => b.resolvedStyle.display != DisplayStyle.None && b.worldBound.height > 0).All(b => b.worldBound.height >= 43 && b.worldBound.xMin >= characterPhone.worldBound.xMin - 1 && b.worldBound.xMax <= characterPhone.worldBound.xMax + 1); } }
#endif
    }
}
