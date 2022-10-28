using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace List_Everything
{
	public interface IFilterOwner
	{
		public FindDescription RootFindDesc { get; }
		public void Add(ListFilter newFilter, bool remake = false);
		public IEnumerable<ListFilter> Filters { get; }
		public void RemoveAll(HashSet<ListFilter> removedFilters);
		public bool Check(Predicate<ListFilter> check);
	}
	public static class IFilterOwnerExtensions
	{
		public static bool DoFilters(this IFilterOwner owner, Listing_StandardIndent listing, bool locked)
		{
			bool changed = false;
			HashSet<ListFilter> removedFilters = new();
			foreach (ListFilter filter in owner.Filters)
			{
				Rect highlightRect = listing.GetRect(0);
				float heightBefore = listing.CurHeight;
				(bool ch, bool d) = filter.Listing(listing, locked);
				changed |= ch;
				if (d)
					removedFilters.Add(filter);

				// Highlight the filters that pass for selected objects (useful for "any" filters)
				if (!(filter is IFilterOwner) && Find.Selector.SelectedObjects.Any(o => o is Thing t && filter.AppliesTo(t)))
				{
					highlightRect.height = listing.CurHeight - heightBefore;
					Widgets.DrawHighlight(highlightRect);
				}
			}

			owner.RemoveAll(removedFilters);
			return changed;
		}

		public static void DrawAddRow(this IFilterOwner owner, Listing_StandardIndent listing)
		{
			Rect addRow = listing.GetRect(Text.LineHeight);
			listing.Gap(listing.verticalSpacing);

			Rect butRect = addRow; butRect.width = Text.LineHeight;
			Widgets.DrawTextureFitted(butRect, TexButton.Plus, 1.0f);

			Rect textRect = addRow; textRect.xMin += Text.LineHeight + WidgetRow.DefaultGap;
			Widgets.Label(textRect, "TD.AddNewFilter...".Translate());

			Widgets.DrawHighlightIfMouseover(addRow);

			if (Widgets.ButtonInvisible(addRow))
			{
				DoFloatAllFilters(owner);
			}
		}

		public static void DoFloatAllFilters(this IFilterOwner owner)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterSelectableDef def in ListFilterMaker.SelectableList)
			{
				if (def is ListFilterDef fDef)
					options.Add(new FloatMenuOption(
						fDef.LabelCap,
						() => owner.Add(ListFilterMaker.MakeFilter(fDef, owner), true)
					));
				if (def is ListFilterCategoryDef cDef)
					options.Add(new FloatMenuOption(
						"+ " + cDef.LabelCap,
						() => owner.DoFloatAllCategory(cDef)
					));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static void DoFloatAllCategory(this IFilterOwner owner, ListFilterCategoryDef cDef)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in cDef.SubFilters)
			{
				// I don't think we need to worry about double-nested filters
				options.Add(new FloatMenuOption(
					def.LabelCap,
					() => owner.Add(ListFilterMaker.MakeFilter(def, owner), true)
				));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
