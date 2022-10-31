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
	public class ListFilterDef : ListFilterSelectableDef
	{
		public Type filterClass;

		public override IEnumerable<string> ConfigErrors()
		{
			if (filterClass == null)
				yield return "ListFilterDef needs filterClass set";
		}
	}

	public abstract partial class ListFilter : IExposable
	{
		public ListFilterDef def;

		public IFilterHolder parent;
		// parent is not set after ExposeData, that'll be done in Clone.
		// parent is only used in UI or actual processing so as is made clear below,
		// An ExpostData-loaded ListFilter needs to be cloned before actual use

		public FindDescription RootFindDesc => parent.RootFindDesc;


		protected int id; //For Widgets.draggingId purposes
		private static int nextID = 1;
		protected ListFilter() { id = nextID++; }


		private bool enabled = true; //simply turn off but keep in list
		public bool Enabled => enabled && DisableReason == null;

		private bool include = true; //or exclude


		// Okay, save/load. The basic gist here is:
		// ExposeData saves any filter fine.
		// ExposeData can load a filter for reference, but it's not yet usable.
		// After ExposeData loading, filters need to be cloned
		// After Cloning, they get DoResolveReference on a map
		// Then filters can actually be used.


		// Any overridden ExposeData+Clone should copy data but not process much.
		// If there's proessing to do, do it in ResolveReference. 
		// e.g. ListFilterWithOption sets refName in Clone,
		//  but sets the actual selection in ResolveReference

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
			//clone.parent = newHolder; //No - MakeFilter just set it.
			return clone;
		}
		public virtual void DoResolveReference(Map map) { }


		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return Enabled ? list.Where(t => AppliesTo(t)) : list;
		}

		//This can be problematic for minified things: We want the qualities of the inner things,
		// but position/status of outer thing. So it just checks both -- but then something like 'no stuff' always applies. Oh well
		public bool AppliesTo(Thing thing)
		{
			bool applies = FilterApplies(thing);
			if (!applies && thing.GetInnerThing() is Thing innerThing && innerThing != thing)
				applies = FilterApplies(innerThing);

			return applies == include;
		}

		protected abstract bool FilterApplies(Thing thing);


		private bool shouldFocus;
		public void Focus() => shouldFocus = true;
		protected virtual void DoFocus() { }


		// Seems to be GameFont.Small on load so we're good
		public static float? incExcWidth;
		public static float IncExcWidth =>
			incExcWidth.HasValue ? incExcWidth.Value :
			(incExcWidth = Mathf.Max(Text.CalcSize("TD.IncludeShort".Translate()).x, Text.CalcSize("TD.ExcludeShort".Translate()).x)).Value;

		public (bool, bool) Listing(Listing_StandardIndent listing, bool locked)
		{
			Rect rowRect = listing.GetRect(Text.LineHeight);
			WidgetRow row = new WidgetRow(rowRect.xMax, rowRect.y, UIDirection.LeftThenDown, rowRect.width);

			bool changed = false;
			bool delete = false;

			if (locked)
			{
				row.Label(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate(),
					IncExcWidth, "TD.IncludeOrExcludeThingsMatchingThisFilter".Translate());
				row.Gap(4);
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
				if (row.ButtonText(include ? "TD.IncludeShort".Translate() : "TD.ExcludeShort".Translate(),
					"TD.IncludeOrExcludeThingsMatchingThisFilter".Translate(),
					fixedWidth: IncExcWidth))
				{
					include = !include;
					changed = true;
				}
			}


			//Draw option row
			rowRect.width -= (rowRect.xMax - row.FinalX);
			changed |= DrawMain(rowRect, locked);
			changed |= DrawUnder(listing, locked);
			if (shouldFocus)
			{
				DoFocus();
				shouldFocus = false;
			}
			if (DisableReason is string reason)
			{
				Widgets.DrawBoxSolid(rowRect, new Color(0.5f, 0, 0, 0.25f));

				TooltipHandler.TipRegion(rowRect, reason);
			}

			listing.Gap(listing.verticalSpacing);
			return (changed, delete);
		}


		public virtual bool DrawMain(Rect rect, bool locked)
		{
			Widgets.Label(rect, def.LabelCap);
			return false;
		}
		protected virtual bool DrawUnder(Listing_StandardIndent listing, bool locked) => false;

		public virtual bool ValidForAllMaps => true && !CurrentMapOnly;
		public virtual bool CurrentMapOnly => false;

		public virtual string DisableReason =>
			!ValidForAllMaps && RootFindDesc.allMaps
				? "TD.ThisFilterDoesntWorkWithAllMaps".Translate()
				: null;

		public void DoFloatOptions(List<FloatMenuOption> options)
		{
			if (options.NullOrEmpty())
				Messages.Message("TD.ThereAreNoOptionsAvailablePerhapsYouShouldUncheckOnlyAvailableThings".Translate(), MessageTypeDefOf.RejectInput);
			else
			{
				foreach (FloatMenuOption opt in options)
				{
					// append RootFindDesc.RemakeList to actions
					Action action = opt.action;
					opt.action = () => { action(); RootFindDesc.RemakeList(); };

					Find.WindowStack.Add(new FloatMenu(options));
				}
			}
		}

		public virtual bool Check(Predicate<ListFilter> check) => check(this);
	}

	class ListFilterName : ListFilterWithOption<string>
	{
		public ListFilterName() => sel = "";

		protected override bool FilterApplies(Thing thing) =>
			//thing.Label.Contains(sel, CaseInsensitiveComparer.DefaultInvariant);	//Contains doesn't accept comparer with strings. okay.
			sel == "" || thing.Label.IndexOf(sel, StringComparison.OrdinalIgnoreCase) >= 0;

		public static readonly string namedLabel = "Named: ";
		public static float? namedLabelWidth;
		public static float NamedLabelWidth =>
			namedLabelWidth.HasValue ? namedLabelWidth.Value :
			(namedLabelWidth = Text.CalcSize(namedLabel).x).Value;

		public override bool DrawMain(Rect rect, bool locked)
		{
			Widgets.Label(rect, namedLabel);
			rect.xMin += NamedLabelWidth;

			if(locked)
			{
				Widgets.Label(rect, '"' + sel + '"');
				return false;
			}

			if (GUI.GetNameOfFocusedControl() == $"LIST_FILTER_NAME_INPUT{id}" &&
				Mouse.IsOver(rect) && Event.current.type == EventType.MouseDown && Event.current.button == 1)
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
		protected override bool FilterApplies(Thing thing)
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
		// selection
		private T _sel;
		protected string refName;// if UsesRefName,  = SaveLoadXmlConstants.IsNullAttributeName;
		private int _extraOption; //0 meaning use _sel, what 1+ means is defined in subclass

		// A subclass with extra fields needs to override ExposeData and Clone to copy them

		public string selectionError; // Probably set on load when selection is invalid (missing mod?)
		public override string DisableReason => base.DisableReason ?? selectionError;

		// would like this to be T const * sel;
		public T sel
		{
			get => _sel;
			set
			{
				_sel = value;
				_extraOption = 0;
				selectionError = null;
				if (UsesRefName) refName = MakeRefName();
				PostSelected();
			}
		}

		// A subclass should often set sel in the constructor
		// which will call the property setter above
		// If the default is null, and there's no PostSelected to do,
		// then it's fine to skip defining a constructor
		protected ListFilterWithOption()
		{
			if (UsesRefName)
				refName = SaveLoadXmlConstants.IsNullAttributeName;
		}
		protected virtual void PostSelected()
		{
			// A subclass with fields whose validity depends on the selection should override this
			// Most common usage is to set a default value that is valid for the selection
			// e.g. the skill filter has a range 0-20, but that's valid for all skills, so no need to reset here
			// e.g. the hediff filter has a range too, but that depends on the selected hediff, so the selected range needs to be set here
		}

		// This method works double duty:
		// Both telling if Sel can be set to null, and the string to show for null selection
		public virtual string NullOption() => null;

		protected int extraOption
		{
			get => _extraOption;
			set
			{
				_extraOption = value;
				_sel = default;
				selectionError = null;
				refName = null;
			}
		}

		//Okay, so, references.
		//A simple filter e.g. string search is usable everywhere.
		//In-game, as an alert, as a saved filter to load in, saved to file to load into another game, etc.
		//ExposeData and Clone can just copy T sel, because a string is the same everywhere.
		//But a filter that references in-game things can't be used universally
		//When such a filter is run in-game, it does of course set 'sel' and reference it like normal
		//But when such a filter is saved, it cannot be bound to an instance
		//So ExposeData saves and loads 'string refName' instead of the 'T sel'
		//When showing that filter as an option to load, that's fine, sel isn't set but refName is.
		//When the filter is copied, loaded or saved in any way, it is cloned with Clone(), which will copy refName but not sel
		//When loading or copying into a map, whoever called Clone will also call ResolveReference(Map) to bind to that map
		//(even if a copy ends up referencing the same thing, the reference is re-resolved for simplicity's sake)

		//TL;DR there are two 'modes' a ListFilter can be: active or inactive.
		//When active, it's bound to a map, ready to do actual filtering based on sel
		//When inactive, it's in storage - it only knows the name of sel
		//When loading an inactive filter, the refname+map are used to find and set sel
		//When saving an active filter, just refname is saved
		//When copinying an active filter, refname is copied and sel is found again
		//(Of course if you don't use refname, the filter just copies sel around)

		protected readonly static bool IsDef = typeof(Def).IsAssignableFrom(typeof(T));
		protected readonly static bool IsRef = typeof(ILoadReferenceable).IsAssignableFrom(typeof(T));
		protected readonly static bool IsEnum = typeof(T).IsEnum;

		public virtual bool UsesRefName => IsRef || IsDef;
		protected virtual string MakeRefName() => sel?.ToString() ?? SaveLoadXmlConstants.IsNullAttributeName;

		// Subclasses where UsesRefName==true need to implement ResolveReference()
		// (unless it's just a Def)
		// return matching object based on refName (refName will not be "null")
		// returning null produces a selection error and the filter will be disabled
		protected virtual T ResolveReference(Map map)
		{
			if (IsDef)
			{
				//Scribe_Defs.Look doesn't work since it needs the subtype of "Def" and T isn't boxed to be a Def so DefFromNodeUnsafe instead
				//_sel = ScribeExtractor.DefFromNodeUnsafe<T>(Scribe.loader.curXmlParent["sel"]);

				//DefFromNodeUnsafe also doesn't work since it logs errors - so here's custom code copied to remove the logging:

				return (T)(object)GenDefDatabase.GetDefSilentFail(typeof(T), refName, false);
			}

			throw new NotImplementedException();
		}

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref _extraOption, "ex");
			if (_extraOption > 0)
			{
				if (Scribe.mode == LoadSaveMode.LoadingVars)
					extraOption = _extraOption;	// property setter to set other fields null

				// No need to worry about sel or refname, we're done!
				return;
			}

			//Oh Jesus T can be anything but Scribe doesn't like that much flexibility so here we are:
			//(avoid using property 'sel' so it doesn't MakeRefName())
			if (UsesRefName)
			{
				// Of course between games you can't get references so just save by name should be good enough
				// (even if it's from the same game, it can still resolve the reference all the same)

				// Saving a null refName saves "IsNull"
				Scribe_Values.Look(ref refName, "refName");

				// ResolveReferences() will be called when loaded onto a map for actual use
			}
			else if (typeof(IExposable).IsAssignableFrom(typeof(T)))
			{
				//This might just be to handle ListFilterSelection
				Scribe_Deep.Look(ref _sel, "sel");
			}
			else
				Scribe_Values.Look(ref _sel, "sel");
		}
		public override ListFilter Clone()
		{
			ListFilterWithOption<T> clone = (ListFilterWithOption<T>)base.Clone();

			clone.extraOption = extraOption;
			if (extraOption > 0)
				return clone;

			if (UsesRefName)
				clone.refName = refName;
			else
				clone._sel = _sel;	//todo handle if sel needs to be deep-copied. Perhaps sel should be T const * sel...

			return clone;
		}
		public override void DoResolveReference(Map map)
		{
			if (!UsesRefName || extraOption > 0) return;

			if (refName == SaveLoadXmlConstants.IsNullAttributeName)
			{
				_sel = default; //can't use null because generic T isn't bound as reftype
			}
			else
			{
				_sel = ResolveReference(map);

				if (_sel == null)
				{
					selectionError = $"Missing {def.LabelCap}: {refName}?";
					Messages.Message("TD.TriedToLoad0FilterNamed1ButCouldNotBeFound".Translate(def.LabelCap, refName), MessageTypeDefOf.RejectInput);
				}
			}
		}
	}

	abstract class ListFilterDropDown<T> : ListFilterWithOption<T>
	{
		private string GetLabel()
		{
			if (selectionError != null)
				return refName;

			if (extraOption > 0)
				return NameForExtra(extraOption);

			if (sel != null)
				return NameFor(sel);

			return NullOption() ?? "??Null selection??";
		}

		public virtual IEnumerable<T> Options()
		{
			if (IsEnum)
				return Enum.GetValues(typeof(T)).OfType<T>();
			if (IsDef)
				return GenDefDatabase.GetAllDefsInDatabaseForDef(typeof(T)).Cast<T>();
			throw new NotImplementedException();
		}

		public virtual bool Ordered => false;
		public virtual string NameFor(T o) => o is Def def ? def.LabelCap.Resolve() : typeof(T).IsEnum ? o.TranslateEnum() : o.ToString();
		protected override string MakeRefName()
		{
			if (sel is Def def)
				return def.defName;

			// Many subclasses will just use NameFor, so do it here.
			return sel != null ? NameFor(sel) : base.MakeRefName();
		}

		public virtual int ExtraOptionsCount => 0;
		private IEnumerable<int> ExtraOptions() => Enumerable.Range(1, ExtraOptionsCount);
		public virtual string NameForExtra(int ex) => throw new NotImplementedException();

		public override bool DrawMain(Rect rect, bool locked)
		{
			bool changeSelection = false;
			bool changed = false;
			if (HasSpecial)
			{
				// No label, selected option button on left, special on right
				WidgetRow row = new WidgetRow(rect.x, rect.y);
				row.Label(def.LabelCap);
				changeSelection = row.ButtonText(GetLabel());

				rect.xMin = row.FinalX;
				changed = DrawCustom(rect, row);
			}
			else
			{
				//Just the label on left, and selected option button on right
				base.DrawMain(rect, locked);
				changeSelection = Widgets.ButtonText(rect.RightPart(0.4f), GetLabel());
			}
			if (changeSelection)
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();

				if (NullOption() is string nullOption)
					options.Add(new FloatMenuOption(nullOption, () => sel = default)); //can't null because T isn't bound as reftype

				foreach (T o in Ordered ? Options().OrderBy(o => NameFor(o)) : Options())
					options.Add(new FloatMenuOption(NameFor(o), () => sel = o));

				foreach (int ex in ExtraOptions())
					options.Add(new FloatMenuOption(NameForExtra(ex), () => extraOption = ex));

				DoFloatOptions(options);
			}
			return changed;
		}

		// Subclass can override DrawCustom to draw anything custom
		// (otherwise it's just label and option selection button)
		// Use either rect or WidgetRow in the implementation
		public virtual bool DrawCustom(Rect rect, WidgetRow row) => throw new NotImplementedException();

		// Auto detection of subclasses that use DrawCustom:
		private static readonly HashSet<Type> specialDrawers = null;
		private bool HasSpecial => specialDrawers?.Contains(GetType()) ?? false;
		static ListFilterDropDown()//<T>	//Remember there's a specialDrawers for each <T> but functionally that doesn't change anything
		{
			Type baseType = typeof(ListFilterDropDown<T>);
			foreach (Type subclass in baseType.AllSubclassesNonAbstract())
			{
				if (subclass.GetMethod(nameof(DrawCustom)).DeclaringType != baseType)
				{
					if(specialDrawers == null)
						specialDrawers = new HashSet<Type>();

					specialDrawers.Add(subclass);
				}
			}
		}
	}

	class ListFilterDesignation : ListFilterDropDown<DesignationDef>
	{
		protected override bool FilterApplies(Thing thing) =>
			sel != null ?
			(sel.targetType == TargetType.Thing ? thing.MapHeld.designationManager.DesignationOn(thing, sel) != null :
			thing.MapHeld.designationManager.DesignationAt(thing.PositionHeld, sel) != null) :
			(thing.MapHeld.designationManager.DesignationOn(thing) != null ||
			thing.MapHeld.designationManager.AllDesignationsAt(thing.PositionHeld).Count() > 0);

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<DesignationDef> Options() =>
			ContentsUtility.OnlyAvailable ?
				Find.CurrentMap.designationManager.AllDesignations.Select(d => d.def).Distinct() :
				base.Options();

		public override bool Ordered => true;
		public override string NameFor(DesignationDef o) => o.defName; // no labels on Designation def
	}

	class ListFilterFreshness : ListFilterDropDown<RotStage>
	{
		protected override bool FilterApplies(Thing thing)
		{
			CompRottable rot = thing.TryGetComp<CompRottable>();
			return 
				extraOption == 1 ? rot != null : 
				extraOption == 2 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) is float r && r>0 && r<1 : 
				extraOption == 3 ? GenTemperature.RotRateAtTemperature(thing.AmbientTemperature) <= 0 : 
				rot?.Stage == sel;
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
		public ListFilterGrowth() => sel = FloatRange.ZeroToOne;

		protected override bool FilterApplies(Thing thing) =>
			thing is Plant p && sel.Includes(p.Growth);
		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
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
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && plant.HarvestableNow;
	}

	class ListFilterPlantCrop : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && plant.IsCrop;
	}

	class ListFilterPlantDies : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Plant plant && (plant.def.plant?.dieIfLeafless ?? false);
	}

	class ListFilterClassType : ListFilterDropDown<Type>
	{
		public ListFilterClassType() => sel = typeof(Thing);

		protected override bool FilterApplies(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t=>t.ToString()).ToList();
		public override IEnumerable<Type> Options() =>
			ContentsUtility.OnlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.GetType()).OrderBy(NameFor).ToList() : 
				types;
	}

	class ListFilterFaction : ListFilterDropDown<FactionRelationKind>
	{
		public ListFilterFaction() => extraOption = 1;

		protected override bool FilterApplies(Thing thing) =>
			extraOption == 1 ? thing.Faction == Faction.OfPlayer :
			extraOption == 2 ? thing.Faction == Faction.OfMechanoids :
			extraOption == 3 ? thing.Faction == Faction.OfInsects :
			extraOption == 4 ? thing.Faction == null || thing.Faction.def.hidden :
			(thing.Faction is Faction fac && fac != Faction.OfPlayer && fac.PlayerRelationKind == sel);

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
		public ListFilterItemCategory() => sel = ThingCategoryDefOf.Root;

		protected override bool FilterApplies(Thing thing) =>
			thing.def.IsWithinCategory(sel);

		public override IEnumerable<ThingCategoryDef> Options() =>
			ContentsUtility.OnlyAvailable ?
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
		public ListFilterSpecialFilter() => sel = SpecialThingFilterDefOf.AllowFresh;

		protected override bool FilterApplies(Thing thing) =>
			sel.Worker.Matches(thing);
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
		protected override bool FilterApplies(Thing thing)
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
		protected override bool FilterApplies(Thing thing)
		{
			switch (sel)
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
		public ListFilterHP() => sel = FloatRange.ZeroToOne;

		protected override bool FilterApplies(Thing thing)
		{
			float? pct = null;
			if (thing is Pawn pawn)
				pct = pawn.health.summaryHealth.SummaryHealthPercent;
			if (thing.def.useHitPoints)
				pct = (float)thing.HitPoints / thing.MaxHitPoints;
			return pct != null && sel.Includes(pct.Value);
		}

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
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

		protected override bool FilterApplies(Thing thing) =>
			thing.TryGetQuality(out QualityCategory qc) &&
			sel.Includes(qc);

		public override bool DrawMain(Rect rect, bool locked)
		{
			base.DrawMain(rect, locked);
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
		protected override bool FilterApplies(Thing thing)
		{
			ThingDef stuff = thing is IConstructible c ? c.EntityToBuildStuff() : thing.Stuff;
			return 
				extraOption == 1 ? !thing.def.MadeFromStuff :
				extraOption > 1 ?	stuff?.stuffProps?.categories?.Contains(DefDatabase<StuffCategoryDef>.AllDefsListForReading[extraOption - 2]) ?? false :
				sel == null ? stuff != null :
				stuff == sel;
		}
		
		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<ThingDef> Options() => 
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => t.Stuff)
				: DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.IsStuff);
		
		public override int ExtraOptionsCount => DefDatabase<StuffCategoryDef>.DefCount + 1;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.NotMadeFromStuff".Translate() : 
			DefDatabase<StuffCategoryDef>.AllDefsListForReading[ex-2]?.LabelCap;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;
	}

	class ListFilterMissingBodyPart : ListFilterDropDown<BodyPartDef>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				sel == null ? !pawn.health.hediffSet.GetMissingPartsCommonAncestors().NullOrEmpty() :
				pawn.RaceProps.body.GetPartsWithDef(sel).Any(r => pawn.health.hediffSet.PartIsMissing(r));
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<BodyPartDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(
					t => (t as Pawn)?.health.hediffSet.GetMissingPartsCommonAncestors().Select(h => h.Part.def) ?? Enumerable.Empty<BodyPartDef>())
				: base.Options();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "None".Translate();
	}


	enum BaseAreas { Home, BuildRoof, NoRoof, SnowClear };
	class ListFilterArea : ListFilterDropDown<Area>
	{
		public ListFilterArea()
		{
			extraOption = 1;
		}

		protected override Area ResolveReference(Map map) =>
			map.areaManager.GetLabeled(refName);

		public override bool ValidForAllMaps => extraOption > 0 || sel == null;

		protected override bool FilterApplies(Thing thing)
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
		protected override Zone ResolveReference(Map map) =>
			map.zoneManager.AllZones.FirstOrDefault(z => z.label == refName);

		public override bool ValidForAllMaps => extraOption != 0 || sel == null;

		protected override bool FilterApplies(Thing thing)
		{
			IntVec3 pos = thing.PositionHeld;
			Zone zoneAtPos = thing.MapHeld.zoneManager.ZoneAt(pos);
			return 
				extraOption == 1 ? zoneAtPos is Zone_Stockpile :
				extraOption == 2 ? zoneAtPos is Zone_Growing :
				sel != null ? zoneAtPos == sel :
				zoneAtPos != null;
		}

		public override string NullOption() => "TD.AnyOption".Translate();
		public override IEnumerable<Zone> Options() => Find.CurrentMap.zoneManager.AllZones;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) => ex == 1 ? "TD.AnyStockpile".Translate() : "TD.AnyGrowingZone".Translate();
	}

	class ListFilterDeterioration : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			SteadyEnvironmentEffects.FinalDeteriorationRate(thing) >= 0.001f;
	}

	enum DoorOpenFilter { Open, Close, HoldOpen, BlockedOpenMomentary }
	class ListFilterDoorOpen : ListFilterDropDown<DoorOpenFilter>
	{
		protected override bool FilterApplies(Thing thing)
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
		public ListFilterThingDef()
		{
			sel = ThingDefOf.WoodLog;
		}

		protected override bool FilterApplies(Thing thing) =>
			sel == thing.def;

		public override IEnumerable<ThingDef> Options() =>
			(ContentsUtility.OnlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.def) :
				base.Options())
			.Where(def => FindDescription.ValidDef(def));

		public override bool Ordered => true;
	}

	class ListFilterModded : ListFilterDropDown<ModContentPack>
	{
		public ListFilterModded()
		{
			sel = LoadedModManager.RunningMods.First(mod => mod.IsCoreMod);
		}


		public override bool UsesRefName => true;
		protected override string MakeRefName() => sel.ToString();

		protected override ModContentPack ResolveReference(Map map) =>
			LoadedModManager.RunningMods.FirstOrDefault(mod => mod.PackageIdPlayerFacing == refName);


		protected override bool FilterApplies(Thing thing) =>
			sel == thing.ContentSource;

		public override IEnumerable<ModContentPack> Options() =>
			LoadedModManager.RunningMods.Where(mod => mod.AllDefs.Any(d => d is ThingDef));

		public override string NameFor(ModContentPack o) => o.Name;
	}

	class ListFilterOnScreen : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.OccupiedRect().Overlaps(Find.CameraDriver.CurrentViewRect);

		public override bool CurrentMapOnly => true;
	}
}
