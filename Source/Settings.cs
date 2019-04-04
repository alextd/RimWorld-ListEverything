using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace List_Everything
{
	class Settings : ModSettings
	{
		public Dictionary<string, FindDescription> savedFilters = new Dictionary<string, FindDescription>();

		public static Settings Get()
		{
			return LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
		}

		public bool Has(string name)
		{
			return savedFilters.ContainsKey(name);
		}

		public void Save(string name, FindDescription desc)
		{
			savedFilters[name] = new FindDescription()
			{
				filters = desc.filters.Select(f => f.Clone()).ToList(),
				baseType = desc.baseType
			};
			Write();
		}

		public void DoWindowContents(Rect wrect)
		{
			var listing = new Listing_Standard();
			listing.Begin(wrect);

			listing.Label("Saved list filters:");
			string remove = null;
			foreach(string name in savedFilters.Keys)
				if (listing.ButtonTextLabeled(name, "Delete"))
					remove = name;

			if (remove != null)
				savedFilters.Remove(remove);

			listing.End();
		}


		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedFilters, "savedFilters");
		}
	}
}