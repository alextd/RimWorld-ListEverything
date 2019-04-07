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
		public ListFilterDef parent;
		public Type filterClass;
		public bool devOnly;
		public List<ListFilterDef> subFilters = new List<ListFilterDef>();

		public override void ResolveReferences()
		{
			base.ResolveReferences();
			foreach (ListFilterDef def in DefDatabase<ListFilterDef>.AllDefs)
				if (def.subFilters?.Contains(this) ?? false)
					parent = def;
		}
	}

	[DefOf]
	public static class ListFilterMaker
	{
		public static ListFilterDef Filter_Name;
		public static ListFilterDef Filter_Group;

		public static ListFilter MakeFilter(ListFilterDef def, FindDescription owner)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			filter.owner = owner;
			filter.PostMake();
			return filter;
		}
		public static ListFilter NameFilter(FindDescription owner) =>
			ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name, owner);
	}

	[StaticConstructorOnStartup]
	public abstract class ListFilter : IExposable
	{
		public int id;//For window focus purposes
		public static int nextID = 1;
		public ListFilterDef def;
		public FindDescription owner;

		protected ListFilter()	// Of course protected here doesn't make subclasses protected sooo ?
		{
			id = nextID++;
		}

		private bool enabled = true; //simply turn off but keep in list
		public bool Enabled
		{
			get => enabled && !ForceDisable();
		}
		public bool include = true; //or exclude
		public bool topLevel = true;
		public bool delete;

		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return Enabled ? list.Where(t => AppliesTo(t)) : list;
		}

		//This can be problematic for minified things: We want the qualities of the inner things,
		//But position/status of outer thing. So it just checks both -- but then something like 'no stuff' always applies. Oh well
		public bool AppliesTo(Thing thing) => (FilterApplies(thing.GetInnerThing()) || FilterApplies(thing)) == include;

		public abstract bool FilterApplies(Thing thing);

		public bool Listing(Listing_StandardIndent listing)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown, rowRect.width);

			bool changed = false;
			//Clear button
			if (row.ButtonIcon(TexButton.CancelTex, "Delete this filter"))
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
			rowRect.width -= (rowRect.xMax - row.FinalX);
			changed |= DrawOption(rowRect);
			changed |= DrawMore(listing);
			if(shouldFocus)
			{
				DoFocus();
				shouldFocus = false;
			}
			if (ForceDisable())
			{
				Widgets.DrawBoxSolid(rowRect, new Color(0.5f, 0, 0, 0.25f));

				TooltipHandler.TipRegion(rowRect, "This filter doesn't work with all maps");
			}

			listing.Gap(listing.verticalSpacing);
			return changed;
		}


		public virtual bool DrawOption(Rect rect)
		{
			if(topLevel)	Widgets.Label(rect, def.LabelCap);
			return false;
		}
		public virtual bool DrawMore(Listing_StandardIndent listing) => false;

		private bool shouldFocus;
		public void Focus() => shouldFocus = true;
		protected virtual void DoFocus() { }
		
		public virtual void ExposeData()
		{
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref enabled, "enabled", true);
			Scribe_Values.Look(ref include, "include", true);
			Scribe_Values.Look(ref topLevel, "topLevel", true);
		}

		//Clone, and resolve references if map specified
		public virtual ListFilter Clone(Map map, FindDescription newOwner) =>
			BaseClone(map, newOwner);

		protected ListFilter BaseClone(Map map, FindDescription newOwner)
		{
			ListFilter clone = ListFilterMaker.MakeFilter(def, newOwner);
			clone.enabled = enabled;
			clone.include = include;
			clone.topLevel = topLevel;
			//clone.owner = newOwner; //No - MakeFilter sets it.
			return clone;
		}

		public virtual void PostMake() { }
		public virtual bool ValidForAllMaps => true;
		public bool ForceDisable() => !ValidForAllMaps && owner.allMaps;
	}

	class ListFilterName : ListFilterWithOption<string>
	{
		public ListFilterName() => sel = "";

		public override bool FilterApplies(Thing thing) =>
			thing.Label.ToLower().Contains(sel.ToLower());

		public override bool DrawOption(Rect rect)
		{
			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.mouseDown && Event.current.button == 1)
			{
				GUI.FocusControl("");
				Event.current.Use();
			}

			GUI.SetNextControlName($"LIST_FILTER_NAME_INPUT{id}");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), sel);
			if (newStr != sel)
			{
				sel = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				GUI.FocusControl("");
				sel = "";
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
	}

	//automated ExposeData + Clone 
	public abstract class ListFilterWithOption<T> : ListFilter
	{
		protected T sel;

		//References must be saved by name in case of T: ILoadReferenceable, since they not game specific
		//(probably could be in ListFilter)
		//ExposeData saves refName instead of sel
		//Only saved lists get ExposeData called, so only saved lists have refName set
		//(The in-game list will NOT have this set; The saved lists will have this set)
		//Saving the list will generate refName from the current filter on Clone(null) via MakeRefName()
		//Loading will use refName from the saved list to resolve references in Clone(map) via ResolveReference()
		//Cloning between two reference types makes the ref from current map and resolves on the new map
		string refName;

		public override void ExposeData()
		{
			base.ExposeData();

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
				//objects saved here  need to be copies made with Clone(null)
				Scribe_Values.Look(ref refName, "refName"); //And Clone will handle references
			}
			else if (typeof(IExposable).IsAssignableFrom(typeof(T)))
			{
				//Of course between games you can't get references so just save by name should be good enough.
				//objects saved here  need to be copies made with Clone(null)
				Scribe_Deep.Look(ref sel, "sel"); //And Clone will handle references
			}
			else
				Scribe_Values.Look(ref sel, "sel");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterWithOption<T> clone = (ListFilterWithOption<T>)base.Clone(map, newOwner);

			if (typeof(ILoadReferenceable).IsAssignableFrom(typeof(T)))
			{
				if (map == null)//SAVING: I don't have refName, but I make it and tell saved clone
				{
					clone.refName = sel == null ? "null" : MakeRefName();
				}
				else //LOADING: use my refName to resolve loaded clone's reference
				{
					if (refName == "null")
						clone.sel = default(T);
					else if (refName == null)
						clone.ResolveReference(MakeRefName(), map);//Cloning from ref to ref
					else
						clone.ResolveReference(refName, map);
				}
			}
			else
				clone.sel = sel;

			return clone;
		}
		public virtual string MakeRefName() => sel.ToString();
		public virtual void ResolveReference(string refName, Map map) => throw new NotImplementedException();
	}

	public enum DropDownDrawStyle {NameAndOptions, OptionsAndDrawSpecial}
	abstract class ListFilterDropDown<T> : ListFilterWithOption<T>
	{
		protected DropDownDrawStyle drawStyle;
		public int extraOption; //0 being use T, 1+ defined in subclass

		//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref extraOption, "ex");
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterDropDown<T> clone =
				extraOption == 0 ?
				(ListFilterDropDown<T>)base.Clone(map, newOwner) ://ListFilterwithOption handles sel, and refName for sel if needed
				(ListFilterDropDown<T>)BaseClone(map, newOwner);  //This is not needed with extraOption, so bypass ListFilterWithOption<T> to ListFilter
			clone.extraOption = extraOption;
			return clone;
		}

		private string GetLabel() => extraOption > 0 ? NameForExtra(extraOption): sel != null ? NameFor(sel) : NullOption();
		public virtual string NullOption() => null;
		public virtual IEnumerable<T> Options()
		{
			if (typeof(T).IsEnum)
				return Enum.GetValues(typeof(T)).OfType<T>();
			if (typeof(Def).IsAssignableFrom(typeof(T)))
				return GenDefDatabase.GetAllDefsInDatabaseForDef(typeof(T)).Cast<T>();
			throw new NotImplementedException();
		}
		public virtual bool Ordered => false;
		public virtual string NameFor(T o) => o.ToString();
		public override string MakeRefName() => NameFor(sel);
		protected virtual void Callback(T o) { sel = o; extraOption = 0; }

		public virtual int ExtraOptionsCount => 0;
		private IEnumerable<int> ExtraOptions() => Enumerable.Range(1, ExtraOptionsCount);
		public virtual string NameForExtra(int ex) => throw new NotImplementedException();
		private void CallbackExtra(int ex) => extraOption = ex;

		public override bool DrawOption(Rect rect)
		{
			bool changeSelection = false;
			bool changed = false;
			switch(drawStyle)
			{
				case DropDownDrawStyle.NameAndOptions:
					base.DrawOption(rect);
					changeSelection = Widgets.ButtonText(rect.RightPart(0.4f), GetLabel());
					break;
				case DropDownDrawStyle.OptionsAndDrawSpecial:
					WidgetRow row = new WidgetRow(rect.x, rect.y);
					changeSelection = row.ButtonText(GetLabel());

					rect.xMin = row.FinalX;
					changed = DrawSpecial(rect, row);
					break;
			}
			if (changeSelection)
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOption(nullOption, () => Callback(default(T))));
				foreach (T o in Ordered ? Options().OrderBy(o => NameFor(o)) : Options())
					options.Add(new FloatMenuOption(NameFor(o), () => Callback(o)));
				foreach (int ex in ExtraOptions())
					options.Add(new FloatMenuOption(NameForExtra(ex), () => CallbackExtra(ex)));
				MainTabWindow_List.DoFloatMenu(options);

				changed = true;
			}
			return changed;
		}

		//Use either rect or WidgetRow
		public virtual bool DrawSpecial(Rect rect, WidgetRow row) => throw new NotImplementedException();
	}

	class ListFilterDesignation : ListFilterDropDown<DesignationDef>
	{
		public override bool FilterApplies(Thing thing) =>
			sel != null ?
			(sel.targetType == TargetType.Thing ? thing.MapHeld.designationManager.DesignationOn(thing, sel) != null :
			thing.MapHeld.designationManager.DesignationAt(thing.PositionHeld, sel) != null) :
			(thing.MapHeld.designationManager.DesignationOn(thing) != null ||
			thing.MapHeld.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string NullOption() => "Any";
		public override IEnumerable<DesignationDef> Options() =>
			ContentsUtility.onlyAvailable ?
				Find.CurrentMap.designationManager.allDesignations.Select(d => d.def).Distinct() :
				base.Options();

		public override bool Ordered => true;
		public override string NameFor(DesignationDef o) => o.defName; // no labels on Designation def
	}

	class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		public override bool FilterApplies(Thing thing)
		{
			CompRottable rot = thing.TryGetComp<CompRottable>();
			return 
				extraOption == 1 ? rot != null : 
				extraOption == 2 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) is float r && r>0 && r<1 : 
				extraOption == 3 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) <= 0 : 
				rot?.Stage == sel;
		}
		
		public override int ExtraOptionsCount => 3;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "Spoils" :
			ex == 2 ? "Refrigerated" : 
			"Frozen";
	}

	class ListFilterGrowth : ListFilterWithOption<FloatRange>
	{
		public ListFilterGrowth() => sel = FloatRange.ZeroToOne;

		public override bool FilterApplies(Thing thing) =>
			thing is Plant p && sel.Includes(p.Growth);
		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (sel != newRange)
			{
				sel = newRange;
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
		public override IEnumerable<Type> Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableOnMap(t => t.GetType()).OrderBy(NameFor).ToList() : 
				types;
	}

	class ListFilterFaction : ListFilterDropDown<FactionRelationKind>
	{
		public ListFilterFaction() => extraOption = 1;

		public override bool FilterApplies(Thing thing) =>
			extraOption == 1 ? thing.Faction == Faction.OfPlayer :
			extraOption == 2 ? thing.Faction == Faction.OfMechanoids :
			extraOption == 3 ? thing.Faction == Faction.OfInsects :
			extraOption == 4 ? thing.Faction == null || thing.Faction.def.hidden :
			(thing.Faction is Faction fac && fac != Faction.OfPlayer && fac.PlayerRelationKind == sel);

		public override int ExtraOptionsCount => 4;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "Player" :
			ex == 2 ? "Mechanoid" :
			ex == 3 ? "Insectoid" :
			"No Faction";
	}

	/* This is no good, CanHaveFaction includes rock walls.
	class ListFilterCanFaction : ListFilter
	{
		public override bool Applies(Thing thing) =>
			thing.def.CanHaveFaction;
	}*/

	class ListFilterItemCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterItemCategory() => sel = ThingCategoryDefOf.Root;

		public override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(sel);

		public override IEnumerable<ThingCategoryDef> Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableOnMap(ThingCategoryDefsOfThing) :
				base.Options();

		public static IEnumerable<ThingCategoryDef> ThingCategoryDefsOfThing(Thing thing)
		{
			if (thing.def.thingCategories == null)
				yield break;
			foreach (var def in thing.def.thingCategories)
			{
				yield return def;
				foreach (var pDef in def.Parents)
					yield return pDef;
			}
		}
		public override string NameFor(ThingCategoryDef o) => o.LabelCap;
	}

	class ListFilterSpecialFilter : ListFilterDropDown<SpecialThingFilterDef>
	{
		public ListFilterSpecialFilter() => sel = SpecialThingFilterDefOf.AllowFresh;

		public override bool FilterApplies(Thing thing) =>
			sel.Worker.Matches(thing);

		public override string NameFor(SpecialThingFilterDef o) => o.LabelCap;
	}

	enum ListCategory
	{
		Person,
		Animal,
		Item,
		Building,
		Natural,
		Plant,
		Other
	}
	class ListFilterCategory : ListFilterDropDown<ListCategory>
	{
		public override bool FilterApplies(Thing thing)
		{
			switch(sel)
			{
				case ListCategory.Person: return thing is Pawn pawn && !pawn.NonHumanlikeOrWildMan();
				case ListCategory.Animal: return thing is Pawn animal && animal.NonHumanlikeOrWildMan();
				case ListCategory.Item: return thing.def.alwaysHaulable;
				case ListCategory.Building: return thing is Building building && building.def.filthLeaving != ThingDefOf.Filth_RubbleRock;
				case ListCategory.Natural: return thing is Building natural && natural.def.filthLeaving == ThingDefOf.Filth_RubbleRock;
				case ListCategory.Plant: return thing is Plant;
				case ListCategory.Other: return !(thing is Pawn) && !(thing is Building) && !(thing is Plant) && !thing.def.alwaysHaulable;
			}
			return false;
		}
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
	}

	class ListFilterHP : ListFilterWithOption<FloatRange>
	{
		public ListFilterHP() => sel = FloatRange.ZeroToOne;

		public override bool FilterApplies(Thing thing)
		{
			float? pct = null;
			if (thing is Pawn pawn)
				pct = pawn.health.summaryHealth.SummaryHealthPercent;
			if (thing.def.useHitPoints)
				pct = (float)thing.HitPoints / thing.MaxHitPoints;
			return pct != null && sel.Includes(pct.Value);
		}

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (sel != newRange)
			{
				sel = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterQuality : ListFilterWithOption<QualityRange>
	{
		public ListFilterQuality() => sel = QualityRange.All;

		public override bool FilterApplies(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			sel.Includes(qc);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			QualityRange newRange = sel;
			Widgets.QualityRange(rect.RightPart(0.5f), id, ref newRange);
			if (sel != newRange)
			{
				sel = newRange;
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
			return 
				extraOption == 1 ? !thing.def.MadeFromStuff :
				extraOption > 1 ?	stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 2]) ?? false :
				sel == null ? stuff != null :
				stuff == sel;
		}

		public override string NullOption() => "Any";
		public override IEnumerable<ThingDef> Options() => 
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(t => t.Stuff)
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff);

		public override string NameFor(ThingDef o) => o.LabelCap;
		
		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount + 1;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "Not made from stuff" : 
			DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex-2]?.LabelCap;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;
	}

	class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				sel == null ? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				pawn.RaceProps.body.GetPartsWithDef(sel).Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string NullOption() => "Any";
		public override IEnumerable<BodyPartDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>())
				: base.Options();
		
		public override string NameFor(BodyPartDef o) => o.LabelCap;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "None";
	}


	enum BaseAreas { Home, BuildRoof, NoRoof, SnowClear };
	class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea() => sel = Find.CurrentMap?.areaManager.Home;

		public override void ResolveReference(string refName, Map map)
		{
			sel = map.areaManager.GetLabeled(refName);
			if (sel == null)
				Messages.Message($"Tried to load area Filter named ({refName}) but the current map doesn't have any by that name", MessageTypeDefOf.RejectInput);
		}
		public override bool ValidForAllMaps => extraOption > 0 || sel == null;

		public override bool FilterApplies(Thing thing)
		{
			Map map = thing.MapHeld;
			IntVec3 pos = thing.PositionHeld;

			if (extraOption == 5)
				return pos.Roofed(map);

			if(extraOption == 0)
				return sel != null ? sel[pos] :
				map.areaManager.AllAreas.Any(a => a[pos]);

			switch((BaseAreas)(extraOption - 1))
			{
				case BaseAreas.Home:			return map.areaManager.Home[pos];
				case BaseAreas.BuildRoof: return map.areaManager.BuildRoof[pos];
				case BaseAreas.NoRoof:		return map.areaManager.NoRoof[pos];
				case BaseAreas.SnowClear: return map.areaManager.SnowClear[pos];
			}
			return false;
		}

		public override string NullOption() => "Any";
		public override IEnumerable<Area> Options() => Find.CurrentMap.areaManager.AllAreas.Where(a => a is Area_Allowed);
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex)
		{
			if (ex == 5) return "Roofed";
			switch((BaseAreas)(ex - 1))
			{
				case BaseAreas.Home: return "Home";
				case BaseAreas.BuildRoof: return "Build Roof";
				case BaseAreas.NoRoof: return "No Roof";
				case BaseAreas.SnowClear: return "Snow Clear";
			}
			return "???";
		}
	}

	class ListFilterZone : ListFilterDropDown<Zone>
	{
		public override void ResolveReference(string refName, Map map)
		{
			sel = map.zoneManager.AllZones.FirstOrDefault(z => z.label == refName);
			if (sel == null)
				Messages.Message($"Tried to load zone Filter named ({refName}) but the current map doesn't have any by that name", MessageTypeDefOf.RejectInput);
		}
		public override bool ValidForAllMaps => extraOption != 0 || sel == null;

		public override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = thing.MapHeld.zoneManager.ZoneAt(pos);
			return 
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				sel != null ? zoneAtPos == sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "Any";
		public override IEnumerable<Zone> Options() => Find.CurrentMap.zoneManager.AllZones;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "Any Stockpile" : "Any Growing Zone";
	}

	class ListFilterDeterioration : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}

	enum DoorOpenFilter { Open, Close, HoldOpen, BlockedOpenMomentary }
	class ListFilterDoorOpen : ListFilterDropDown<DoorOpenFilter>
	{
		public override bool FilterApplies(Thing thing)
		{
			Building_Door door = thing as Building_Door;
			if (door == null) return false;
			switch (sel)
			{
				case DoorOpenFilter.Open: return door.Open;
				case DoorOpenFilter.Close: return !door.Open;
				case DoorOpenFilter.HoldOpen: return door.HoldOpen;
				case DoorOpenFilter.BlockedOpenMomentary: return door.BlockedOpenMomentary;
			}
			return false;//???
		}
		public override string NameFor(DoorOpenFilter o)
		{
			switch (o)
			{
				case DoorOpenFilter.Open: return "Opened";
				case DoorOpenFilter.Close: return "Closed";
				case DoorOpenFilter.HoldOpen: return "Hold Open";
				case DoorOpenFilter.BlockedOpenMomentary: return "Blocked Open";
			}
			return "???";
		}
	}
}
