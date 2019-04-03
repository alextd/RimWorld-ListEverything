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
	class ExposeableList<T> : IExposable
	{
		public List<T> internalList = new List<T>();
		public string exposeString = "internalList";
		public LookMode mode = LookMode.Deep;

		public void ExposeData() =>
			Scribe_Collections.Look(ref internalList, exposeString, mode);

		public static implicit operator ExposeableList<T>(List<T> list) =>
			new ExposeableList<T>() { internalList = list };

		public static implicit operator List<T>(ExposeableList<T> exL) =>
			exL.internalList;
	}


	class Settings : ModSettings
	{
		public Dictionary<string, ExposeableList<ListFilter>> savedFilters = new Dictionary<string, ExposeableList<ListFilter>>();

		public static Settings Get()
		{
			return LoadedModManager.GetMod<Mod>().GetSettings<Settings>();
		}

		public void Save(string name, List<ListFilter> filters)
		{
			savedFilters[name] = filters.ToList();
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
			Log.Message($"ExposeData: {Scribe.mode}");
			Scribe_Collections.Look(ref savedFilters, "savedFilters", LookMode.Value, LookMode.Deep);
		}
	}
}