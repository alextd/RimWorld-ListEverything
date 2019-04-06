using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace List_Everything
{
	public class ListFilterSelection : ListFilter
	{
		public ListFilter selected;

		public override void PostMake() =>
			selected = ListFilterMaker.MakeFilter(def.subFilters.First());

		public override bool FilterApplies(Thing thing) =>
			selected.FilterApplies(thing);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref selected, "selected");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterSelection clone = (ListFilterSelection)base.Clone(map);
			clone.selected = selected.Clone(map);
			return clone;
		}

		public override bool DrawOption(Rect rect)
		{
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			if (row.ButtonText(selected.def.LabelCap))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (ListFilterDef def in def.subFilters)
					options.Add(new FloatMenuOption(def.LabelCap, () => selected = ListFilterMaker.MakeFilter(def)));
				MainTabWindow_List.DoFloatMenu(options);

				return true;
			}
			rect.xMin += row.FinalX;
			return selected.DrawOption(rect);
		}
		public override bool DrawMore(Listing_StandardIndent listing) =>
			selected.DrawMore(listing);
	}
}
