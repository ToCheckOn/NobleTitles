using Newtonsoft.Json;
using static NobleTitles.TitleDb;
using TaleWorlds.Library;
using System.IO;

namespace NobleTitles
{
    public class Settings
    {
        public int noneMaxRenown;
        public int baronMaxRenown;
        public int countMaxRenown;
        public int dukeMaxRenown;
        public int kingMaxRenown;

        public int townScore;
        public int castleScore;
        public int villageScore;

        public int baronTreshold;
        public int countTreshold;
        public int dukeTreshold;

        public float renownDeteriotationMultiplier;
        public float renownIncreaseAmount;

        public float renownLossThresholdForRelationshipChange;

        public bool enableRenownDeteriotationModule;
        public bool enableNobleTitlesModule;
        public bool enableRelationModule;

        public Settings(int noneMaxRenown, int baronMaxRenown, int countMaxRenown, int dukeMaxRenown, int kingMaxRenown,
                        int townScore, int castleScore, int villageScore,
                        int baronTreshold, int countTreshold, int dukeTreshold,
                        float renownDeteriotationMultiplier, float renownIncreaseAmount,
                        float renownLossThresholdForRelationshipChange,
                        bool enableRenownDegradation, bool enableNobleTitles, bool enableRelationModule)
        {
            this.noneMaxRenown = noneMaxRenown;
            this.baronMaxRenown = baronMaxRenown;
            this.countMaxRenown = countMaxRenown;
            this.dukeMaxRenown = dukeMaxRenown;
            this.kingMaxRenown = kingMaxRenown;

            this.townScore = townScore;
            this.castleScore = castleScore;
            this.villageScore = villageScore;

            this.baronTreshold = baronTreshold;
            this.countTreshold = countTreshold;
            this.dukeTreshold = dukeTreshold;

            this.renownDeteriotationMultiplier = renownDeteriotationMultiplier;
            this.renownIncreaseAmount = renownIncreaseAmount;

            this.renownLossThresholdForRelationshipChange = renownLossThresholdForRelationshipChange;

            this.enableRenownDeteriotationModule = enableRenownDegradation;
            this.enableNobleTitlesModule = enableNobleTitles;
            this.enableRelationModule = enableRelationModule;
        }

        public static Settings Load()
        {
            var settingsPath = BasePath.Name + $"Modules/{SubModule.Name}/settings.json";

            Settings settings = JsonConvert.DeserializeObject<Settings>(
                File.ReadAllText(settingsPath),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace })
                ?? throw new BadTitleDatabaseException("Failed to deserialize settings!");

            return settings;
        }
    }
}
