﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace NobleTitles
{
	class TitleDb
	{
		internal string GetKingTitlePrefix(CultureObject culture, bool isFemale)
		{
			if (culture == null || !cultureMap.TryGetValue(culture.StringId, out CultureEntry culEntry))
				return null;

			return isFemale ? culEntry.King.Female : culEntry.King.Male;
		}

		internal string GetDukeTitlePrefix(CultureObject culture, bool isFemale)
		{
			if (culture == null || !cultureMap.TryGetValue(culture.StringId, out CultureEntry culEntry))
				return null;

			return isFemale ? culEntry.Duke.Female : culEntry.Duke.Male;
		}

		internal string GetCountTitlePrefix(CultureObject culture, bool isFemale)
		{
			if (culture == null || !cultureMap.TryGetValue(culture.StringId, out CultureEntry culEntry))
				return null;

			return isFemale ? culEntry.Count.Female : culEntry.Count.Male;
		}

		internal string GetBaronTitlePrefix(CultureObject culture, bool isFemale)
		{
			if (culture == null || !cultureMap.TryGetValue(culture.StringId, out CultureEntry culEntry))
				return null;

			return isFemale ? culEntry.Baron.Female : culEntry.Baron.Male;
		}

		internal TitleDb()
		{
			Path = BasePath.Name + $"Modules/{SubModule.Name}/titles.json";
			Deserialize();
		}

		internal void Deserialize()
		{
			cultureMap = JsonConvert.DeserializeObject<Dictionary<string, CultureEntry>>(
				File.ReadAllText(Path),
				new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }
				);

			if (cultureMap == null || cultureMap.Count == 0)
				throw new BadTitleDataException("Title database is empty!");

			foreach (var i in cultureMap)
			{
				var (cul, entry) = (i.Key, i.Value);

				// some basic validation first:

				if (entry.King == null || entry.Duke == null || entry.Count == null || entry.Baron == null)
					throw new BadTitleDataException($"All title types must be defined for culture '{cul}'!");

				if (entry.King.Male.IsStringNoneOrEmpty() || entry.Duke.Male.IsStringNoneOrEmpty() ||
					entry.Count.Male.IsStringNoneOrEmpty() || entry.Baron.Male.IsStringNoneOrEmpty())
					throw new BadTitleDataException($"Missing at least one male variant of a title type for culture '{cul}'");

				// missing feminine titles default to equivalent masculine/neutral titles:
				if (entry.King.Female.IsStringNoneOrEmpty())  entry.King.Female  = entry.King.Male;
				if (entry.Duke.Female.IsStringNoneOrEmpty())  entry.Duke.Female  = entry.Duke.Male;
				if (entry.Count.Female.IsStringNoneOrEmpty()) entry.Count.Female = entry.Count.Male;
				if (entry.Baron.Female.IsStringNoneOrEmpty()) entry.Baron.Female = entry.Baron.Male;

				// commonly, we want the full title prefix, i.e. including the trailing space, so we just use
				// such strings natively instead of constantly doing string creation churn just to append a space:
				entry.King.Male += ' ';
				entry.King.Female += ' ';
				entry.Duke.Male += ' ';
				entry.Duke.Female += ' ';
				entry.Count.Male += ' ';
				entry.Count.Female += ' ';
				entry.Baron.Male += ' ';
				entry.Baron.Female += ' ';
			}
		}

		internal void Serialize()
		{
			// undo our baked-in trailing space
			foreach (var e in cultureMap.Values)
			{
				e.King.Male = RmEndChar(e.King.Male);
				e.King.Female = RmEndChar(e.King.Female);
				e.Duke.Male = RmEndChar(e.Duke.Male);
				e.Duke.Female = RmEndChar(e.Duke.Female);
				e.Count.Male = RmEndChar(e.Count.Male);
				e.Count.Female = RmEndChar(e.Count.Female);
				e.Baron.Male = RmEndChar(e.Baron.Male);
				e.Baron.Female = RmEndChar(e.Baron.Female);
			}

			File.WriteAllText(Path, JsonConvert.SerializeObject(cultureMap, Formatting.Indented));
		}

		string RmEndChar(string s) => s.Substring(0, s.Length - 1);

		public class BadTitleDataException : Exception
		{
			public BadTitleDataException(string message) : base(message) { }
		}

		protected class CultureEntry
		{
			public Entry King = null;
			public Entry Duke = null;
			public Entry Count = null;
			public Entry Baron = null;

			public CultureEntry() { }
		}


		protected class Entry
		{
			public string Male = null;
			public string Female = null;

			public Entry() { }
		}

		protected string Path { get; private set; }

		// culture StringId => CultureEntry (contains bulk of title information, only further split by gender)
		protected Dictionary<string, CultureEntry> cultureMap;
	}
}