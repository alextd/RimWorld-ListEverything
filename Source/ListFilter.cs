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
		public bool devOnly;
	}

	[DefOf]
	public static class ListFilterMaker
	{
		public static ListFilterDef Filter_Name;

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
		public int id;//For window purposes
		public static int nextID = 1;
		public ListFilterDef def;

		public ListFilter()
		{
			id = nextID++;
		}

		private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

		public bool enabled = true;
		public bool include = true;
		public bool delete;

		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return enabled ? list.Where(t => (Applies(t.GetInnerThing()) || Applies(t)) == include) : list;
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
			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.mouseDown && Event.current.button == 1)
			{
				GUI.FocusControl("");
				Event.current.Use();
			}

			GUI.SetNextControlName($"LIST_FILTER_NAME_INPUT{id}");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), name);
			if (newStr != name)
			{
				name = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				GUI.FocusControl("");
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
	}

	class ListFilterForbiddable : ListFilter
	{
		public override bool Applies(Thing thing) =>
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

		public override string GetLabel() => des?.defName ?? NullOption();
		public override string NullOption() => "Any";
		public override IEnumerable<object> Options() => DefDatabase<DesignationDef>.AllDefs.OrderBy(d=>d.defName).Cast<object>();
		public override string NameFor(object o) => (o as DesignationDef).defName;
		public override void Callback(object o) => des = o as DesignationDef;
	}

	class ListFilterFreshness : ListFilterDropDown
	{
		RotStage stage = RotStage.Fresh;

		public override bool Applies(Thing thing) =>
			thing.TryGetComp<CompRottable>() is CompRottable rot && rot.Stage == stage;

		public override string GetLabel() => stage.ToString();
		public override IEnumerable<object> Options() => Enum.GetValues(typeof(RotStage)).Cast<object>();
		public override void Callback(object o) => stage = (RotStage)o;
	}

	class ListFilterRottable : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.HasComp(typeof(CompRottable));
	}

	class ListFilterGrowth : ListFilter
	{
		FloatRange range = FloatRange.ZeroToOne;

		public override bool Applies(Thing thing) =>
			thing is Plant p && range.Includes(p.Growth);
		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = range;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (range != newRange)
			{
				range = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterPlant : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.category == ThingCategory.Plant;
	}

	class ListFilterClassType : ListFilterDropDown
	{
		Type type = typeof(Thing);

		public override bool Applies(Thing thing) =>
			type.IsAssignableFrom(thing.GetType());

		public static List<object> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t=>t.ToString()).Cast<object>().ToList();
		public override string GetLabel() => type.ToString();
		public override IEnumerable<object> Options() => types;
		public override void Callback(object o) => type = o as Type;
	}

	class ListFilterFaction : ListFilterDropDown
	{
		Faction faction = Faction.OfPlayer;

		public override bool Applies(Thing thing) =>
			faction == null ?
				thing.Faction == null || thing.Faction.def.hidden:
				thing.Faction == faction;
		
		public override string GetLabel() => faction?.Name ?? NullOption();
		public override string NullOption() => "None";
		public override IEnumerable<object> Options() => Find.FactionManager.AllFactionsVisibleInViewOrder.Cast<object>();
		public override void Callback(object o) => faction = o as Faction;
	}

	/*class ListFilterCanFaction : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.CanHaveFaction;
	}*/

	class ListFilterCategory : ListFilterDropDown
	{
		ThingCategoryDef catDef = ThingCategoryDefOf.Root;

		public override bool Applies(Thing thing) =>
			thing.def.IsWithinCategory(catDef);
		
		public override string GetLabel() => catDef.LabelCap;
		public override IEnumerable<object> Options() => DefDatabase<ThingCategoryDef>.AllDefsListForReading.Cast<object>();
		public override string NameFor(object o) => (o as ThingCategoryDef).LabelCap;
		public override void Callback(object o) => catDef = o as ThingCategoryDef;
	}

	class ListFilterMineable : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.mineable;
	}

	class ListFilterResourceRock: ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.building?.isResourceRock ?? false;
	}

	class ListFilterHP : ListFilter
	{
		FloatRange range = FloatRange.ZeroToOne;

		public override bool Applies(Thing thing)
		{
			float? pct = null;
			if (thing is Pawn pawn)
				pct = pawn.health.summaryHealth.SummaryHealthPercent;
			if (thing.def.useHitPoints)
				pct = (float)thing.HitPoints / thing.MaxHitPoints;
			return pct != null && range.Includes(pct.Value);
		}

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = range;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (range != newRange)
			{
				range = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterQuality : ListFilter
	{
		QualityRange range = QualityRange.All;

		public override bool Applies(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			range.Includes(qc);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			QualityRange newRange = range;
			Widgets.QualityRange(rect.RightPart(0.5f), id, ref newRange);
			if (range != newRange)
			{
				range = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterStuff : ListFilterDropDown
	{
		ThingDef stuffDef;

		public override bool Applies(Thing thing) =>
			thing.Stuff == stuffDef || thing is IConstructible c && c.UIStuff() == stuffDef;

		public override string GetLabel() => stuffDef?.LabelCap ?? NullOption();
		public override string NullOption() => "No Stuff";
		public override IEnumerable<object> Options()
		{
			return ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap<ThingDef>(t => t.Stuff).Cast<object>()
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff).Cast<object>();
		}
		public override string NameFor(object o) => (o as ThingDef).LabelCap;
		public override void Callback(object o) => stuffDef = o as ThingDef;
	}

	class ListFilterDrawerType : ListFilterDropDown
	{
		DrawerType type;

		public override bool Applies(Thing thing) =>
			thing.def.drawerType == type;

		public override string GetLabel() => type.ToString();
		public override IEnumerable<object> Options() => Enum.GetValues(typeof(DrawerType)).Cast<object>();
		public override void Callback(object o) => type = (DrawerType)o;
	}

	class ListFilterMissingBodyPart : ListFilterDropDown
	{
		BodyPartDef partDef;

		public override bool Applies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return partDef == null 
				? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty()
				: pawn.RaceProps.body.GetPartsWithDef(partDef)
					.Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string GetLabel() => partDef?.LabelCap ?? NullOption();
		public override string NullOption() => "Any";
		public override IEnumerable<object> Options()
		{
			return ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>()
					).Cast<object>()
				: DefDatabase<BodyPartDef>.AllDefs.Cast<object>();
		}
		public override string NameFor(object o) => (o as BodyPartDef).LabelCap;
		public override void Callback(object o) => partDef = o as BodyPartDef;
	}
}
