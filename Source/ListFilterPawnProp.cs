using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	
	class ListFilterSkill : ListFilterDropDown<SkillDef>
	{
		IntRange skillRange = new IntRange(0, 20);
		int passion = 3;

		static string[] passionText = new string[] { "PassionNone", "PassionMinor", "PassionMajor", "TD.AnyOption" };//notranslate
		public static string GetPassionText(int x) => passionText[x].Translate().ToString().Split(' ')[0];

		public ListFilterSkill()
		{
			sel = SkillDefOf.Animals;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref skillRange, "skillRange");
			Scribe_Values.Look(ref passion, "passion");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterSkill clone = (ListFilterSkill)base.Clone(newOwner);
			clone.skillRange = skillRange;
			clone.passion = passion;
			return clone;
		}

		protected override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
				pawn.skills?.GetSkill(sel) is SkillRecord rec &&
				!rec.TotallyDisabled &&
				skillRange.Includes(rec.Level) && (passion == 3 || (int)rec.passion == passion);

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			if (row.ButtonText(GetPassionText(passion)))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>{
					new FloatMenuOption(GetPassionText(0), () => passion = 0),
					new FloatMenuOption(GetPassionText(1), () => passion = 1),
					new FloatMenuOption(GetPassionText(2), () => passion = 2),
					new FloatMenuOption(GetPassionText(3), () => passion = 3),
				};
				MainTabWindow_List.DoFloatMenu(options);
			}
			rect.x += 100;
			rect.width -= 100;
			IntRange newRange = skillRange;
			Widgets.IntRange(rect, id, ref newRange, SkillRecord.MinLevel, SkillRecord.MaxLevel);
			if (newRange != skillRange)
			{
				skillRange = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterTrait : ListFilterDropDown<TraitDef>
	{
		int traitDegree;

		public ListFilterTrait()
		{
			sel = TraitDefOf.Beauty;  //Todo: beauty shows even if it's not on map
		}
		protected override void PostSelected()
		{
			traitDegree = sel.degreeDatas.First().degree;
		}

		public override string NameFor(TraitDef def) =>
			def.degreeDatas.Count == 1
				? def.degreeDatas.First().label.CapitalizeFirst()
				: def.defName + "*";//TraitDefs don't have labels

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref traitDegree, "traitDegree");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterTrait clone = (ListFilterTrait)base.Clone(newOwner);
			clone.traitDegree = traitDegree;
			return clone;
		}

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.story?.traits.GetTrait(sel) is Trait trait &&
				trait.Degree == traitDegree;
		}

		public override IEnumerable<TraitDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.story?.traits.allTraits.Select(tr => tr.def) ?? Enumerable.Empty<TraitDef>())
				: base.Options();

		public override bool Ordered => true;

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			if (sel.degreeDatas.Count > 1 &&
				row.ButtonText(sel.DataAtDegree(traitDegree).label.CapitalizeFirst()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (TraitDegreeData deg in sel.degreeDatas)
				{
					options.Add(new FloatMenuOption(deg.label.CapitalizeFirst(), () => traitDegree = deg.degree));
				}
				MainTabWindow_List.DoFloatMenu(options);
			}
			return false;
		}
	}

	class ListFilterThought: ListFilterDropDown<ThoughtDef>
	{
		IntRange stageRange;	//Indexes of orderedStages
		List<int> orderedStages = new();

		// There is a confusing translation between stage index and ordered index.
		// The xml defines stages inconsistently so we order them to orderedStages
		// The selection is done with the ordered index
		// But of course this has to be translated from and to the actual stage index

		public ListFilterThought()
		{
			sel = ThoughtDefOf.AteWithoutTable;
		}


		// stageI = orderedStages[orderI], so
		// gotta reverse index search to find orderI from stageI
		private int OrderedIndex(int stageI) =>
			orderedStages.IndexOf(stageI);

		private bool Includes(int stageI) =>
			stageRange.Includes(OrderedIndex(stageI));


		// Multistage UI is only shown when when there's >1 stage
		// Some hidden stages are not shown (unless you have godmode on)
		public static bool VisibleStage(ThoughtStage stage) =>
			DebugSettings.godMode || (stage?.visible ?? false);

		public static bool ShowMultistage(ThoughtDef def) =>
			def.stages.Count(VisibleStage) > 1;

		public IEnumerable<int> SelectableStages =>
			orderedStages.Where(i => VisibleStage(sel.stages[i]));


		// How to order the stages: by mood/opinion/xml-order
		public class CompareThoughtStage : IComparer<int>
		{
			ThoughtDef tDef;
			public CompareThoughtStage(ThoughtDef d) => tDef = d;

			//Implementing the Compare method
			public int Compare(int l, int r)
			{
				ThoughtStage stageL = tDef.stages[l];
				ThoughtStage stageR = tDef.stages[r];
				float moodL = stageL?.baseMoodEffect ?? 0;
				float moodR = stageR?.baseMoodEffect ?? 0;

				if (moodL > moodR)
					return 1;
				if (moodL < moodR)
					return -1;

				float offsL = stageL?.baseOpinionOffset ?? 0;
				float offsR = stageR?.baseOpinionOffset ?? 0;
				if (offsL > offsR)
					return 1;
				if (offsL < offsR)
					return -1;

				return l - r;
			}
		}

		private void MakeOrderedStages()
		{
			orderedStages.Clear();
			orderedStages.AddRange(Enumerable.Range(0, sel.stages.Count).OrderBy(i => i, new CompareThoughtStage(sel)));
		}

		protected override void PostSelected()
		{
			//Whether it's multistage, visible, or not, alls doesn't matter, just order them ffs.
			MakeOrderedStages();

			stageRange = new IntRange(0, SelectableStages.Count() - 1);
		}

		public override void ExposeData()
		{
			base.ExposeData();

			if (Scribe.mode == LoadSaveMode.LoadingVars)
				MakeOrderedStages();

			Scribe_Values.Look(ref stageRange, "stageRange");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterThought clone = (ListFilterThought)base.Clone(newOwner);
			clone.stageRange = stageRange;
			return clone;
		}

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			if (pawn.needs?.TryGetNeed<Need_Mood>() is Need_Mood mood)
			{
				//memories
				if (mood.thoughts.memories.Memories.Any(t => t.def == sel && Includes(t.CurStageIndex)))
					return true;

				//situational
				List<Thought> thoughts = new List<Thought>();
				mood.thoughts.situational.AppendMoodThoughts(thoughts);
				if (thoughts.Any(t => t.def == sel && Includes(t.CurStageIndex)))
					return true;
			}
			return false;
		}

		public override string NameFor(ThoughtDef def)
		{
			string label =
				def.label?.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.label != null).label.CapitalizeFirst() ??
				def.stages.FirstOrDefault(d => d?.labelSocial != null).labelSocial.CapitalizeFirst() ?? "???";

			return ShowMultistage(def) ? label + "*" : label;
		}

		public override IEnumerable<ThoughtDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(ThoughtsForThing)
				: base.Options();

		public override bool Ordered => true;

		private string NameForStage(int stageI)
		{
			ThoughtStage stage = sel.stages[stageI];
			if (stage == null || !stage.visible)
				return "TD.Invisible".Translate();

			StringBuilder str = new(stage.label.CapitalizeFirst().Replace("{0}", "_").Replace("{1}", "_"));

			if (stage.baseMoodEffect != 0)
				str.Append($" : ({stage.baseMoodEffect})");

			if (stage.baseOpinionOffset != 0)
				str.Append($" : ({stage.baseOpinionOffset})");

			return str.ToString();
		}

		protected override bool DrawUnder(Listing_StandardIndent listing)
		{
			if (!ShowMultistage(sel)) return false;

			//Buttons apparently are too tall for the line height?
			listing.Gap(listing.verticalSpacing);

			listing.NestedIndent(Listing_Standard.DefaultIndent);
			Rect nextRect = listing.GetRect(Text.LineHeight);
			listing.NestedOutdent();

			WidgetRow row = new WidgetRow(nextRect.x, nextRect.y);
			
			row.Label("TD.From".Translate());
			DoStageDropdown(row, stageRange.min, i => stageRange.min = i);

			row.Label("RangeTo".Translate());
			DoStageDropdown(row, stageRange.max, i => stageRange.max = i);
			
			return false;
		}

		private void DoStageDropdown(WidgetRow row, int setI, Action<int> selectedAction)
		{
			if (row.ButtonText(NameForStage(orderedStages[setI])))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (int stageI in SelectableStages)
				{
					int localI = OrderedIndex(stageI);
					options.Add(new FloatMenuOption(NameForStage(stageI), () => selectedAction(localI)));
				}
				MainTabWindow_List.DoFloatMenu(options);
			}
		}

		public static IEnumerable<ThoughtDef> ThoughtsForThing(Thing t)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<ThoughtDef> memories = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Where(th => VisibleStage(th.CurStage)).Select(th => th.def);
			if (memories != null)
				foreach (ThoughtDef def in memories)
					yield return def;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts.Where(th => VisibleStage(th.CurStage)))
				yield return thought.def;
		}
	}

	class ListFilterNeed : ListFilterDropDown<NeedDef>
	{
		FloatRange needRange = new FloatRange(0, 0.5f);

		public ListFilterNeed()
		{
			sel = NeedDefOf.Food;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref needRange, "needRange");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterNeed clone = (ListFilterNeed)base.Clone(newOwner);
			clone.needRange = needRange;
			return clone;
		}

		protected override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
			(!pawn.RaceProps.Animal || pawn.Faction != null || DebugSettings.godMode) &&
				pawn.needs?.TryGetNeed(sel) is Need need && needRange.Includes(need.CurLevelPercentage);

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			FloatRange newRange = needRange;
			Widgets.FloatRange(rect, id, ref newRange, valueStyle: ToStringStyle.PercentOne);
			if (newRange != needRange)
			{
				needRange = newRange;
				return true;
			}
			return false;
		}
	}

	class ListFilterHealth : ListFilterDropDown<HediffDef>
	{
		FloatRange? severityRange;

		public ListFilterHealth()
		{
			sel = null;
		}
		protected override void PostSelected()
		{
			severityRange = SeverityRangeFor(sel);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref severityRange, "severityRange");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterHealth clone = (ListFilterHealth)base.Clone(newOwner);
			clone.severityRange = severityRange;
			return clone;
		}

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				sel == null ? !pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				(pawn.health.hediffSet.GetFirstHediffOfDef(sel, !DebugSettings.godMode) is Hediff hediff &&
				(!severityRange.HasValue || severityRange.Value.Includes(hediff.Severity)));
		}

		public override string NullOption() => "None".Translate();
		public override IEnumerable<HediffDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.health.hediffSet.hediffs.Select(h => h.def) ?? Enumerable.Empty<HediffDef>())
				: base.Options();

		public override bool Ordered => true;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			if (sel != null && severityRange.HasValue)
			{
				Rect rangeRect = rect;
				rangeRect.xMin = row.FinalX;
				FloatRange newRange = severityRange.Value;
				FloatRange boundRange = SeverityRangeFor(sel).Value;
				Widgets.FloatRange(rangeRect, id, ref newRange, boundRange.min, boundRange.max, valueStyle: ToStringStyle.FloatOne);
				if (newRange != severityRange.Value)
				{
					severityRange = newRange;
					return true;
				}
			}
			return false;
		}

		public static FloatRange? SeverityRangeFor(HediffDef hediffDef)
		{
			if (hediffDef == null) return null;

			float min = hediffDef.minSeverity;
			float max = hediffDef.maxSeverity;
			if (hediffDef.lethalSeverity != -1f)
				max = Math.Min(max, hediffDef.lethalSeverity);

			if (max == float.MaxValue) return null;
			return new FloatRange(min, max);
		}
	}

	class ListFilterIncapable : ListFilterDropDown<WorkTags>
	{
		public override string NameFor(WorkTags tags) =>
			tags.LabelTranslated().CapitalizeFirst();

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return 
				extraOption == 1 ? pawn.CombinedDisabledWorkTags != WorkTags.None :
				sel == WorkTags.None ? pawn.CombinedDisabledWorkTags == WorkTags.None :
				pawn.WorkTagIsDisabled(sel);
		}

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	enum TemperatureFilter { Cold, Cool, Okay, Warm, Hot }
	class ListFilterTemp : ListFilterDropDown<TemperatureFilter>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;
			float temp = pawn.AmbientTemperature;
			FloatRange safeRange = pawn.SafeTemperatureRange();
			FloatRange comfRange = pawn.ComfortableTemperatureRange();
			switch (sel)
			{
				case TemperatureFilter.Cold: return temp < safeRange.min;
				case TemperatureFilter.Cool: return temp >= safeRange.min && temp < comfRange.min;
				case TemperatureFilter.Okay: return comfRange.Includes(temp);
				case TemperatureFilter.Warm: return temp <= safeRange.max && temp > comfRange.max;
				case TemperatureFilter.Hot: return temp > safeRange.max;
			}
			return false;//???
		}
		public override string NameFor(TemperatureFilter o)
		{
			switch (o)
			{
				case TemperatureFilter.Cold: return "TD.Cold".Translate();
				case TemperatureFilter.Cool: return "TD.ALittleCold".Translate();
				case TemperatureFilter.Okay: return "TD.Comfortable".Translate();
				case TemperatureFilter.Warm: return "TD.ALittleHot".Translate();
				case TemperatureFilter.Hot: return "TD.Hot".Translate();
			}
			return "???";
		}
	}

	class ListFilterRestricted : ListFilterDropDown<Area>
	{
		protected override Area ResolveReference(Map map) =>
			map.areaManager.GetLabeled(refName);

		public override bool ValidForAllMaps => extraOption > 0 || sel == null;

		protected override bool FilterApplies(Thing thing)
		{
			Area selectedArea = extraOption == 1 ? thing.MapHeld.areaManager.Home : sel;
			return thing is Pawn pawn && pawn.playerSettings is Pawn_PlayerSettings set && set.AreaRestriction == selectedArea;
		}

		public override string NullOption() => "NoAreaAllowed".Translate();
		public override IEnumerable<Area> Options() => Find.CurrentMap.areaManager.AllAreas.Where(a => a is Area_Allowed);//a.AssignableAsAllowed());
		public override string NameFor(Area o) => o.Label;

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "Home".Translate();
	}

	class ListFilterMentalState : ListFilterDropDown<MentalStateDef>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return
				extraOption == 1 ? pawn.MentalState != null: 
				sel == null ? pawn.MentalState == null : 
				pawn.MentalState?.def is MentalStateDef def && def == sel;
		}

		public override IEnumerable<MentalStateDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.MentalState?.def)
				: base.Options();

		public override bool Ordered => true;

		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	class ListFilterPrisoner : ListFilterDropDown<PrisonerInteractionModeDef>
	{
		public ListFilterPrisoner()
		{
			sel = PrisonerInteractionModeDefOf.NoInteraction;
		}

		protected override bool FilterApplies(Thing thing)
		{
			if (extraOption == 2)
				return thing.GetRoom()?.IsPrisonCell ?? false;

			Pawn pawn = thing as Pawn;
			if (pawn == null)
				return false;

			if (extraOption == 1)
				return pawn.IsPrisoner;

			return pawn.IsPrisoner && pawn.guest?.interactionMode == sel;
		}
		
		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "TD.IsPrisoner".Translate() : "TD.InCell".Translate();
	}

	enum DraftFilter { Drafted, Undrafted, Controllable }
	class ListFilterDrafted : ListFilterDropDown<DraftFilter>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			switch (sel)
			{
				case DraftFilter.Drafted: return pawn.Drafted;
				case DraftFilter.Undrafted: return pawn.drafter != null && !pawn.Drafted;
				case DraftFilter.Controllable: return pawn.drafter != null;
			}
			return false;
		}
	}

	class ListFilterJob : ListFilterDropDown<JobDef>
	{
		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			return pawn.CurJobDef == sel;
		}

		public override string NameFor(JobDef o) =>
			Regex.Replace(o.reportString.Replace(".",""), "Target(A|B|C)", "...");

		public override string NullOption() => "None".Translate();

		public override IEnumerable<JobDef> Options() =>
			ContentsUtility.OnlyAvailable
				? ContentsUtility.AvailableInGame(t => (t as Pawn)?.CurJobDef)
			: base.Options();
		public override bool Ordered => true;
	}

	class ListFilterGuestStatus : ListFilterDropDown<GuestStatus>
	{
		protected override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn && pawn.GuestStatus is GuestStatus status && status == sel;
	}

	enum RacePropsFilter { Predator, Prey, Herd, Pack, Wildness, Petness, Trainability, Intelligence }
	class ListFilterRaceProps : ListFilterDropDown<RacePropsFilter>
	{
		Intelligence intelligence;
		FloatRange wild;
		FloatRange petness;
		TrainabilityDef trainability;

		protected override void PostSelected()
		{
			switch (sel)
			{
				case RacePropsFilter.Intelligence: intelligence = Intelligence.Humanlike; return;
				case RacePropsFilter.Wildness: wild = new FloatRange(0.25f, 0.75f); return;
				case RacePropsFilter.Petness: petness = new FloatRange(0.25f, 0.75f); return;
				case RacePropsFilter.Trainability: trainability = TrainabilityDefOf.Advanced; return;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref intelligence, "intelligence");
			Scribe_Values.Look(ref wild, "wild");
			Scribe_Values.Look(ref petness, "petness");
			Scribe_Defs.Look(ref trainability, "trainability");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterRaceProps clone = (ListFilterRaceProps)base.Clone(newOwner);
			clone.intelligence = intelligence;
			clone.wild = wild;
			clone.petness = petness;
			clone.trainability = trainability;
			return clone;
		}

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			RaceProperties props = pawn.RaceProps;
			if (props == null) return false;

			switch (sel)
			{
				case RacePropsFilter.Intelligence: return props.intelligence == intelligence;
				case RacePropsFilter.Herd: 
					return props.herdAnimal;
				case RacePropsFilter.Pack: 
					return props.packAnimal;
				case RacePropsFilter.Predator: 
					return props.predator;
				case RacePropsFilter.Prey: 
					return props.canBePredatorPrey;
				case RacePropsFilter.Wildness: 
					return wild.Includes(props.wildness);
				case RacePropsFilter.Petness: 
					return petness.Includes(props.petness);
				case RacePropsFilter.Trainability:
					return props.trainability == trainability;
			}
			return false;
		}

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			switch (sel)
			{
				case RacePropsFilter.Intelligence:
					if (row.ButtonText(intelligence.TranslateEnum()))
					{
						foreach (Intelligence intel in Enum.GetValues(typeof(Intelligence)))
						{
							options.Add(new FloatMenuOption(intel.TranslateEnum(), () => intelligence = intel));
						}
						MainTabWindow_List.DoFloatMenu(options);
					}
					break;

				case RacePropsFilter.Wildness:
				case RacePropsFilter.Petness:
					ref FloatRange oldRange = ref wild;
					if (sel == RacePropsFilter.Petness)
						oldRange = ref petness;

					FloatRange newRange = oldRange;
					Widgets.FloatRange(rect, id, ref newRange, valueStyle:ToStringStyle.PercentZero);
					if (newRange != oldRange)
					{
						oldRange = newRange;
						return true;
					}
					break;

				case RacePropsFilter.Trainability:
					if (row.ButtonText(trainability.LabelCap))
					{
						foreach (TrainabilityDef def in DefDatabase<TrainabilityDef>.AllDefsListForReading)
						{
							options.Add(new FloatMenuOption(def.LabelCap, () => trainability = def));
						}
						MainTabWindow_List.DoFloatMenu(options);
					}
					break;
			}
			return false;
		}
	}

	class ListFilterGender : ListFilterDropDown<Gender>
	{
		public ListFilterGender() => sel = Gender.Male;

		protected override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn && pawn.gender == sel;
	}

	class ListFilterDevelopmentalStage : ListFilterDropDown<DevelopmentalStage>
	{
		public ListFilterDevelopmentalStage() => sel = DevelopmentalStage.Adult;

		protected override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn && pawn.DevelopmentalStage == sel;
	}

	// -------------------------
	// Animal Details
	// -------------------------


	abstract class ListFilterProduct : ListFilterDropDown<ThingDef>
	{
		protected IntRange countRange;

		public ListFilterProduct()
		{
			extraOption = 1;
			countRange = new IntRange(0, Max());	//Not PostChosen as this depends on subclass, not selection
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref countRange, "countRange");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterProduct clone = (ListFilterProduct)base.Clone(newOwner);
			clone.countRange = countRange;
			return clone;
		}

		public abstract ThingDef DefFor(Pawn pawn);
		public abstract int CountFor(Pawn pawn);

		protected override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null) return false;

			ThingDef productDef = DefFor(pawn);

			if (extraOption == 0 && sel == null)
				return productDef == null;

			if(extraOption == 1 ? productDef != null : sel == productDef)
				return countRange.Includes(CountFor(pawn));

			return false;
		}

		public abstract int Max();
		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			//TODO: write 'IsNull' method to handle confusing extraOption == 1 but Sel == null
			if (extraOption == 0 && sel == null) return false;

			IntRange newRange = countRange;
			
			Widgets.IntRange(rect, id, ref newRange, 0, Max());
			if (newRange != countRange)
			{
				countRange = newRange;
				return true;
			}
			return false;
		}

		public abstract IEnumerable<ThingDef> AllOptions();
		public override IEnumerable<ThingDef> Options()
		{
			if (ContentsUtility.OnlyAvailable)
			{
				HashSet<ThingDef> ret = new HashSet<ThingDef>();
				foreach (Map map in Find.Maps)
					foreach (Pawn p in map.mapPawns.AllPawns)
						if (DefFor(p) is ThingDef def)
							ret.Add(def);

				return ret;
			}
			return AllOptions();
		}

		public override string NullOption() => "None".Translate();

		public override int ExtraOptionsCount => 1;
		public override string NameForExtra(int ex) => "TD.AnyOption".Translate();
	}

	class ListFilterMeat : ListFilterProduct
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.RaceProps.meatDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.MeatAmount));

		public static List<ThingDef> allMeats = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsMeat).ToList();
		public override IEnumerable<ThingDef> AllOptions() => allMeats;

		public static int mostMeat = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.AdultMeatAmount(d))).Max();
		public override int Max() => mostMeat;
	}

	class ListFilterLeather : ListFilterProduct
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.RaceProps.leatherDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(pawn.GetStatValue(StatDefOf.LeatherAmount));

		public static List<ThingDef> allLeathers = DefDatabase<ThingDef>.AllDefs.Where(d => d.IsLeather).ToList();
		public override IEnumerable<ThingDef> AllOptions() => allLeathers;

		public static int mostLeather = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.AdultLeatherAmount(d))).Max();
		public override int Max() => mostLeather;
	}

	class ListFilterEgg : ListFilterProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn)
		{
			var props = pawn.def.GetCompProperties<CompProperties_EggLayer>();

			if (props == null)
				return null;
			if (props.eggLayFemaleOnly && pawn.gender != Gender.Female)
				return null;

			return props.eggUnfertilizedDef;
		}
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.EggsPerYear(pawn.def));

		public static HashSet<ThingDef> allEggs = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_EggLayer>()?.eggUnfertilizedDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allEggs;

		public static int mostEggs = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.EggsPerYear(d))).Max();
		public override int Max() => mostEggs;
	}


	class ListFilterMilk : ListFilterProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn)
		{
			var props = pawn.def.GetCompProperties<CompProperties_Milkable>();

			if (props == null)
				return null;
			if (props.milkFemaleOnly && pawn.gender != Gender.Female)
				return null;

			return props.milkDef;
		}
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.MilkPerYear(pawn.def));

		public static HashSet<ThingDef> allMilks = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_Milkable>()?.milkDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allMilks;

		public static int mostMilk = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.MilkPerYear(d))).Max();
		public override int Max() => mostMilk;
	}

	class ListFilterWool : ListFilterProduct //Per Year
	{
		public override ThingDef DefFor(Pawn pawn) => pawn.def.GetCompProperties<CompProperties_Shearable>()?.woolDef;
		public override int CountFor(Pawn pawn) => Mathf.RoundToInt(AnimalProductionUtility.WoolPerYear(pawn.def));

		public static HashSet<ThingDef> allWools = DefDatabase<ThingDef>.AllDefs.Select(d => d.GetCompProperties<CompProperties_Shearable>()?.woolDef).Where(d => d != null).ToHashSet();
		public override IEnumerable<ThingDef> AllOptions() => allWools;

		public static int mostWool = DefDatabase<ThingDef>.AllDefs.Select(d => Mathf.RoundToInt(AnimalProductionUtility.WoolPerYear(d))).Max();
		public override int Max() => mostWool;
	}
	
	//Enum values matching existing translation keys
	enum ProgressType { Milkable, Shearable, MilkFullness, WoolGrowth, EggProgress, EggHatch}
	class ListFilterProductProgress : ListFilterDropDown<ProgressType>
	{
		protected FloatRange progressRange = new FloatRange(0, 1);

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref progressRange, "progressRange");
		}
		public override ListFilter Clone(IFilterOwner newOwner)
		{
			ListFilterProductProgress clone = (ListFilterProductProgress)base.Clone(newOwner);
			clone.progressRange = progressRange;
			return clone;
		}

		public float ProgressFor(Thing thing) =>
			(float)
			(sel switch
			{
				ProgressType.EggProgress => thing.TryGetComp<CompEggLayer>()?.eggProgress,
				ProgressType.EggHatch => thing.TryGetComp<CompHatcher>()?.gestateProgress,
				ProgressType.MilkFullness => thing.TryGetComp<CompMilkable>()?.Fullness,
				ProgressType.WoolGrowth => thing.TryGetComp<CompShearable>()?.Fullness,
				ProgressType.Milkable => thing.TryGetComp<CompMilkable>()?.ActiveAndFull ?? false ? 1 : 0,
				ProgressType.Shearable => thing.TryGetComp<CompShearable>()?.ActiveAndFull ?? false ? 1 : 0,
				_ => 0,
			});

		protected override bool FilterApplies(Thing thing)
		{
			if (!thing.def.HasComp(sel switch
			{
				ProgressType.EggProgress => typeof(CompEggLayer),
				ProgressType.EggHatch => typeof(CompHatcher),
				ProgressType.MilkFullness => typeof(CompMilkable),
				ProgressType.WoolGrowth => typeof(CompShearable),
				ProgressType.Milkable => typeof(CompMilkable),
				ProgressType.Shearable => typeof(CompShearable),
				_ => null
			}))
				return false;

			if (sel == ProgressType.EggProgress)
			{
				if (thing.def.GetCompProperties<CompProperties_EggLayer>().eggLayFemaleOnly && (thing as Pawn).gender != Gender.Female)
					return false;
			}
			if (sel == ProgressType.MilkFullness || sel == ProgressType.Milkable)
			{
				if (thing.def.GetCompProperties<CompProperties_Milkable>().milkFemaleOnly && (thing as Pawn).gender != Gender.Female)
					return false;
			}

			float progress = ProgressFor(thing);
			if (sel == ProgressType.Milkable || sel == ProgressType.Shearable)
				return progress == 1;
			else
				return progressRange.Includes(progress);
		}

		public override bool DrawCustom(Rect rect, WidgetRow row)
		{
			if (sel == ProgressType.Milkable || sel == ProgressType.Shearable)
				return false;

			FloatRange newRange = progressRange;

			Widgets.FloatRange(rect, id, ref newRange, valueStyle: ToStringStyle.PercentZero);
			if (newRange != progressRange)
			{
				progressRange = newRange;
				return true;
			}
			return false;
		}
	}
	
}