using System;
using System.Collections;
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
		public static ListFilterDef Filter_Group;

		public static ListFilter MakeFilter(ListFilterDef def)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			return filter;
		}
		public static ListFilter NameFilter => ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name);
	}

	[StaticConstructorOnStartup]
	public abstract class ListFilter : IExposable
	{
		public int id;//For window focus purposes
		public static int nextID = 1;
		public ListFilterDef def;

		protected ListFilter()	// Of course protected here doesn't make subclasses protected sooo ?
		{
			id = nextID++;
		}
	
		private static readonly Texture2D CancelTex = ContentFinder<Texture2D>.Get("UI/Designators/Cancel", true);

		public bool enabled = true; //simply turn off but keep in list
		public bool include = true;	//or exclude
		public bool delete;

		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return enabled ? list.Where(t => AppliesTo(t)) : list;
		}

		//This can be problematic for minified things: We want the qualities of the inner things,
		//But position/status of outer thing. So it just checks both -- but then something like 'no stuff' always applies. Oh well
		public bool AppliesTo(Thing thing) => (FilterApplies(thing.GetInnerThing()) || FilterApplies(thing)) == include;

		public abstract bool FilterApplies(Thing thing);

		public bool Listing(Listing_Standard listing)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.yMin, UIDirection.LeftThenDown, rowRect.width);

			bool changed = false;
			//Clear button
			if (row.ButtonIcon(CancelTex, "Delete this filter"))
			{
				delete = true;
				changed = true;
			}

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
			changed |= DrawMore(listing);
			if(shouldFocus)
			{
				DoFocus();
				shouldFocus = false;
			}

			listing.Gap(listing.verticalSpacing);
			return changed;
		}


		public virtual bool DrawOption(Rect rect)
		{
			Widgets.Label(rect, def.LabelCap);
			return false;
		}
		public virtual bool DrawMore(Listing_Standard listing) => false;

		private bool shouldFocus;
		public void Focus() => shouldFocus = true;
		protected virtual void DoFocus() { }
		
		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref include, "include", true);
		}
		public virtual ListFilter Clone()
		{
			ListFilter clone = ListFilterMaker.MakeFilter(def);
			clone.enabled = enabled;
			clone.include = include;

			return clone;
		}
	}

	class ListFilterName : ListFilter
	{
		string name = "";
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref name, "name");
		}
		public override ListFilter Clone()
		{
			ListFilterName clone = (ListFilterName)base.Clone();
			clone.name = name;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
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

		protected override void DoFocus()
		{
			GUI.FocusControl($"LIST_FILTER_NAME_INPUT{id}");
		}
	}

	class ListFilterForbidden : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing.IsForbidden(Faction.OfPlayer);
	}

	class ListFilterForbiddable : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.HasComp(typeof(CompForbiddable)) && thing.Spawned;
	}

	abstract class ListFilterDropDown<T> : ListFilter
	{
		public T sel;
		public override void ExposeData()
		{
			base.ExposeData();

			if (typeof(Def).IsAssignableFrom(typeof(T)))
			{
				// *magic*
				Def temp = sel as Def;
				Scribe_Defs.Look(ref temp, "sel");
				sel = (T)(object)temp;
			}
			else if (typeof(ILoadReferenceable).IsAssignableFrom(typeof(T)))
			{
				ILoadReferenceable temp = sel as ILoadReferenceable;
				Scribe_References.Look(ref temp, "sel");
				sel = (T)(object)temp;
			}
			else
				Scribe_Values.Look(ref sel, "sel");
		}
		public override ListFilter Clone()
		{
			ListFilterDropDown<T> clone = (ListFilterDropDown<T>)base.Clone();
			clone.sel = sel;
			return clone;
		}

		public abstract string GetLabel();
		public virtual string NullOption() => null;
		public abstract IEnumerable Options();
		public virtual string NameFor(T o) => o.ToString();
		public abstract void Callback(T o);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			if (Widgets.ButtonText(rect.RightPart(0.4f), GetLabel()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOption(nullOption, () => Callback(default(T))));
				foreach (T o in Options())
				{
					options.Add(new FloatMenuOption(NameFor(o), () => Callback(o)));
				}
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });

				return true;
			}
			return false;
		}
	}

	class ListFilterDesignation : ListFilterDropDown<DesignationDef>
	{
		public override bool FilterApplies(Thing thing) =>
			sel != null ? 
			(sel.targetType == TargetType.Thing ? Find.CurrentMap.designationManager.DesignationOn(thing, sel) != null :
			Find.CurrentMap.designationManager.DesignationAt(thing.PositionHeld, sel) != null) :
			(Find.CurrentMap.designationManager.DesignationOn(thing) != null ||
			Find.CurrentMap.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string GetLabel() => sel?.defName ?? NullOption();
		public override string NullOption() => "Any";
		public override IEnumerable Options() => DefDatabase<DesignationDef>.AllDefs.OrderBy(d=>d.defName);
		public override string NameFor(DesignationDef o) => o.defName;
		public override void Callback(DesignationDef o) => sel = o;
	}

	class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		public ListFilterFreshness() => sel = RotStage.Fresh;

		public override bool FilterApplies(Thing thing) =>
			thing.TryGetComp<CompRottable>() is CompRottable rot && rot.Stage == sel;

		public override string GetLabel() => sel.ToString();
		public override IEnumerable Options() => Enum.GetValues(typeof(RotStage));
		public override void Callback(RotStage o) => sel = o;
	}

	class ListFilterRottable : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.HasComp(typeof(CompRottable));
	}

	class ListFilterGrowth : ListFilter
	{
		FloatRange range = FloatRange.ZeroToOne;
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref range, "range");
		}
		public override ListFilter Clone()
		{
			ListFilterGrowth clone = (ListFilterGrowth)base.Clone();
			clone.range = range;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
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

	class ListFilterPlantHarvest : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing is Plant plant && plant.HarvestableNow;
	}

	class ListFilterClassType : ListFilterDropDown<Type>
	{
		public ListFilterClassType() => sel = typeof(Thing);

		public override bool FilterApplies(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t=>t.ToString()).ToList();
		public override string GetLabel() => sel.ToString();
		public override IEnumerable Options() => types;
		public override void Callback(Type o) => sel = o;
	}

	class ListFilterFaction : ListFilterDropDown<Faction>
	{
		public ListFilterFaction() => sel = Faction.OfPlayer;

		public override bool FilterApplies(Thing thing) =>
			sel == null ?
				thing.Faction == null || thing.Faction.def.hidden:
				thing.Faction == sel;
		
		public override string GetLabel() => sel?.Name ?? NullOption();
		public override string NullOption() => "None";
		public override IEnumerable Options() => Find.FactionManager.AllFactionsVisibleInViewOrder;
		public override void Callback(Faction o) => sel = o;
	}

	/*class ListFilterCanFaction : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.CanHaveFaction;
	}*/

	class ListFilterCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterCategory() => sel = ThingCategoryDefOf.Root;

		public override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(sel);
		
		public override string GetLabel() => sel.LabelCap;
		public override IEnumerable Options() => DefDatabase<ThingCategoryDef>.AllDefsListForReading;
		public override string NameFor(ThingCategoryDef o) => o.LabelCap;
		public override void Callback(ThingCategoryDef o) => sel = o;
	}

	class ListFilterMineable : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.mineable;
	}

	class ListFilterResourceRock: ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.building?.isResourceRock ?? false;
	}

	class ListFilterHP : ListFilter
	{
		FloatRange range = FloatRange.ZeroToOne;
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref range, "range");
		}
		public override ListFilter Clone()
		{
			ListFilterHP clone = (ListFilterHP)base.Clone();
			clone.range = range;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
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
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref range, "range");
		}
		public override ListFilter Clone()
		{
			ListFilterQuality clone = (ListFilterQuality)base.Clone();
			clone.range = range;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
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

	class ListFilterStuff : ListFilterDropDown<ThingDef>
	{
		public override bool FilterApplies(Thing thing) =>
			thing.Stuff == sel || thing is IConstructible c && c.UIStuff() == sel;

		public override string GetLabel() => sel?.LabelCap ?? NullOption();
		public override string NullOption() => "No Stuff";
		public override IEnumerable Options()
		{
			return ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap<ThingDef>(t => t.Stuff)
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff);
		}
		public override string NameFor(ThingDef o) => o.LabelCap;
		public override void Callback(ThingDef o) => sel = o;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;

		public override string GetLabel() => sel.ToString();
		public override IEnumerable Options() => Enum.GetValues(typeof(DrawerType));
		public override void Callback(DrawerType o) => sel = o;
	}

	class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return sel == null
				? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty()
				: pawn.RaceProps.body.GetPartsWithDef(sel)
					.Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string GetLabel() => sel?.LabelCap ?? NullOption();
		public override string NullOption() => "Any";
		public override IEnumerable Options()
		{
			return ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>())
				: DefDatabase<BodyPartDef>.AllDefs;
		}
		public override string NameFor(BodyPartDef o) => o.LabelCap;
		public override void Callback(BodyPartDef o) => sel = o;
	}

	class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea() => sel = Find.CurrentMap.areaManager.Home;

		public override bool FilterApplies(Thing thing) =>
			sel[thing.PositionHeld];

		public override string GetLabel() => sel.Label;
		public override IEnumerable Options() => Find.CurrentMap.areaManager.AllAreas;
		public override string NameFor(Area o) => o.Label;
		public override void Callback(Area o) => sel = o;
	}

	class ListFilterDeterioration : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}
}
