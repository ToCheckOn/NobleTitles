using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace NobleTitles
{
    internal sealed class TitleBehavior : CampaignBehaviorBase
    {
        private readonly AccessTools.FieldRef<Clan, int> _tier = AccessTools.FieldRefAccess<Clan, int>("_tier");

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnGameLoaded));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnBeforeSave);
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, OnDailyTickClan);
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

            RelationshipGainsDB.Init();
        }

        private void OnDailyTickClan(Clan clan)
        {
            if (clan.Leader == null || clan.IsEliminated || clan.Kingdom == null) return;

            DailyClanTitleResolve(clan.Leader);

            var spouse = clan.Leader.Spouse;

            if (spouse == null ||
                spouse.IsDead ||
                (spouse.Clan?.Leader == spouse && spouse.Clan.Kingdom != null))
                return;

            DailyClanTitleResolve(spouse);
        }

        private void DailyClanTitleResolve(Hero hero)
        {
            var name = hero.Name.ToString();
            var firstName = hero.FirstName.ToString();

            if (assignedTitles.TryGetValue(hero, out string oldTitlePrefix))
            {
                if (name.StartsWith(oldTitlePrefix))
                {
                    //Util.Log.Print($">> WARNING: Tried to add title \"{oldTitlePrefix}\" to hero \"{hero.Name}\" with pre-assigned title \"{oldTitlePrefix}\"");
                    return;
                }

                hero.SetName(new TextObject(oldTitlePrefix + name), new TextObject(firstName));
            }
        }

        private void OnDailyTick()
        {
            // Remove and unregister all titles from living heroes
            RemoveTitlesFromLivingHeroes();
            
            // Now add currently applicable titles to living heroes
            AddTitlesToLivingHeroes();

            // Apply Renown change to kingdomless clans
            HandleRenownOfKingdomlessClans();

            // Apply relationship changes
            HandleRelationshipChanges();
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

                if (GetFiefScore(clan) <= settings.baronTreshold)
                {
                    HandleRenownDeteriotation(clan, settings.noneMaxRenown);
                }
                else if(GetFiefScore(clan) > settings.baronTreshold && GetFiefScore(clan) < settings.countTreshold)
                {
                    HandleRenownDeteriotation(clan, settings.baronMaxRenown);
                }
                else if(GetFiefScore(clan) >= settings.countTreshold && GetFiefScore(clan) < settings.dukeTreshold)
                {
                    HandleRenownDeteriotation(clan, settings.countMaxRenown);
                }
                else if(GetFiefScore(clan) >= settings.dukeTreshold)
                {
                    HandleRenownDeteriotation(clan, settings.dukeMaxRenown);
                }
            }
        }

        private void HandleRelationshipChanges()
        {
            foreach (var relationshipModel in kingdomRelationshipMap.Values)
                relationshipModel.HandleRelationship();

            kingdomRelationshipMap.Clear();
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

            var nobles = kingdom.Clans
                .Where(c =>
                    !c.IsClanTypeMercenary &&
                    !c.IsUnderMercenaryService &&
                    c.Leader != null &&
                    c.Leader.IsAlive)
                .SelectMany(c => c.Lords)
                .ToList();

            kingdomRelationshipMap[kingdom] = new RelationshipModel(kingdom.Leader);

            // Pass over poor schmucks
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) <= settings.baronTreshold && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetLordTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "LORD"));

                HandleRenownDeteriotation(h.Clan, settings.noneMaxRenown);
            }
            // Pass over all barons.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) > settings.baronTreshold && GetFiefScore(v.Clan) < settings.countTreshold && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetBaronTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "BARON"));

                HandleRenownDeteriotation(h.Clan, settings.baronMaxRenown);

            }
            // Pass over all counts.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) >= settings.countTreshold && GetFiefScore(v.Clan) < settings.dukeTreshold && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetCountTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "COUNT"));

                HandleRenownDeteriotation(h.Clan, settings.countMaxRenown);

            }
            // Pass over all dukes.
            foreach (var h in vassals.Where(v => GetFiefScore(v.Clan) >= settings.dukeTreshold && !v.IsKingdomLeader))
            {
                AssignRulerTitle(h, titleDb.GetDukeTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "DUKE"));

                HandleRenownDeteriotation(h.Clan, settings.dukeMaxRenown);

            }
            // Pass overr all noble clan members
            foreach(var h in nobles.Where(n => n.Clan.Leader != n
                                               && (n.Clan.Leader.Spouse != null ? n != n.Clan.Leader.Spouse : true)
                                               ))
            {
                AssignRulerTitle(h, titleDb.GetLordTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(h, "LORD"));
            }

            // Finally, the most obvious, the ruler (King) title:
            if (kingdom.Leader != null &&
                !Kingdom.All.Where(k => k != kingdom).SelectMany(k => k.Lords).Where(h => h == kingdom.Leader).Any()) // fix for stale ruler status in defunct kingdoms
            {
                AssignRulerTitle(kingdom.Leader, titleDb.GetKingTitle(kingdom.Culture));
                tr.Add(GetHeroTrace(kingdom.Leader, "KING"));

                HandleRenownDeteriotation(kingdom.Leader.Clan, settings.kingMaxRenown);
            }

            Util.Log.Print(tr);
        }

        private void HandleRenownDeteriotation(Clan clan, int maxRenown)
        {
            if(!settings.enableRenownDeteriotationModule) return;

            if (clan.Renown > maxRenown && !(clan.Kingdom != null && clan == clan.Kingdom.RulingClan))
            {
                var renownLoss = (float)Math.Min(clan.Renown * settings.renownDeteriotationMultiplier, clan.Renown - maxRenown);
                clan.Renown -= renownLoss;

                if(settings.enableRelationModule && clan.Kingdom != null && renownLoss > settings.renownLossThresholdForRelationshipChange)
                {
                    kingdomRelationshipMap[clan.Kingdom].AddVassalState(clan.Leader, false);
                }
                
                var num = Campaign.Current.Models.ClanTierModel.CalculateTier(clan);
                if (num != clan.Tier)
                {
                    _tier(clan) = num;
                    CampaignEventDispatcher.Instance.OnClanTierChanged(clan, clan.Leader.IsHumanPlayerCharacter);
                }
            }
            else if (clan.Renown < maxRenown)
            {
                var renownGain = (float)Math.Min(settings.renownIncreaseAmount, maxRenown + clan.Renown);
                clan.Renown += renownGain;

                if (settings.enableRelationModule && clan.Kingdom != null)
                {
                    kingdomRelationshipMap[clan.Kingdom].AddVassalState(clan.Leader, true);
                }

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

        private int GetFiefScore(Clan clan) => clan.Fiefs.Sum(t => t.IsTown ? settings.townScore : settings.castleScore) + (clan.Villages.Count() * settings.villageScore);

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
            if (!settings.enableNobleTitlesModule) return;

            var name = hero.Name.ToString();
            var firstName = hero.FirstName.ToString();

            if (assignedTitles.TryGetValue(hero, out string oldTitlePrefix))
            {
                if (overrideTitle && !titlePrefix.Equals(oldTitlePrefix) && name.StartsWith(oldTitlePrefix))
                    RemoveTitleFromHero(hero);
                else if (!overrideTitle && name.StartsWith(oldTitlePrefix))
                {
                    Util.Log.Print($">> WARNING: Tried to add title \"{titlePrefix}\" to hero \"{hero.Name}\" with pre-assigned title \"{oldTitlePrefix}\"");
                    return;
                }
            }

            if (registerTitle)
                assignedTitles[hero] = titlePrefix;

            
            hero.SetName(new TextObject(titlePrefix + name), new TextObject(firstName));
        }

        private void RemoveTitlesFromLivingHeroes(bool unregisterTitles = true)
        {
            Util.Log.Print($"Removing noble titles...");
            foreach (var h in assignedTitles.Keys.Where(h => h.IsAlive).ToList())
                RemoveTitleFromHero(h, unregisterTitles);
        }

        private void RemoveTitleFromHero(Hero hero, bool unregisterTitle = true)
        {
            if (!settings.enableNobleTitlesModule) return;

            var name = hero.Name.ToString();
            var firstName = hero.FirstName.ToString();
            var title = assignedTitles[hero];

            if (!name.StartsWith(title))
            {
                Util.Log.Print($">> WARNING: Expected title prefix not found in hero name when removing title, removing the reference! Title prefix: \"{title}\" | Name: \"{name}\"");
                return;
            }

            if (unregisterTitle)
                assignedTitles.Remove(hero);

            hero.SetName(new TextObject(name.Remove(0, title.Length)), new TextObject(firstName));
        }

        private readonly Dictionary<Hero, string> assignedTitles = new Dictionary<Hero, string>();

        private Dictionary<Kingdom, RelationshipModel> kingdomRelationshipMap = new Dictionary<Kingdom, RelationshipModel>();

        private readonly TitleDb titleDb = new TitleDb();

        private Settings settings = Settings.Load();

        private Dictionary<string, string>? savedDeadTitles; // Maps a Hero's string ID to a static title prefix for dead heroes, only used for (de)serialization

        private int saveVersion = 0;

        private const int CurrentSaveVersion = 2;
    }
}
