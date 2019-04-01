using System;
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
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), name);
			if (newStr != name)
			{
				name = newStr;
				return true;
			}
			if(Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
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

	class ListFilterDesignation : ListFilter
	{
		DesignationDef des;

		public override bool Applies(Thing thing) =>
			des != null ? 
			(des.targetType == TargetType.Thing ? Find.CurrentMap.designationManager.DesignationOn(thing, des) != null :
			Find.CurrentMap.designationManager.DesignationAt(thing.PositionHeld, des) != null) :
			(Find.CurrentMap.designationManager.DesignationOn(thing) != null ||
			Find.CurrentMap.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			if(Widgets.ButtonText(rect.RightPart(0.3f), des?.defName ?? "Any"))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				options.Add(new FloatMenuOption("Any", () => des = null));
				foreach (DesignationDef desDef in DefDatabase<DesignationDef>.AllDefs)
				{
					options.Add(new FloatMenuOption(desDef.defName, () => des = desDef));
				}
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });

				return true;
			}
			return false;
		}
	}

	class ListFilterFreshness : ListFilter
	{
		RotStage stage = RotStage.Fresh;

		public override bool Applies(Thing thing) =>
			thing.GetRotStage() == stage;

		public override bool PreFilter(Thing thing) =>
			thing.def.HasComp(typeof(CompRottable));

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			if (Widgets.ButtonText(rect.RightPart(0.3f), stage.ToString()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (RotStage s in Enum.GetValues(typeof(RotStage)))
				{
					options.Add(new FloatMenuOption(s.ToString(), () => stage = s));
				}
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });

				return true;
			}
			return false;
		}
	}
}
