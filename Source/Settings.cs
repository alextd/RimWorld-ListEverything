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
		private Dictionary<string, FindDescription> savedFilters = new Dictionary<string, FindDescription>();

		public static Settings Get()
		{
			return LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
		}

		public IEnumerable<string> SavedNames() => savedFilters.Keys;

		public bool Has(string name)
		{
			return savedFilters.ContainsKey(name);
		}

		public void Save(string name, FindDescription desc, bool overwrite = false)
		{
			if (!overwrite && Has(name))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite saved filter?",
					 () => Save(name, desc, true)));
			}
			else
			{
				FindDescription newDesc = desc.Clone(null); ;
				newDesc.name = name;
				savedFilters[name] = newDesc;
			}
			Write();
		}

		public FindDescription Load(string name)
		{
			return savedFilters[name].Clone(Find.CurrentMap);
		}

		public void DoWindowContents(Rect wrect)
		{
			var listing = new Listing_Standard();
			listing.Begin(wrect);

			listing.Label("Saved list filters:");
			string remove = null;
			foreach(string name in SavedNames())
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