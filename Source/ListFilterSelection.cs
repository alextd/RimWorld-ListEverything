using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace List_Everything
{
	partial class ListFilter	// so I can grab at private fields inside sel below
	{
		public class ListFilterSelection : ListFilterWithOption<ListFilter>, IFilterOwner
		{
			public IEnumerable<ListFilterDef> SubFilters => (def as ListFilterListDef).SubFilters;

			public override void PostMake() => SetSelectedFilter(SubFilters.First());

			public void SetSelectedFilter(ListFilterDef def)
			{
				sel = ListFilterMaker.MakeFilter(def, owner);
			}

			protected override bool FilterApplies(Thing thing) =>
				sel.FilterApplies(thing);

			public override ListFilter Clone(IFilterOwner newOwner)
			{
				ListFilterSelection clone = (ListFilterSelection)base.Clone(newOwner);

				clone.sel = sel.Clone(clone);

				return clone;
			}
			public override void DoResolveReference(Map map)
			{
				sel.DoResolveReference(map);
			}

			protected override bool DrawMain(Rect rect)
			{
				WidgetRow row = new WidgetRow(rect.x, rect.y);
				if (row.ButtonText(sel.def.LabelCap))
				{
					List<FloatMenuOption> options = new List<FloatMenuOption>();
					foreach (ListFilterDef def in SubFilters)
						options.Add(new FloatMenuOption(def.LabelCap, () => SetSelectedFilter(def)));
					MainTabWindow_List.DoFloatMenu(options);

					return true;
				}
				rect.xMin += row.FinalX;
				return sel.DrawMain(rect);
			}
			protected override bool DrawUnder(Listing_StandardIndent listing) =>
				sel.DrawUnder(listing);

			public override bool ValidForAllMaps =>
				sel.ValidForAllMaps;
		}
	}
}