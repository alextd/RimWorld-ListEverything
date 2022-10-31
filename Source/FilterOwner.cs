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

		//Gather method that passes in both FindDescription and all ListFilters to selector
		public IEnumerable<T> Gather<T>(Func<object, T?> selector) where T : struct
		{
			{
				if (selector(parent) is T result)
					yield return result;
			}
			foreach (var filter in filters)
			{
				{
					if (selector(filter) is T result)
						yield return result;
				}
				if (filter is IFilterHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
			}
		}
		//sadly 100% copied, subtract the "?" oh gee.
		public IEnumerable<T> Gather<T>(Func<object, T> selector) where T : class
		{
			{
				if (selector(parent) is T result)
					yield return result;
			}
			foreach (var filter in filters)
			{
				{
					if (selector(filter) is T result)
						yield return result;
				}
				if (filter is IFilterHolder childHolder)
					foreach (T r in childHolder.Children.Gather(selector))
						yield return r;
			}
		}

		//Gather method that passes in both FindDescription and all ListFilters to selector
		public void ForEach(Action<object> action)
		{
			action(parent);
			foreach (var filter in filters)
			{
				if (filter is IFilterHolder childHolder)
					childHolder.Children.ForEach(action);	//handles calling on itself
				else //just a filter then
					action(filter);
			}
		}

		public void MasterReorder(int from, int fromGroup, int to, int toGroup)
		{
			Log.Message($"MasterReorder(int from={from}, int fromGroup={fromGroup}, int to={to}, int toGroup={toGroup})");

			ListFilter draggedFilter = Gather(delegate (object o)
			{
				if (o is IFilterHolder holder)
				{
					if (holder.Children.reorderID == fromGroup)
					{
						ListFilter result = holder.Children.filters.ElementAt(from);
						holder.Children.filters.RemoveAt(from);
						return result;
					}
				}
				return null;
			}).First();

			ForEach(delegate (object o)
			{
				if (o is IFilterHolder holder)
				{
					if (holder.Children.reorderID == toGroup)
					{
						holder.Children.filters.Insert(to, draggedFilter);
					}
				}
			});
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

			List<int> reorderIDs = new(Gather(f => (f as IFilterHolder)?.Children.reorderID));

			ReorderableWidget.NewMultiGroup(reorderIDs, parent.RootFindDesc.Children.MasterReorder);

			listing.EndScrollView(ref scrollHeight);

			return changed;
		}


		// draw filters continuing a Listing_StandardIndent
		public int reorderID;
		private float reorderRectHeight;

		public bool DrawFilters(Listing_StandardIndent listing, bool locked)
		{
			Rect coveredRect = new Rect(0f, listing.CurHeight, listing.ColumnWidth, reorderRectHeight);
			if (Event.current.type == EventType.Repaint)
			{
				reorderID = ReorderableWidget.NewGroup(
					(int from, int to) => Reorder(from, to, true),
					ReorderableDirection.Vertical,
					coveredRect, 1f,
					extraDraggedItemOnGUI: (int index, Vector2 dragStartPos) =>
						DrawMouseAttachedFilter(Filters.ElementAt(index), coveredRect.width - 100));

				// Turn off The Multigroup system assuming that if you're closer to group A but in group B's rect, that you want to insert at end of B.
				// That just doesn't apply here.
				// (it uses absRect to check mouseover group B and that overrides if you're in an okay place to drop in group A)
				var group = ReorderableWidget.groups[reorderID];	//immutable struct O_o
				group.absRect = new Rect();
				ReorderableWidget.groups[reorderID] = group;
			}

			bool changed = false;
			HashSet<ListFilter> removedFilters = new();
			foreach (ListFilter filter in Filters)
			{
				Rect usedRect = listing.GetRect(0);

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
					usedRect.yMax = listing.CurHeight;
					Widgets.DrawHighlight(usedRect);
				}
			}

			reorderRectHeight = listing.CurHeight - coveredRect.y;

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
