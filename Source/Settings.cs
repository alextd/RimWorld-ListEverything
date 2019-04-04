using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace List_Everything
{
	//So it can go inside a dictionary
	class SavedFilter : IExposable
	{
		public BaseListType baseType;
		public List<ListFilter> list = new List<ListFilter>();

		public void ExposeData()
		{
			Scribe_Values.Look(ref baseType, "baseType");
			Scribe_Collections.Look(ref list, "list");
		}
	}


	class Settings : ModSettings
	{
		public Dictionary<string, SavedFilter> savedFilters = new Dictionary<string, SavedFilter>();

		public static Settings Get()
		{
			return LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
		}

		public bool Has(string name)
		{
			return savedFilters.ContainsKey(name);
		}

		public void Save(string name, BaseListType baseType, List<ListFilter> filters)
		{
			savedFilters[name] = new SavedFilter()
			{
				list = filters.Select(f => f.Clone()).ToList(),
				baseType = baseType
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