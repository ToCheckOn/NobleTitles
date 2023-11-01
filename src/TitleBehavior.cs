using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace NobleTitles
{
    internal sealed class TitleBehavior : CampaignBehaviorBase
    {
        private readonly AccessTools.FieldRef<Clan, int> _tier = AccessTools.FieldRefAccess<Clan, int>("_tier");

        private int schmuckMaxRenown = 250;
        private int baronMaxRenown   = 625;
        private int countMaxRenown   = 1625;
        private int dukeMaxRenown    = 4250;
        private int kingMaxRenown    = 6150;

        private float renownDeteriotationMultiplier = 0.01f;
        private float renownIncreaseAmount = 5;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameLoaded));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
        }

        public override void SyncData(IDataStore dataStore)
        {
            string dtKey = $"{SubModule.Name}DeadTitles";
            string svKey = $"{SubModule.Name}SaveVersion";

            // Synchronize current savegame version:
            dataStore.SyncData(svKey, ref saveVersion);

            if (dataStore.IsSaving)
            {
                // Serializing dead heroes' titles:
                savedDeadTitles = new();

                foreach (var at in assignedTitles.Where(item => item.Key.IsDead))
                    savedDeadTitles[at.Key.StringId] = at.Value;

                string serialized = JsonConvert.SerializeObject(savedDeadTitles);
                dataStore.SyncData(dtKey, ref serialized);
                savedDeadTitles = null;
            }
            else if (saveVersion >= 2)
            {
                // Deserializing dead heroes' titles (will be applied in OnSessionLaunched):
                string? serialized = null;
                dataStore.SyncData(dtKey, ref serialized);

                if (string.IsNullOrEmpty(serialized))
                    return;

                savedDeadTitles = JsonConvert.DeserializeObject<Dictionary<string, string>>(serialized);
            }
            else
                Util.Log.Print($"Savegame version of {saveVersion}: skipping deserialization of dead noble titles...");
        }

        private void OnNewGameCreated(CampaignGameStarter starter) =>
            Util.Log.Print($"Starting new campaign on {SubModule.Name} v{SubModule.Version} with savegame version of {CurrentSaveVersion}...");

        private void OnGameLoaded(CampaignGameStarter starter) =>
            Util.Log.Print($"Loading campaign on {SubModule.Name} v{SubModule.Version} with savegame version of {saveVersion}...");

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            saveVersion = CurrentSaveVersion; // By now (and no later), it's safe to update the save to the latest savegame version

            AddTitlesToLivingHeroes();

            if (savedDeadTitles is null)
                return;

            foreach (var item in savedDeadTitles)
            {
                if (Campaign.Current.CampaignObjectManager.Find<Hero>(item.Key) is not Hero hero)
                {
                    Util.Log.Print($">> ERROR: Hero ID lookup failed for hero {item.Key} with title {item.Value}");
                    continue;
                }

                AddTitleToHero(hero, item.Value);
            }

            savedDeadTitles = null;
        }

        private void OnDailyTick()
        {
            // Remove and unregister all titles from living heroes
            RemoveTitlesFromLivingHeroes();

            // Now add currently applicable titles to living heroes
            AddTitlesToLivingHeroes();

            HandleRenownOfKingdomlessClans();
        }

        // Leave no trace in the save. Remove all titles from all heroes. Keep their assignment records.
        private void OnBeforeSave()
        {
            Util.Log.Print($"{nameof(OnBeforeSave)}: Temporarily removing title prefixes from all heroes...");

            foreach (var at in assignedTitles)
                RemoveTitleFromHero(at.Key, unregisterTitle: false);
        }

        internal void OnAfterSave() // Called from a Harmony patch rather than event dispatch
        {
            Util.Log.Print($"{nameof(OnAfterSave)}: Restoring title prefixes to all heroes...");

            // Restore all title prefixes to all heroes using the still-existing assignment records.
            foreach (var at in assignedTitles)
                AddTitleToHero(at.Key, at.Value, overrideTitle: true, registerTitle: false);
        }

        private void AddTitlesToLivingHeroes()
        {
            // All living, titled heroes are associated with kingdoms for now, so go straight to the source
            Util.Log.Print("Adding kingdom-based noble titles...");

            foreach (var k in Kingdom.All.Where(x => !x.IsEliminated))
                AddTitlesToKingdomHeroes(k);
        }

        private void HandleRenownOfKingdomlessClans()
        {
            foreach (var clan in Campaign.Current.Clans.Where(clan => clan.Kingdom == null))
            {
                if (clan.IsEliminated || clan.Leader == null) continue;

                if (GetFiefScore(clan) <= 0)
                {
                    HandleRenownDeteriotation(clan, schmuckMaxRenown);
                }
                else if(GetFiefScore(clan) > 0 && GetFiefScore(clan) < 9)
                {
                    HandleRenownDeteriotation(clan, baronMaxRenown);
                }
                else if(GetFiefScore(clan) >= 9 && GetFiefScore(clan) < 20)
                {
                    HandleRenownDeteriotation(clan, countMaxRenown);
                }
                else if(GetFiefScore(clan) >= 20)
                {
                    HandleRenownDeteriotation(clan, dukeMaxRenown);
                }
            }
        }

        private void AddTitlesToKingdomHeroes(Kingdom kingdom)
        {
            var tr = new List<string> { $"Adding noble titles to {kingdom.Name}..." };

            /* The vassals first...
             *
             * We consider all noble, active vassal clans and sort them by their "fief score" and, as a tie-breaker,
             * their renown in ascending order (weakest -> strongest). For the fief score, 3 castles = 1 town.
             * Finally, we select the ordered list of their leaders.
             */

            var vassals = kingdom.Clans
                .Where(c =>
                    c != kingdom.RulingClan &&
                    !c.IsClanTypeMercenary &&
                    !c.IsUnderMercenaryService &&
                    c.Leader != null &&
                    c.Leader.IsAlive)
                .OrderBy(c => GetFiefScore(c))
                .ThenBy(c => c.Renown)
                .Select(c => c.Leader)
                .ToList();

            // Pass over poor schmucks 
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) <= 0 && !v.IsKingdomLeader))
            {
                HandleRenownDeteriotation(h.Clan, schmuckMaxRenown);
            }
            // Pass over all barons.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) > 0 && GetFiefScore(v.Clan) < 9 && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetBaronTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "BARON"));

                HandleRenownDeteriotation(h.Clan, baronMaxRenown);

            }
            // Pass over all counts.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) < 20 && GetFiefScore(v.Clan) >= 9 && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetCountTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "COUNT"));

                HandleRenownDeteriotation(h.Clan, countMaxRenown);

            }
            // Pass over all dukes.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) >= 20 && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetDukeTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "DUKE"));

                HandleRenownDeteriotation(h.Clan, dukeMaxRenown);

            }

            // Finally, the most obvious, the ruler (King) title:
            if (kingdom.Leader != null &&
                !Kingdom.All.Where(k => k != kingdom).SelectMany(k => k.Lords).Where(h => h == kingdom.Leader).Any()) // fix for stale ruler status in defunct kingdoms
            {
                AssignRulerTitle(kingdom.Leader, titleDb.GetKingTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(kingdom.Leader, "KING"));

                HandleRenownDeteriotation(kingdom.Leader.Clan, kingMaxRenown);
            }

            Util.Log.Print(tr);
        }

        private void HandleRenownDeteriotation(Clan clan, int maxRenown)
        {
            if (clan.Renown > maxRenown && !(clan.Kingdom != null && clan == clan.Kingdom.RulingClan))
            {
                clan.Renown -= (float)Math.Min(clan.Renown * renownDeteriotationMultiplier, clan.Renown - maxRenown);
                var num = Campaign.Current.Models.ClanTierModel.CalculateTier(clan);
                if (num != clan.Tier)
                {
                    _tier(clan) = num;
                    CampaignEventDispatcher.Instance.OnClanTierChanged(clan, clan.Leader.IsHumanPlayerCharacter);
                }
            }
            else if (clan.Renown < maxRenown)
            {
                clan.Renown += (float)Math.Min(renownIncreaseAmount, maxRenown + clan.Renown);
                var num = Campaign.Current.Models.ClanTierModel.CalculateTier(clan);
                if (num != clan.Tier)
                {
                    _tier(clan) = num;
                    CampaignEventDispatcher.Instance.OnClanTierChanged(clan, clan.Leader.IsHumanPlayerCharacter);
                }
            }
        }

        private string GetHeroTrace(Hero h, string rank) =>
            $" -> {rank}: {h.Name} [Fief Score: {GetFiefScore(h.Clan)} / Renown: {h.Clan.Renown:F0}]";

        private int GetFiefScore(Clan clan) => clan.Fiefs.Sum(t => t.IsTown ? 7 : 2) + clan.Villages.Count();

        private void AssignRulerTitle(Hero hero, TitleDb.Entry title)
        {
            var titlePrefix = hero.IsFemale ? title.Female : title.Male;
            AddTitleToHero(hero, titlePrefix);

            // Should their spouse also get the same title (after gender adjustment)?
            // If the spouse is the leader of a clan (as we currently assume `hero` is a clan leader too,
            //     it'd also be a different clan) and that clan belongs to any kingdom, no.
            // Else, yes.

            var spouse = hero.Spouse;

            if (spouse == null ||
                spouse.IsDead ||
                (spouse.Clan?.Leader == spouse && spouse.Clan.Kingdom != null))
                return;

            // Sure. Give the spouse the ruler consort title, which is currently and probably always will
            // be the same as the ruler title, adjusted for gender.

            titlePrefix = spouse.IsFemale ? title.Female : title.Male;
            AddTitleToHero(spouse, titlePrefix);
        }

        private void AddTitleToHero(Hero hero, string titlePrefix, bool overrideTitle = false, bool registerTitle = true)
        {
            if (assignedTitles.TryGetValue(hero, out string oldTitlePrefix))
            {
                if (overrideTitle && !titlePrefix.Equals(oldTitlePrefix))
                    RemoveTitleFromHero(hero);
                else if (!overrideTitle)
                {
                    Util.Log.Print($">> WARNING: Tried to add title \"{titlePrefix}\" to hero \"{hero.Name}\" with pre-assigned title \"{oldTitlePrefix}\"");
                    return;
                }
            }

            if (registerTitle)
                assignedTitles[hero] = titlePrefix;

            var name = hero.Name.ToString();
            var firstName = hero.FirstName.ToString();
            hero.SetName(new TextObject(titlePrefix + name), new TextObject(firstName));
        }

        private void RemoveTitlesFromLivingHeroes(bool unregisterTitles = true)
        {
            foreach (var h in assignedTitles.Keys.Where(h => h.IsAlive).ToList())
                RemoveTitleFromHero(h, unregisterTitles);
        }

        private void RemoveTitleFromHero(Hero hero, bool unregisterTitle = true)
        {
            var name = hero.Name.ToString();
            var firstName = hero.FirstName.ToString();
            var title = assignedTitles[hero];

            if (!name.StartsWith(title))
            {
                Util.Log.Print($">> WARNING: Expected title prefix not found in hero name when removing title! Title prefix: \"{title}\" | Name: \"{name}\"");
                return;
            }

            if (unregisterTitle)
                assignedTitles.Remove(hero);

            hero.SetName(new TextObject(name.Remove(0, title.Length)), new TextObject(firstName));
        }

        private readonly Dictionary<Hero, string> assignedTitles = new Dictionary<Hero, string>();

        private readonly TitleDb titleDb = new TitleDb();

        private Dictionary<string, string>? savedDeadTitles; // Maps a Hero's string ID to a static title prefix for dead heroes, only used for (de)serialization

        private int saveVersion = 0;

        private const int CurrentSaveVersion = 2;
    }
}
