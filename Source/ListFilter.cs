using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public abstract class ListFilter
	{
		private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

		public bool enabled = true;
		public bool include = true;
		public bool delete;

		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return enabled ? list.Where(t => Applies(t) == include) : list;
		}
		public abstract bool Applies(Thing list);

		public bool Listing(Listing_Standard listing)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.yMin, UIDirection.LeftThenDown, rowRect.width);

			//Clear button
			if (row.ButtonIcon(CancelTex, "Delete this filter"))
			{
				delete = true;
			}

			bool changed = false;
			//Toggle button
			if (row.ButtonIcon(enabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, "Toggle This Filter"))
			{
				enabled = !enabled;
				changed = true;
			}

			//Include/Exclude
			if (row.ButtonText(include ? "Inc" : "Exc", "Include or Exclude this filter"))
			{
				include = !include;
				changed = true;
			}


			//Draw option row
			rowRect.width = row.FinalX;
			changed |= DrawOption(rowRect);
			listing.Gap(listing.verticalSpacing);
			return changed;
		}
		
		public virtual bool DrawOption(Rect rect)
		{
			Widgets.Label(rect, Name());
			return false;
		}

		public override string ToString()
		{
			return Name();
		}
		public abstract string Name();
	}

	class ListFilterName : ListFilter
	{
		public override string Name() => "Name";

		string name = "";
		public override bool Applies(Thing thing) =>
			thing.Label.ToLower().Contains(name.ToLower());

		public override bool DrawOption(Rect rect)
		{
			string newStr = Widgets.TextField(rect, name);
			if (newStr != name)
			{
				name = newStr;
				return true;
			}
			return false;
		}
	}

	class ListFilterForbidden : ListFilter
	{
		public override string Name() => "Forbidden";

		public override bool Applies(Thing thing) =>
			thing.IsForbidden(Faction.OfPlayer);
	}

	class ListFilterDesignation : ListFilter
	{
		public override string Name() => "Designated";

		public override bool Applies(Thing thing) =>
			Find.CurrentMap.designationManager.AllDesignationsOn(thing).Count() > 0 ||
			Find.CurrentMap.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0;
	}
}
