using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace List_Everything
{
	public class ListFilterSelection : ListFilterWithOption<ListFilter>
	{
		public override void PostMake() =>
			sel = ListFilterMaker.MakeFilter(def.subFilters.First());

		public override bool FilterApplies(Thing thing) =>
			sel.FilterApplies(thing);

		public override bool DrawOption(Rect rect)
		{
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			if (row.ButtonText(sel.def.LabelCap))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (ListFilterDef def in def.subFilters)
					options.Add(new FloatMenuOption(def.LabelCap, () => sel = ListFilterMaker.MakeFilter(def)));
				MainTabWindow_List.DoFloatMenu(options);

				return true;
			}
			rect.xMin += row.FinalX;
			return sel.DrawOption(rect);
		}
		public override bool DrawMore(Listing_StandardIndent listing) =>
			sel.DrawMore(listing);
	}
}
