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
			if (row.ButtonIcon(enabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, "Enable this filter"))
			{
				enabled = !enabled;
				changed = true;
			}

			//Include/Exclude
			if (row.ButtonText(include ? "Inc" : "Exc", "Include or Exclude things matching this filter"))
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

	enum ForbiddenType{ Forbidden, Allowed, Forbiddable}
	class ListFilterForbidden : ListFilterDropDown<ForbiddenType>
	{
		public override bool FilterApplies(Thing thing)
		{
			bool forbiddable = thing.def.HasComp(typeof(CompForbiddable)) && thing.Spawned;
			if (!forbiddable) return false;
			bool forbidden = thing.IsForbidden(Faction.OfPlayer);
			switch (sel)
			{
				case ForbiddenType.Forbidden: return forbidden;
				case ForbiddenType.Allowed: return !forbidden;
			}
			return true;  //forbiddable
		}

		public override IEnumerable Options() => Enum.GetValues(typeof(ForbiddenType));
	}

	abstract class ListFilterDropDown<T> : ListFilter
	{
		public T sel;
		public int extraOption; //0 being use T, 1+ defined in subclass

		//References must be saved by name in case of T: ILoadReferenceable, since they not game specific
		//(probably could be in ListFilter)
		//ExposeData saves refName instead of sel
		//Only saved lists get ExposeData called, so only saved lists have refName set
		//(The in-game list will NOT have this set; The saved lists will have this set)
		//Saving the list will generate refName from the current filter on Clone() via MakeRefName()
		//Loading will use refName from the saved list to resolve references in Clone() via ResolveReference()
		string refName; 

		//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
		public override void ExposeData()
		{
			base.ExposeData();

			//Maybe don't save T sel if extraOption > 0 but that doesn't apply for loading so /shrug
			if (typeof(Def).IsAssignableFrom(typeof(T)))
			{
				//From Scribe_Collections:
				if (Scribe.mode == LoadSaveMode.Saving)
				{
					Def temp = sel as Def;
					Scribe_Defs.Look(ref temp, "sel");
				}
				else if (Scribe.mode == LoadSaveMode.LoadingVars)
				{
					//Scribe_Defs.Look doesn't work since it needs the subtype of "Def" and T isn't boxed to be a Def so DefFromNodeUnsafe instead
					sel = ScribeExtractor.DefFromNodeUnsafe<T>(Scribe.loader.curXmlParent["sel"]);
				}
			}
			else if (typeof(ILoadReferenceable).IsAssignableFrom(typeof(T)))
			{
				//Of course between games you can't get references so just save by name should be good enough.
				Scribe_Values.Look(ref refName, "refName"); //And Clone will handle references
			}
			else
				Scribe_Values.Look(ref sel, "sel");

			Scribe_Values.Look(ref extraOption, "ex");
		}
		public override ListFilter Clone()
		{
			ListFilterDropDown<T> clone = (ListFilterDropDown<T>)base.Clone();
			if (extraOption == 0 && typeof(ILoadReferenceable).IsAssignableFrom(typeof(T)))
			{
				if (refName == null)//SAVING: I don't have refName, but I make it and tell saved clone
					clone.refName = sel == null ? "null" : MakeRefName();
				else //LOADING: use my refName to resolve loaded clone's reference
				{
					if (refName == "null") sel = default(T);
					else clone.ResolveReference(refName);
				}
			}
			else
				clone.sel = sel;
			clone.extraOption = extraOption;
			return clone;
		}
		public virtual string MakeRefName() => NameFor(sel);
		public virtual void ResolveReference(string refName) => throw new NotImplementedException();

		private string GetLabel() => extraOption > 0 ? NameForExtra(extraOption): sel != null ? NameFor(sel) : NullOption();
		public virtual string NullOption() => null;
		public abstract IEnumerable Options();
		public virtual string NameFor(T o) => o.ToString();
		private void Callback(T o) { sel = o; extraOption = 0; }

		public virtual int ExtraOptionsCount => 0;
		private IEnumerable<int> ExtraOptions() => Enumerable.Range(1, ExtraOptionsCount);
		public virtual string NameForExtra(int ex) => throw new NotImplementedException();
		private void CallbackExtra(int ex) => extraOption = ex;

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			if (Widgets.ButtonText(rect.RightPart(0.4f), GetLabel()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOption(nullOption, () => Callback(default(T))));
				foreach (T o in Options())
					options.Add(new FloatMenuOption(NameFor(o), () => Callback(o)));
				foreach (int ex in ExtraOptions())
					options.Add(new FloatMenuOption(NameForExtra(ex), () => CallbackExtra(ex)));
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

		public override string NullOption() => "Any";
		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable ?
				Find.CurrentMap.designationManager.allDesignations.Select(d => d.def).Distinct():
				DefDatabase<DesignationDef>.AllDefs.OrderBy(d => d.defName);

		public override string NameFor(DesignationDef o) => o.defName;
	}

	class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		public ListFilterFreshness() => sel = RotStage.Fresh;

		public override bool FilterApplies(Thing thing) =>
			thing.TryGetComp<CompRottable>() is CompRottable rot && rot.Stage == sel;
		
		public override IEnumerable Options() => Enum.GetValues(typeof(RotStage));
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
		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableOnMap(t => t.GetType()).ToList() : 
				types;
	}

	class ListFilterFaction : ListFilterDropDown<FactionRelationKind>
	{
		public ListFilterFaction() => extraOption = 1;

		public override bool FilterApplies(Thing thing) =>
			extraOption == 1 ? thing.Faction == Faction.OfPlayer :
			extraOption == 2 ? thing.Faction == null || thing.Faction.def.hidden :
			(thing.Faction is Faction fac && fac != Faction.OfPlayer && fac.PlayerRelationKind == sel);

		public override IEnumerable Options() => Enum.GetValues(typeof(FactionRelationKind));
		public override int ExtraOptionsCount => 2; 
		public override string NameForExtra(int ex) => ex == 1 ? "Player" : "No Faction";
	}

	/* This is no good, CanHaveFaction includes rock walls.
	class ListFilterCanFaction : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.CanHaveFaction;
	}*/

	class ListFilterCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterCategory() => sel = ThingCategoryDefOf.Root;

		public override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(sel);
		
		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableOnMap(ThingCategoryDefsOfThing).ToList() : 
				DefDatabase<ThingCategoryDef>.AllDefsListForReading;

		public static IEnumerable<ThingCategoryDef> ThingCategoryDefsOfThing(Thing thing)
		{
			if (thing.def.thingCategories == null)
				yield break;
			foreach(var def in thing.def.thingCategories)
			{
				yield return def;
				foreach (var pDef in def.Parents)
					yield return pDef;
			}
		}
		public override string NameFor(ThingCategoryDef o) => o.LabelCap;
	}

	enum MineableType { Resource, Rock, All }
	class ListFilterMineable : ListFilterDropDown<MineableType>
	{
		public override bool FilterApplies(Thing thing)
		{
			switch (sel)
			{
				case MineableType.Resource: return thing.def.building?.isResourceRock ?? false;
				case MineableType.Rock: return thing.def.building?.isNaturalRock ?? false;
				case MineableType.All: return thing.def.mineable;
			}
			return false;
		}

		public override IEnumerable Options() => Enum.GetValues(typeof(MineableType));
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
		public override bool FilterApplies(Thing thing)
		{
			ThingDef stuff = thing is IConstructible c ? c.UIStuff() : thing.Stuff;
			return extraOption > 0 ?
			stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 1]) ?? false :
			sel == null ? stuff != null :
			stuff == sel;
		}

		public override string NullOption() => "Any";
		public override IEnumerable Options() => 
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(t => t.Stuff)
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff);

		public override string NameFor(ThingDef o) => o.LabelCap;
		
		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount;
		public override string NameForExtra(int ex) => DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex-1].LabelCap;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;

		public override IEnumerable Options() => Enum.GetValues(typeof(DrawerType));
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

		public override string NullOption() => "Any";
		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>())
				: DefDatabase<BodyPartDef>.AllDefs;
		
		public override string NameFor(BodyPartDef o) => o.LabelCap;
	}

	class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea() => sel = Find.CurrentMap.areaManager.Home;

		public override string MakeRefName() => sel.Label;
		public override void ResolveReference(string refName)
		{
			sel = Find.CurrentMap.areaManager.GetLabeled(refName);
			if (sel == null)
				Messages.Message($"Tried to load area Filter named ({refName}) but the current map doesn't have any by that name", MessageTypeDefOf.RejectInput);
		}

		public override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			return
				extraOption == 1 ? Find.CurrentMap.roofGrid.Roofed(pos) :
				sel != null ? sel[pos] :
				Find.CurrentMap.areaManager.AllAreas.Any(a => a[pos]);
		}

		public override string NullOption() => "Any";
		public override IEnumerable Options() => Find.CurrentMap.areaManager.AllAreas;
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Roofed";
	}

	class ListFilterZone : ListFilterDropDown<Zone>
	{
		public override void ResolveReference(string refName)
		{
			sel = Find.CurrentMap.zoneManager.AllZones.FirstOrDefault(z => z.label == refName);
			if (sel == null)
				Messages.Message($"Tried to load zone Filter named ({refName}) but the current map doesn't have any by that name", MessageTypeDefOf.RejectInput);
		}

		public override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = Find.CurrentMap.zoneManager.ZoneAt(pos);
			return 
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				sel != null ? zoneAtPos == sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "Any";
		public override IEnumerable Options() => Find.CurrentMap.zoneManager.AllZones;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "Any Stockpile" : "Any Growing Zone";
	}

	class ListFilterDeterioration : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}
}
