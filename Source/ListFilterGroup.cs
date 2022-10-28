using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace List_Everything
{
	[StaticConstructorOnStartup]
	static class TexButton
	{
		public static readonly Texture2D Reveal = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Reveal", true);
		public static readonly Texture2D Collapse = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Collapse", true);
		public static readonly Texture2D Plus = ContentFinder<Texture2D>.Get("UI/Buttons/Plus", true);
		public static readonly Texture2D SelectAll = ContentFinder<Texture2D>.Get("UI/Commands/SelectNextTransporter", true);
		public static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);
		public static readonly Texture2D PassionMajorIcon = ContentFinder<Texture2D>.Get("UI/Icons/PassionMajor", true);

		//Mine!
		public static readonly Texture2D LockOn = ContentFinder<Texture2D>.Get("Locked", true);
		public static readonly Texture2D LockOff = ContentFinder<Texture2D>.Get("Unlocked", true);
		public static new readonly Texture2D Equals = ContentFinder<Texture2D>.Get("Equals", true);
		public static readonly Texture2D LessThan = ContentFinder<Texture2D>.Get("LessThan", true);
		public static readonly Texture2D GreaterThan = ContentFinder<Texture2D>.Get("GreaterThan", true);
	}

	public class ListFilterGroup : ListFilter, IFilterOwner
	{
		private List<ListFilter> filters = new List<ListFilter>() { };
		protected bool any = true; // or all

		protected override bool FilterApplies(Thing t) => 
			any ? filters.Any(f => f.Enabled && f.AppliesTo(t)) : 
			filters.All(f => !f.Enabled || f.AppliesTo(t));

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref filters, "filters");
			Scribe_Values.Look(ref any, "any", true);
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterGroup clone = (ListFilterGroup)base.Clone(newOwner);
			clone.filters = filters.Select(f => f.Clone(clone)).ToList();
			clone.any = any;
			//clone.owner = newOwner; //No - MakeFilter sets it.
			return clone;
		}
		public override void DoResolveReference(Map map)
		{
			filters.ForEach(f => f.DoResolveReference(map));
		}

		protected override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			row.Label("TD.IncludeThingsThatMatch".Translate());
			if (row.ButtonText(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				any = !any;
				changed = true;
			}
			row.Label("TD.OfTheseFilters".Translate());
			return changed;
		}

		protected override bool DrawUnder(Listing_StandardIndent listing, bool locked)
		{
			listing.Gap();
			listing.NestedIndent(Listing_Standard.DefaultIndent);

			//Draw filters
			bool changed = this.DoFilters(listing, locked);

			if (!locked)
				this.DrawAddRow(listing);

			listing.NestedOutdent();
			return changed;
		}

		public void Add(ListFilter newFilter, bool remake = false)
		{
			filters.Add(newFilter);
			if(remake)	RootFindDesc.RemakeList();
		}
		public IEnumerable<ListFilter> Filters => filters;
		public void RemoveAll(HashSet<ListFilter> removedFilters)
		{
			filters.RemoveAll(f => removedFilters.Contains(f));
		}
	}

	public class ListFilterInventory : ListFilterGroup
	{
		protected bool parent;//or child

		protected override bool FilterApplies(Thing t)
		{
			if (parent)
			{
				IThingHolder parent = t.ParentHolder;
				while (parent.IsValidHolder())
				{
					if (parent is Thing parentThing && base.FilterApplies(parentThing))
						return true;
					parent = parent.ParentHolder;
				}
				return false;
			}
			else
			{
				return ContentsUtility.AllKnownThings(t as IThingHolder).Any(child => base.FilterApplies(child));
			}
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref parent, "parent", true);
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterInventory clone = (ListFilterInventory)base.Clone(newOwner);
			clone.parent = parent;
			return clone;
		}

		protected override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);
			if (row.ButtonText(parent ? "TD.TheThingHoldingThis".Translate() : "TD.AnythingThisIsHolding".Translate()))
			{
				changed = true;
				parent = !parent;
			}
			row.Label("TD.Matches".Translate());
			if (row.ButtonText(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				changed = true;
				any = !any;
			}
			row.Label("TD.Of".Translate());
			return changed;
		}
	}

	public class ListFilterNearby: ListFilterGroup
	{
		int range;

		protected override bool FilterApplies(Thing t)
		{
			IntVec3 pos = t.PositionHeld;
			Map map = t.MapHeld;

			CellRect cells = new CellRect(pos.x - range, pos.z - range, range * 2 + 1, range * 2 + 1);
			foreach (IntVec3 p in cells)
				if (map.thingGrid.ThingsAt(p).Any(child => base.FilterApplies(child)))
					return true;
			return false;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref range, "range");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterNearby clone = (ListFilterNearby)base.Clone(newOwner);
			clone.range = range;
			return clone;
		}

		protected override bool DrawMain(Rect rect, bool locked)
		{
			bool changed = false;
			WidgetRow row = new WidgetRow(rect.x, rect.y);

			row.Label("TD.AnythingXStepsNearbyMatches".Translate());
			if (row.ButtonText(any ? "TD.AnyOption".Translate() : "TD.AllOptions".Translate()))
			{
				any = !any;
				changed = true;
			}

			IntRange slider = new IntRange(0, range);
			rect.xMin = row.FinalX;
			Widgets.IntRange(rect, id, ref slider, max: 10);
			if(range != slider.max)
			{
				range = slider.max;
				changed = true;
			}
			return changed;
		}
	}
}
