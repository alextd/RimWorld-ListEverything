using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

namespace List_Everything
{
	public interface IFilterHolder
	{
		public FilterHolder Children { get; }
		public FindDescription RootFindDesc { get; }
	}
	public class FilterHolder : IExposable
	{
		private IFilterHolder parent;
		private List<ListFilter> filters = new List<ListFilter>() { };

		public FilterHolder(IFilterHolder p)
		{
			parent = p;
		}


		public IEnumerable<ListFilter> Filters => filters;

		public void ExposeData()
		{
			Scribe_Collections.Look(ref filters, "filters");
		}

		public FilterHolder Clone(IFilterHolder newHolder)
		{
			FilterHolder clone = new FilterHolder(newHolder);
			clone.filters = filters.Select(f => f.Clone(newHolder)).ToList();
			return clone;
		}

		public void Add(ListFilter newFilter, bool remake = false)
		{
			filters.Add(newFilter);
			if (remake) parent.RootFindDesc.RemakeList();
		}

		public void RemoveAll(HashSet<ListFilter> removedFilters)
		{
			filters.RemoveAll(f => removedFilters.Contains(f));
		}

		public bool Check(Predicate<ListFilter> check) =>
			filters.Any(f => f.Check(check));

		public void Reorder(int from, int to, bool remake = false)
		{
			var f = filters[from];
			filters.RemoveAt(from);
			filters.Insert(from < to ? to - 1 : to, f);


			if (remake) parent.RootFindDesc.RemakeList();
		}

		//Draw filters completely, in a rect
		private Vector2 scrollPositionFilt = Vector2.zero;
		private float scrollHeight;

		public bool DrawFilters(Rect listRect, bool locked)
		{
			Listing_StandardIndent listing = new Listing_StandardIndent()
				{ maxOneColumn = true };

			float viewWidth = listRect.width;
			if (scrollHeight > listRect.height)
				viewWidth -= 16f;
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollHeight);

			listing.BeginScrollView(listRect, ref scrollPositionFilt, viewRect);

			bool changed = DrawFilters(listing, locked);

			listing.EndScrollView(ref scrollHeight);

			return changed;
		}


		// draw filters continuing a Listing_StandardIndent
		private int reorderID;
		private float reorderRectHeight;

		public bool DrawFilters(Listing_StandardIndent listing, bool locked)
		{
			float heightBeforeAll = listing.CurHeight;


			Rect coveredRect = new Rect(0f, heightBeforeAll, listing.ColumnWidth, reorderRectHeight);
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => Reorder(from, to, true),
					ReorderableDirection.Vertical,
					coveredRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedFilter(Filters.ElementAt(index), coveredRect.width - 100));
			}

			bool changed = false;
			HashSet<ListFilter> removedFilters = new();
			foreach (ListFilter filter in Filters)
			{
				Rect usedRect = listing.GetRect(0);
				float heightBeforeSingle = listing.CurHeight;
				(bool ch, bool d) = filter.Listing(listing, locked);
				changed |= ch;
				if (d)
					removedFilters.Add(filter);

				//Reorder box with only one line tall
				usedRect.height = Text.LineHeight;
				ReorderableWidget.Reorderable(reorderID, usedRect);

				// Highlight the filters that pass for selected objects (useful for "any" filters)
				if (!(filter is IFilterHolder) && Find.Selector.SelectedObjects.Any(o => o is Thing t && filter.AppliesTo(t)))
				{
					usedRect.height = listing.CurHeight - heightBeforeSingle;
					Widgets.DrawHighlight(usedRect);
				}
			}

			reorderRectHeight = listing.CurHeight - heightBeforeAll;

			RemoveAll(removedFilters);

			if (!locked)
				DrawAddRow(listing);

			return changed;
		}

		public static void DrawMouseAttachedFilter(ListFilter dragFilter, float width)
		{
			Vector2 mousePositionOffset = Event.current.mousePosition + Vector2.one * 12;
			Rect dragRect = new Rect(mousePositionOffset, new(width, Text.LineHeight));

			//Same id 34003428 as GenUI.DrawMouseAttachment
			Find.WindowStack.ImmediateWindow(34003428, dragRect, WindowLayer.Super,
				() => dragFilter.DrawMain(dragRect.AtZero(), true),
				doBackground: false, absorbInputAroundWindow: false, 0f);
		}

		public void DrawAddRow(Listing_StandardIndent listing)
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
				DoFloatAllFilters();
			}
		}

		public void DoFloatAllFilters()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterSelectableDef def in ListFilterMaker.SelectableList)
			{
				if (def is ListFilterDef fDef)
					options.Add(new FloatMenuOption(
						fDef.LabelCap,
						() => Add(ListFilterMaker.MakeFilter(fDef, parent), true)
					));
				if (def is ListFilterCategoryDef cDef)
					options.Add(new FloatMenuOption(
						"+ " + cDef.LabelCap,
						() => DoFloatAllCategory(cDef)
					));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public void DoFloatAllCategory(ListFilterCategoryDef cDef)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in cDef.SubFilters)
			{
				// I don't think we need to worry about double-nested filters
				options.Add(new FloatMenuOption(
					def.LabelCap,
					() => Add(ListFilterMaker.MakeFilter(def, parent), true)
				));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}
	}
}
