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
		public void Reorder(int from, int to, bool remake = false);
	}
	public static class IFilterOwnerExtensions
	{
		// draw filters continuing a Listing_StandardIndent
		public static bool DrawFilters(this IFilterOwner owner, Listing_StandardIndent listing, bool locked)
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


		//Draw filters completely, in a rect
		private static Vector2 scrollPositionFilt = Vector2.zero;
		public static float scrollViewHeightFilt;
		private static int reorderID;
		public static bool DrawFilters(this IFilterOwner owner, Rect listRect, bool locked)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
				{ maxOneColumn = true };

			float viewWidth = listRect.width;
			if (scrollViewHeightFilt > listRect.height)
				viewWidth -= 16f;
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollViewHeightFilt);

			listing.BeginScrollView(listRect, ref scrollPositionFilt, viewRect);

			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => owner.Reorder(from, from < to ? to - 1 : to, true),
					ReorderableDirection.Vertical,
					viewRect, 1f,
					extraDraggedItemOnGUI: delegate (int index, Vector2 dragStartPos)
					{
						Vector2 mousePosition = Event.current.mousePosition + Vector2.one * 12;

						Rect dragRect = new Rect(mousePosition, new(listRect.width - 100, Text.LineHeight));
						ListFilter dragFilter = owner.Filters.ElementAt(index);

						//Same id 34003428 as GenUI.DrawMouseAttachment
						Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
							() => dragFilter.DrawMain(dragRect.AtZero(), true),
							doBackground: false, absorbInputAroundWindow: false, 0f); ;
					}); ;
			}
			bool changed = false;
			HashSet<ListFilter> removedFilters = new();
			foreach (ListFilter filter in owner.Filters)
			{
				Rect usedRect = listing.GetRect(0);
				float heightBefore = listing.CurHeight;
				(bool ch, bool d) = filter.Listing(listing, locked);
				changed |= ch;
				if (d)
					removedFilters.Add(filter);

				//Reorder box with only one line tall
				usedRect.height = Text.LineHeight;
				ReorderableWidget.Reorderable(reorderID, usedRect);

				// Highlight the filters that pass for selected objects (useful for "any" filters)
				if (!(filter is IFilterOwner) && Find.Selector.SelectedObjects.Any(o => o is Thing t && filter.AppliesTo(t)))
				{
					usedRect.height = listing.CurHeight - heightBefore;
					Widgets.DrawHighlight(usedRect);
				}
			}

			owner.RemoveAll(removedFilters);

			if (!locked)
				owner.DrawAddRow(listing);

			listing.EndScrollView(ref scrollViewHeightFilt);

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
