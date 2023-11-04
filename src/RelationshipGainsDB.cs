using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NobleTitles
{
    public static class RelationshipGainsDB
    {
        public static Dictionary<string, Dictionary<string, int>> RelationshipGains = new Dictionary<string, Dictionary<string, int>>();

        public static void Init()
        {
            RelationshipGains["Generous"] = new Dictionary<string, int>();
            RelationshipGains["Closefisted"] = new Dictionary<string, int>();
            RelationshipGains["Honorable"] = new Dictionary<string, int>();
            RelationshipGains["Devious"] = new Dictionary<string, int>();
            RelationshipGains["Calculating"] = new Dictionary<string, int>();
            RelationshipGains["Impulsive"] = new Dictionary<string, int>();

            RelationshipGains["Generous"]["IGot"] = 1;
            RelationshipGains["Generous"]["ILost"] = 0;
            RelationshipGains["Generous"]["SmbdGot"] = 1;
            RelationshipGains["Generous"]["SmbdLost"] = -1;
            RelationshipGains["Generous"]["SmbdMatchGot"] = 2;
            RelationshipGains["Generous"]["SmbdMatchLost"] = -2;
            RelationshipGains["Generous"]["SmbdMisMatchGot"] = 0;
            RelationshipGains["Generous"]["SmbdMisMatchLost"] = 0;

            RelationshipGains["Closefisted"]["IGot"] = 2;
            RelationshipGains["Closefisted"]["ILost"] = -2;
            RelationshipGains["Closefisted"]["SmbdGot"] = -1;
            RelationshipGains["Closefisted"]["SmbdLost"] = 1;
            RelationshipGains["Closefisted"]["SmbdMatchGot"] = 0;
            RelationshipGains["Closefisted"]["SmbdMatchLost"] = 0;
            RelationshipGains["Closefisted"]["SmbdMisMatchGot"] = -2;
            RelationshipGains["Closefisted"]["SmbdMisMatchLost"] = 2;

            RelationshipGains["Honorable"]["IGot"] = 1;
            RelationshipGains["Honorable"]["ILost"] = -1;
            RelationshipGains["Honorable"]["SmbdGot"] = 0;
            RelationshipGains["Honorable"]["SmbdLost"] = 0;
            RelationshipGains["Honorable"]["SmbdMatchGot"] = 1;
            RelationshipGains["Honorable"]["SmbdMatchLost"] = -1;
            RelationshipGains["Honorable"]["SmbdMisMatchGot"] = -1;
            RelationshipGains["Honorable"]["SmbdMisMatchLost"] = 1;

            RelationshipGains["Devious"]["IGot"] = 1;
            RelationshipGains["Devious"]["ILost"] = -2;
            RelationshipGains["Devious"]["SmbdGot"] = 0;
            RelationshipGains["Devious"]["SmbdLost"] = 0;
            RelationshipGains["Devious"]["SmbdMatchGot"] = 1;
            RelationshipGains["Devious"]["SmbdMatchLost"] = -1;
            RelationshipGains["Devious"]["SmbdMisMatchGot"] = -2;
            RelationshipGains["Devious"]["SmbdMisMatchLost"] = 2;

            RelationshipGains["Calculating"]["IGot"] = 1;
            RelationshipGains["Calculating"]["ILost"] = 0;
            RelationshipGains["Calculating"]["SmbdGot"] = 1;
            RelationshipGains["Calculating"]["SmbdLost"] = 0;
            RelationshipGains["Calculating"]["SmbdMatchGot"] = 1;
            RelationshipGains["Calculating"]["SmbdMatchLost"] = -1;
            RelationshipGains["Calculating"]["SmbdMisMatchGot"] = 0;
            RelationshipGains["Calculating"]["SmbdMisMatchLost"] = 0;

            RelationshipGains["Impulsive"]["IGot"] = 3;
            RelationshipGains["Impulsive"]["ILost"] = -3;
            RelationshipGains["Impulsive"]["SmbdGot"] = 1;
            RelationshipGains["Impulsive"]["SmbdLost"] = -1;
            RelationshipGains["Impulsive"]["SmbdMatchGot"] = 3;
            RelationshipGains["Impulsive"]["SmbdMatchLost"] = -3;
            RelationshipGains["Impulsive"]["SmbdMisMatchGot"] = -3;
            RelationshipGains["Impulsive"]["SmbdMisMatchLost"] = 3;
        }
    }
}
