﻿using System;
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

		public void Rename(string name, string newName)
		{
			FindDescription desc = savedFilters[name];
			desc.name = newName;
			savedFilters[newName] = desc;
			savedFilters.Remove(name);
		}


		private const float RowHeight = WidgetRow.IconSize + 6;

		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		public void DoWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Text.Font = GameFont.Medium;
			listing.Label($"Saved Find Filters:");
			Text.Font = GameFont.Small;
			listing.GapLine();
			listing.End();

			inRect.yMin += listing.CurHeight;
			

			//Scrolling!
			Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

			Rect rowRect = viewRect; rowRect.height = RowHeight;
			string remove = null;
			foreach (var kvp in savedFilters)
			{
				string name = kvp.Key;
				FindDescription desc = kvp.Value;

				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);
				rowRect.y += RowHeight;

				row.Label(name, rowRect.width / 4);

				if (row.ButtonText("Rename"))
					Find.WindowStack.Add(new Dialog_Name(newName => Rename(name, newName)));

				if (row.ButtonText("Load"))
					MainTabWindow_List.OpenWith(desc.Clone(Find.CurrentMap));

				if (row.ButtonText("Delete"))
					remove = name;

				row.CheckboxLabeled("All maps?", ref desc.allMaps);
			}
			scrollViewHeight = RowHeight * savedFilters.Count();
			Widgets.EndScrollView();

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