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

			if (owner.locked)
			{
				row.Label(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate());
			}
			else
			{
				//Clear button
				if (row.ButtonIcon(TexButton.CancelTex, "TD.DeleteThisFilter".Translate()))
				{
					delete = true;
					changed = true;
				}

				//Toggle button
				if (row.ButtonIcon(enabled ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex, "TD.EnableThisFilter".Translate()))
				{
					enabled = !enabled;
					changed = true;
				}

				//Include/Exclude
				if (row.ButtonText(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate(), "TD.IncludeOrExcludeThingsMatchingThisFilter".Translate()))
				{
					include = !include;
					changed = true;
				}
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

				TooltipHandler.TipRegion(rowRect, "TD.ThisFilterDoesntWorkWithAllMaps".Translate());
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
		public ListFilterName() => Sel = "";

		public override bool FilterApplies(Thing thing) =>
			//thing.Label.Contains(sel, CaseInsensitiveComparer.DefaultInvariant);	//Contains doesn't accept comparer with strings. okay.
			defaultSel || thing.Label.IndexOf(Sel, StringComparison.OrdinalIgnoreCase) >= 0;

		public override bool DrawOption(Rect rect)
		{
			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.mouseDown && Event.current.button == 1)
			{
				GUI.FocusControl("");
				Event.current.Use();
			}

			GUI.SetNextControlName($"LIST_FILTER_NAME_INPUT{id}");
			string newStr = Widgets.TextField(rect.LeftPart(0.9f), Sel);
			if (newStr != Sel)
			{
				Sel = newStr;
				return true;
			}
			if (Widgets.ButtonImage(rect.RightPartPixels(rect.height), TexUI.RotLeftTex))
			{
				GUI.FocusControl("");
				Sel = "";
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
			switch (Sel)
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
		private T sel;//selection
		protected bool defaultSel = true;

		protected T Sel
		{
			get => sel;
			set
			{
				sel = value;
				defaultSel = sel == null || sel.Equals(default(T));
			}
		}

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

			//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
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
				//objects saved here need to be copies made with Clone(null)
				Scribe_Values.Look(ref refName, "refName"); //And Clone will handle references
			}
			else if (typeof(IExposable).IsAssignableFrom(typeof(T)))
			{
				Scribe_Deep.Look(ref sel, "sel");
			}
			else
				Scribe_Values.Look(ref sel, "sel");

			if(Scribe.mode == LoadSaveMode.PostLoadInit)
				defaultSel = sel == null || sel.Equals(default(T));	//since we cant work with 'Sel = ...' with refs
		}
		public override ListFilter Clone(Map map, FindDescription newOwner)
		{
			ListFilterWithOption<T> clone = (ListFilterWithOption<T>)base.Clone(map, newOwner);

			if (typeof(ILoadReferenceable).IsAssignableFrom(typeof(T)))
			{
				if (map == null)//SAVING: I don't have refName, but I make it and tell saved clone
				{
					clone.refName = Sel == null ? "null" : MakeRefName();
				}
				else //LOADING: use my refName to resolve loaded clone's reference
				{
					if (refName == "null")
					{
						clone.Sel = default(T);
					}
					{
						if (refName == null)
							clone.ResolveReference(MakeRefName(), map);//Cloning from ref to ref
						else
							clone.ResolveReference(refName, map);

						if (clone.Sel == null)
							Messages.Message("TD.TriedToLoad0FilterNamed1ButTheCurrentMapDoesntHaveAnyByThatName".Translate(def.LabelCap, refName), MessageTypeDefOf.RejectInput);
					}
				}
			}
			else
				clone.Sel = Sel;

			return clone;
		}
		public virtual string MakeRefName() => Sel.ToString();
		public virtual void ResolveReference(string refName, Map map) => throw new NotImplementedException();
	}

	public enum DropDownDrawStyle {NameAndOptions, OptionsAndDrawSpecial}
	abstract class ListFilterDropDown<T> : ListFilterWithOption<T>
	{
		protected DropDownDrawStyle drawStyle;
		public int extraOption; //0 being use T, 1+ defined in subclass

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

		private string GetLabel() => extraOption > 0 ? NameForExtra(extraOption): Sel != null ? NameFor(Sel) : NullOption();
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
		public virtual string NameFor(T o) => o is Def def ? def.LabelCap : typeof(T).IsEnum ? o.TranslateEnum() : o.ToString();
		public override string MakeRefName() => NameFor(Sel);	//refname should not apply for defs or enums so this'll be ^^ o.ToString()
		protected virtual void Callback(T o) { Sel = o; extraOption = 0; }

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
			Sel != null ?
			(Sel.targetType == TargetType.Thing ? thing.MapHeld.designationManager.DesignationOn(thing, Sel) != null :
			thing.MapHeld.designationManager.DesignationAt(thing.PositionHeld, Sel) != null) :
			(thing.MapHeld.designationManager.DesignationOn(thing) != null ||
			thing.MapHeld.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string NullOption() => "TD.AnyOption".Translate();
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
				rot?.Stage == Sel;
		}

		public override string NameFor(RotStage o) => ("RotState"+o.ToString()).Translate();

		public override int ExtraOptionsCount => 3;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.Spoils".Translate() :
			ex == 2 ? "TD.Refrigerated".Translate() : 
			"TD.Frozen".Translate();
	}

	class ListFilterGrowth : ListFilterWithOption<FloatRange>
	{
		public ListFilterGrowth() => Sel = FloatRange.ZeroToOne;

		public override bool FilterApplies(Thing thing) =>
			thing is Plant p && Sel.Includes(p.Growth);
		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = Sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (Sel != newRange)
			{
				Sel = newRange;
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

	class ListFilterPlantDies : ListFilter
	{
		public override bool FilterApplies(Thing thing) =>
			thing is Plant plant && (plant.def.plant?.dieIfLeafless ?? false);
	}

	class ListFilterClassType : ListFilterDropDown<Type>
	{
		public ListFilterClassType() => Sel = typeof(Thing);

		public override bool FilterApplies(Thing thing) =>
			Sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t=>t.ToString()).ToList();
		public override IEnumerable<Type> Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.GetType()).OrderBy(NameFor).ToList() : 
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
			(thing.Faction is Faction fac && fac != Faction.OfPlayer && fac.PlayerRelationKind == Sel);

		public override string NameFor(FactionRelationKind o) => o.GetLabel();

		public override int ExtraOptionsCount => 4;
		public override string NameForExtra(int ex) => // or FleshTypeDef but this works
			ex == 1 ? "TD.Player".Translate() :
			ex == 2 ? "TD.Mechanoid".Translate() :
			ex == 3 ? "TD.Insectoid".Translate() :
			"TD.NoFaction".Translate();
	}
	
	class ListFilterItemCategory : ListFilterDropDown<ThingCategoryDef>
	{
		public ListFilterItemCategory() => Sel = ThingCategoryDefOf.Root;

		public override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(Sel);

		public override IEnumerable<ThingCategoryDef> Options() =>
			ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableInGame(ThingCategoryDefsOfThing) :
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
	}

	class ListFilterSpecialFilter : ListFilterDropDown<SpecialThingFilterDef>
	{
		public ListFilterSpecialFilter() => Sel = SpecialThingFilterDefOf.AllowFresh;

		public override bool FilterApplies(Thing thing) =>
			Sel.Worker.Matches(thing);
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
			switch(Sel)
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
			switch (Sel)
			{
				case MineableType.Resource: return thing.def.building?.isResourceRock ?? false;
				case MineableType.Rock: return (thing.def.building?.isNaturalRock ?? false) && (!thing.def.building?.isResourceRock ?? true);
				case MineableType.All: return thing.def.mineable;
			}
			return false;
		}
	}

	class ListFilterHP : ListFilterWithOption<FloatRange>
	{
		public ListFilterHP() => Sel = FloatRange.ZeroToOne;

		public override bool FilterApplies(Thing thing)
		{
			float? pct = null;
			if (thing is Pawn pawn)
				pct = pawn.health.summaryHealth.SummaryHealthPercent;
			if (thing.def.useHitPoints)
				pct = (float)thing.HitPoints / thing.MaxHitPoints;
			return pct != null && Sel.Includes(pct.Value);
		}

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			FloatRange newRange = Sel;
			Widgets.FloatRange(rect.RightPart(0.5f), id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (Sel != newRange)
			{
				Sel = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterQuality : ListFilterWithOption<QualityRange>
	{
		public ListFilterQuality() => Sel = QualityRange.All;

		public override bool FilterApplies(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			Sel.Includes(qc);

		public override bool DrawOption(Rect rect)
		{
			base.DrawOption(rect);
			QualityRange newRange = Sel;
			Widgets.QualityRange(rect.RightPart(0.5f), id, ref newRange);
			if (Sel != newRange)
			{
				Sel = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterStuff : ListFilterDropDown<ThingDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			ThingDef stuff = thing is IConstructible c ? c.EntityToBuildStuff() : thing.Stuff;
			return 
				extraOption == 1 ? !thing.def.MadeFromStuff :
				extraOption > 1 ?	stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 2]) ?? false :
				Sel == null ? stuff != null :
				stuff == Sel;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<ThingDef> Options() => 
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(t => t.Stuff)
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff);
		
		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount + 1;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.NotMadeFromStuff".Translate() : 
			DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex-2]?.LabelCap;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		public override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == Sel;
	}

	class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				Sel == null ? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				pawn.RaceProps.body.GetPartsWithDef(Sel).Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<BodyPartDef> Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableInGame(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>())
				: base.Options();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "None".Translate();
	}


	enum BaseAreas { Home, BuildRoof, NoRoof, SnowClear };
	class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea() => extraOption = 1;

		public override void ResolveReference(string refName, Map map) =>
			Sel = map.areaManager.GetLabeled(refName);

		public override bool ValidForAllMaps => extraOption > 0 || Sel == null;

		public override bool FilterApplies(Thing thing)
		{
			Map map = thing.MapHeld;
			IntVec3 pos = thing.PositionHeld;

			if (extraOption == 5)
				return pos.Roofed(map);

			if(extraOption == 0)
				return Sel != null ? Sel[pos] :
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

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap.areaManager.AllAreas.Where(a => a is Area_Allowed);
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 5;
		public override string NameForExtra(int ex)
		{
			if (ex == 5) return "Roofed".Translate().CapitalizeFirst();
			switch((BaseAreas)(ex - 1))
			{
				case BaseAreas.Home: return "Home".Translate();
				case BaseAreas.BuildRoof: return "BuildRoof".Translate().CapitalizeFirst();
				case BaseAreas.NoRoof: return "NoRoof".Translate().CapitalizeFirst();
				case BaseAreas.SnowClear: return "SnowClear".Translate().CapitalizeFirst();
			}
			return "???";
		}
	}

	class ListFilterZone : ListFilterDropDown<Zone>
	{
		public override void ResolveReference(string refName, Map map) =>
			Sel = map.zoneManager.AllZones.FirstOrDefault(z => z.label == refName);

		public override bool ValidForAllMaps => extraOption != 0 || Sel == null;

		public override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = thing.MapHeld.zoneManager.ZoneAt(pos);
			return 
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				Sel != null ? zoneAtPos == Sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Zone> Options() => Find.CurrentMap.zoneManager.AllZones;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "TD.AnyStockpile".Translate() : "TD.AnyGrowingZone".Translate();
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
			switch (Sel)
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
				case DoorOpenFilter.Open: return "TD.Opened".Translate();
				case DoorOpenFilter.Close: return "VentClosed".Translate();
				case DoorOpenFilter.HoldOpen: return "CommandToggleDoorHoldOpen".Translate().CapitalizeFirst();
				case DoorOpenFilter.BlockedOpenMomentary: return "TD.BlockedOpen".Translate();
			}
			return "???";
		}
	}

	class ListFilterThingDef : ListFilterDropDown<ThingDef>
	{
		public ListFilterThingDef() => Sel = ThingDefOf.WoodLog;

		public override bool FilterApplies(Thing thing) =>
			Sel == thing.def;
		
		public override IEnumerable<ThingDef> Options() =>
			(ContentsUtility.onlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.def) :
				base.Options())
			.Where(def => FindDescription.ValidDef(def));

		public override bool Ordered => true;
	}

}
