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
	}
	public interface IFilterOwnerAdder : IFilterOwner
	{
		public void Add(ListFilter newFilter);
	}
	public static class IFilterOwnerAdderExtensions
	{
		public static void DrawAddRow(this IFilterOwnerAdder owner, Listing_StandardIndent listing)
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

		public static void DoFloatAllFilters(this IFilterOwnerAdder owner)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterSelectableDef def in ListFilterMaker.SelectableList)
			{
				if (def is ListFilterDef fDef)
					options.Add(new FloatMenuOption(
						fDef.LabelCap,
						() => owner.Add(ListFilterMaker.MakeFilter(fDef, owner))
					));
				if (def is ListFilterCategoryDef cDef)
					options.Add(new FloatMenuOption(
						"+ " + cDef.LabelCap,
						() => owner.DoFloatAllCategory(cDef)
					));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static void DoFloatAllCategory(this IFilterOwnerAdder owner, ListFilterCategoryDef cDef)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in cDef.SubFilters)
			{
				// I don't think we need to worry about double-nested filters
				options.Add(new FloatMenuOption(
					def.LabelCap,
					() => owner.Add(ListFilterMaker.MakeFilter(def, owner))
				));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
