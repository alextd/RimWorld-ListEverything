﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class ListFilterDef : Def
	{
		public Type filterClass;
	}

	public static class ListFilterMaker
	{
		public static ListFilter MakeFilter(ListFilterDef def)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			return filter;
		}
	}

	[StaticConstructorOnStartup]
	public abstract class ListFilter
	{
		public ListFilterDef def;

		private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

		public bool enabled = true;
		public bool include = true;
		public bool delete;

		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return enabled ? list.Where(t => PreFilter(t) && Applies(t) == include) : list;
		}
		public abstract bool Applies(Thing list);
		public virtual bool PreFilter(Thing thing) => true;

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
			Widgets.Label(rect, def.LabelCap);
			return false;
		}
	}

	class ListFilterName : ListFilter
	{
		string name = "";
		public override bool Applies(Thing thing) =>
			thing.Label.ToLower().Contains(name.ToLower());

		public override bool DrawOption(Rect rect)
		{
			if (GUI.GetNameOfFocusedControl() == "LIST_FILTER_NAME_INPUT" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.mouseDown && Event.current.button == 1)
			{
				GUI.FocusControl("");
				Event.current.Use();
			}

			GUI.SetNextControlName("LIST_FILTER_NAME_INPUT");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), name);
			if (newStr != name)
			{
				name = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				name = "";
				return true;
			}
			return false;
		}
	}

	class ListFilterForbidden : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.IsForbidden(Faction.OfPlayer);

		public override bool PreFilter(Thing thing) =>
			thing.def.HasComp(typeof(CompForbiddable)) && thing.Spawned;
	}

	abstract class ListFilterDropDown : ListFilter
	{
		public abstract string GetLabel();
		public virtual string NullOption() => null;
		public abstract IEnumerable<object> Options();
		public virtual string NameFor(object o) => o.ToString();
		public abstract void Callback(object o);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			if (Widgets.ButtonText(rect.RightPart(0.3f), GetLabel()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOption(nullOption, () => Callback(null)));
				foreach (object o in Options())
				{
					options.Add(new FloatMenuOption(NameFor(o), () => Callback(o)));
				}
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });

				return true;
			}
			return false;
		}
	}

	class ListFilterDesignation : ListFilterDropDown
	{
		DesignationDef des;

		public override bool Applies(Thing thing) =>
			des != null ? 
			(des.targetType == TargetType.Thing ? Find.CurrentMap.designationManager.DesignationOn(thing, des) != null :
			Find.CurrentMap.designationManager.DesignationAt(thing.PositionHeld, des) != null) :
			(Find.CurrentMap.designationManager.DesignationOn(thing) != null ||
			Find.CurrentMap.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string GetLabel() => des?.defName ?? "Any";
		public override string NullOption() => "Any";
		public override IEnumerable<object> Options() => DefDatabase<DesignationDef>.AllDefs.Cast<object>();
		public override string NameFor(object o) => (o as DesignationDef).defName;
		public override void Callback(object o) => des = o as DesignationDef;
	}

	class ListFilterFreshness : ListFilterDropDown
	{
		RotStage stage = RotStage.Fresh;

		public override bool Applies(Thing thing) =>
			thing.GetRotStage() == stage;

		public override bool PreFilter(Thing thing) =>
			thing.def.HasComp(typeof(CompRottable));

		public override string GetLabel() => stage.ToString();
		public override IEnumerable<object> Options() => Enum.GetValues(typeof(RotStage)).Cast<object>();
		public override void Callback(object o) => stage = (RotStage)o;
	}

	class ListFilterGrowth : ListFilter
	{
		FloatRange range = FloatRange.ZeroToOne;

		public override bool Applies(Thing thing) =>
			thing is Plant p && range.Includes(p.Growth);

		public override bool PreFilter(Thing thing) =>
			thing is Plant;

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = range;
			Widgets.FloatRange(rect.RightPart(0.5f), 85246, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (range != newRange)
			{
				range = newRange;
				return true;
			}
			return false;
		}
	}
}
