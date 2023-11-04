using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace NobleTitles
{
    public class RelationshipModel
    {
        private Hero ruler;
        private List<(Hero vassal, bool gainedRenown)> vassalStates;

        private readonly int HonorWeight = 3;
        private readonly int MercyWeight = 4;
        private readonly int ValorWeight = 2;
        private readonly int GenerosityWeight = 1;
        private readonly int CalculatingWeight = 2;

        private readonly int MismatchScoreWeight = 2;

        public RelationshipModel(Hero ruler)
        {
            this.ruler = ruler;

            vassalStates = new List<(Hero vassal, bool gainedRenown)>();
        }

        public void HandleRelationship()
        {
            foreach(var vassalState in vassalStates.Where(v => !v.vassal.IsHumanPlayerCharacter))
                HandleRelationshipWithTheRuler(vassalState);
        }

        private void HandleRelationshipWithTheRuler((Hero vassal, bool gainedRenown) vassalState)
        {
            var score = 0;

            score += GetOwnGotScore(vassalState.vassal.GetHeroTraits(), vassalState.gainedRenown);

            foreach (var vs in vassalStates.Where(x => x.vassal != vassalState.vassal))
            {
                var matchScore = GetVassalMisMatchScore(vassalState.vassal.GetHeroTraits(), vs.vassal.GetHeroTraits());
                matchScore = CharacterRelationManager.GetHeroRelation(vassalState.vassal, vs.vassal) - (matchScore * MismatchScoreWeight);

                if (matchScore > 5)
                    matchScore = 1;
                else if (matchScore < -5)
                    matchScore = -1;
                else
                    matchScore = 0;

                score += GetOtherGotScore(vassalState.vassal.GetHeroTraits(), matchScore, vs.gainedRenown);
            }

            score = score > 0 ? 1 : score < 0 ? -1 : 0;
            var newRelationship = CharacterRelationManager.GetHeroRelation(vassalState.vassal, ruler) + score;

            CharacterRelationManager.SetHeroRelation(vassalState.vassal, ruler, newRelationship);

            Util.Log.Print(GetVassalTrace(vassalState.vassal, vassalState.gainedRenown, score, true));
        }

        private int GetVassalMisMatchScore(CharacterTraits vassalTraits, CharacterTraits vassalOtherTraits)
        {
            var honorScore = Math.Abs(vassalTraits.Honor - vassalOtherTraits.Honor) * HonorWeight;
            var mercyScore = Math.Abs(vassalTraits.Mercy - vassalOtherTraits.Mercy) * MercyWeight;
            var valorScore = Math.Abs(vassalTraits.Valor - vassalOtherTraits.Valor) * ValorWeight;
            var generosityScore = Math.Abs(vassalTraits.Generosity - vassalOtherTraits.Generosity) * GenerosityWeight;
            var calculatingScore = Math.Abs(vassalTraits.Calculating - vassalOtherTraits.Calculating) * CalculatingWeight;

            return honorScore + mercyScore + valorScore + generosityScore + calculatingScore;
        }

        private int GetOwnGotScore(CharacterTraits vassalTraits, bool gainedRenown)
        {
            var score = gainedRenown ? 1 : -1;
            var caseStr = gainedRenown ? "IGot" : "ILost";

            foreach(var trait in GetPersonality(vassalTraits))
            {
                score += RelationshipGainsDB.RelationshipGains[trait.name][caseStr] * Math.Abs(trait.weight);
            }

            return score;
        }

        private int GetOtherGotScore(CharacterTraits vassalTraits, int matchScore, bool otherGainedRenown)
        {
            var score = 0;
            var caseStr = "";

            switch(matchScore)
            {
                case 1:
                    caseStr = "SmbdMatch";
                    break;
                case -1:
                    caseStr = "SmbdMisMatch";
                    break;
                case 0:
                    caseStr = "Smbd";
                    break;
            }

            caseStr += otherGainedRenown ? "Got" : "Lost";
            
            foreach(var trait in GetPersonality(vassalTraits))
            {
                score += RelationshipGainsDB.RelationshipGains[trait.name][caseStr] * Math.Abs(trait.weight);
            }

            return score;
        }

        private List<(string name, int weight)> GetPersonality(CharacterTraits traits)
        {
            var personality = new List<(string trait, int weight)>();

            (string name, int weight) generosity = traits.Generosity > 0 ? ("Generous", traits.Generosity) : ("Closefisted", traits.Generosity);
            (string name, int weight) honor = traits.Honor > 0 ? ("Honorable", traits.Honor) : ("Devious", traits.Honor);
            (string name, int weight) calculating = traits.Calculating > 0 ? ("Calculating", traits.Calculating) : ("Impulsive", traits.Calculating);

            personality.Add(generosity);
            personality.Add(honor);
            personality.Add(calculating);

            return personality;
        }

        private string GetVassalTrace(Hero vassal, bool gainedRenown, int score, bool total)
            => $"Ruler: {ruler.Name} / {vassal.Name} / Gained Renown: {gainedRenown} / Score: {score} / Total: {total}";

        public void AddVassalState(Hero vassal, bool gainedRenown)
        {
            vassalStates.Add((vassal, gainedRenown));
        }
    }
}
