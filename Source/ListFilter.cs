﻿using System;
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

		//public bool enabled;
		public bool delete;
		//public bool include;

		public abstract IEnumerable<Thing> Apply(IEnumerable<Thing> list);

		public bool Listing(Listing_Standard listing)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.yMin, UIDirection.LeftThenDown, rowRect.width);

			//Clear button
			if(row.ButtonIcon(CancelTex, "Delete this filter"))
			{
				delete = true;
			}


			//Draw option row
			rowRect.width = row.FinalX;
			bool result = DrawOption(rowRect);
			listing.Gap(listing.verticalSpacing);
			return result;
		}

		public abstract bool DrawOption(Rect rect);
	}

	class ListFilterName : ListFilter
	{
		string name = "";
		public override IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			if (name.NullOrEmpty())
				return list;
			return list.Where(t => t.Label.ToLower().Contains(name.ToLower()));
		}

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
		bool show;
		public override IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return list.Where(t => t.IsForbidden(Faction.OfPlayer) == show);
		}

		public override bool DrawOption(Rect rect)
		{
			Widgets.Label(rect.LeftPart(0.8f), "Forbidden");
			if (Widgets.ButtonText(rect.RightPart(0.2f), show ? "Show" : "Hide"))
			{
				show = !show;
				return true;
			}
			return false;
		}
	}

	class ListFilterDesignation : ListFilter
	{
		bool show;
		public override IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			var des = Find.CurrentMap.designationManager;
			return list.Where(t => show == 
			(des.AllDesignationsOn(t).Count() > 0 || 
			des.AllDesignationsAt(t.Position).Count() > 0));
		}

		public override bool DrawOption(Rect rect)
		{
			Widgets.Label(rect.LeftPart(0.8f), "Designated");
			if (Widgets.ButtonText(rect.RightPart(0.2f), show ? "Show" : "Hide"))
			{
				show = !show;
				return true;
			}
			return false;
		}
	}
}
