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
	class ListFilterSkill : ListFilterDropDown<SkillDef>
	{
		IntRange skillRange = new IntRange(10, 20);

		public ListFilterSkill()
		{
			sel = SkillDefOf.Animals;
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(SkillDef def) => def.LabelCap;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref skillRange, "skillRange");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterSkill clone = (ListFilterSkill)base.Clone(map);
			clone.skillRange = skillRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
				pawn.skills?.GetSkill(sel) is SkillRecord rec &&
				!rec.TotallyDisabled &&
				rec.Level >= skillRange.min && rec.Level <= skillRange.max;

		public override IEnumerable Options() => DefDatabase<SkillDef>.AllDefs;
		public override bool DrawSpecial(Rect rect, WidgetRow row)
		{
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
		int traitDegree = TraitDefOf.Beauty.degreeDatas.First().degree;
		public ListFilterTrait()
		{
			sel = TraitDefOf.Beauty;  //Todo: beauty shows even if it's not on map
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(TraitDef def) => TraitName(def);
		public static string TraitName(TraitDef def) =>
			def.degreeDatas.Count == 1
				? def.degreeDatas.First().label.CapitalizeFirst()
				: def.defName + "*";//TraitDefs don't have labels

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref traitDegree, "traitDegree");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterTrait clone = (ListFilterTrait)base.Clone(map);
			clone.traitDegree = traitDegree;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn ?
				pawn.story?.traits.GetTrait(sel) is Trait trait &&
				trait.Degree == traitDegree :
				sel == null;

		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(t => (t as Pawn)?.story?.traits.allTraits.Select(tr => tr.def) ?? Enumerable.Empty<TraitDef>()).OrderBy(TraitName)
				: DefDatabase<TraitDef>.AllDefs.OrderBy(NameFor);
		protected override void Callback(TraitDef o)
		{
			sel = o;
			traitDegree = sel.degreeDatas.First().degree;
		}

		public override bool DrawSpecial(Rect rect, WidgetRow row)
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
		int thoughtStage = 0;
		public ListFilterThought()
		{
			sel = ThoughtDefOf.AteWithoutTable;  //Todo: beauty shows even if it's not on map
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(ThoughtDef def) => ThoughtName(def);
		public static string ThoughtName(ThoughtDef def)
		{
			string label =
				def.label?.CapitalizeFirst() ??
				def.stages?.FirstOrDefault(d => d?.label != null).label.CapitalizeFirst() ??
				def.stages?.FirstOrDefault(d => d?.labelSocial != null).labelSocial.CapitalizeFirst() ?? "???";

			return def.stages?.Count > 1 ? label + "*" : label;
		}
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref thoughtStage, "thoughtStage");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterThought clone = (ListFilterThought)base.Clone(map);
			clone.thoughtStage = thoughtStage;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			if(thing is Pawn pawn && 
				pawn.needs?.TryGetNeed<Need_Mood>() is Need_Mood mood)
			{
				//memories
				if (mood.thoughts.memories.Memories.Any(t => t.def == sel && t.CurStageIndex == thoughtStage))
					return true;

				//situational
				List<Thought> thoughts = new List<Thought>();
				mood.thoughts.situational.AppendMoodThoughts(thoughts);
				if (thoughts.Any(t => t.def == sel && t.CurStageIndex == thoughtStage))
					return true;
			}
			return false;
		}

		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(ThoughtsForThing).OrderBy(tDef => ThoughtName(tDef))
				: DefDatabase<ThoughtDef>.AllDefs.OrderBy(NameFor);
		protected override void Callback(ThoughtDef o)
		{
			sel = o;
			thoughtStage = 0;
		}
		public override bool DrawSpecial(Rect rect, WidgetRow row) => false;//Too big for one line

		public override bool DrawMore(Listing_StandardIndent listing)
		{
			if (sel.stages.Count <= 1) return false;

			Rect nextRect = listing.GetRect(Text.LineHeight);
			listing.Gap(listing.verticalSpacing);

			WidgetRow row = new WidgetRow(nextRect.x, nextRect.y);
			if (row.ButtonText(sel.stages[thoughtStage].label.CapitalizeFirst()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				IEnumerable<int> stages = ContentsUtility.onlyAvailable ?
					ContentsUtility.AvailableOnMap(t => ThoughtStagesForThing(t, sel)) :
					Enumerable.Range(0, sel.stages.Count);
				foreach (int i in stages)
				{
					int localI = i;
					options.Add(new FloatMenuOption(sel.stages[i].label.CapitalizeFirst(), () => thoughtStage = localI));
				}
				MainTabWindow_List.DoFloatMenu(options);
			}
			return false;
		}

		public static IEnumerable<ThoughtDef> ThoughtsForThing(Thing t)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<ThoughtDef> memories = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Select(th => th.def);
			if (memories != null)
				foreach (ThoughtDef def in memories)
					yield return def;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts)
				yield return thought.def;
		}

		public static IEnumerable<int> ThoughtStagesForThing(Thing t, ThoughtDef def)
		{
			Pawn pawn = t as Pawn;
			if (pawn == null) yield break;

			IEnumerable<int> stages = pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.memories.Memories.Where(th => th.def == def).Select(th => th.CurStageIndex);
			if (stages != null)
				foreach (int stage in stages)
					yield return stage;

			List<Thought> thoughts = new List<Thought>();
			pawn.needs?.TryGetNeed<Need_Mood>()?.thoughts.situational.AppendMoodThoughts(thoughts);
			foreach (Thought thought in thoughts)
				if (thought.def == def)
					yield return thought.CurStageIndex;
		}
	}

	class ListFilterNeed : ListFilterDropDown<NeedDef>
	{
		FloatRange needRange = new FloatRange(0, 0.5f);
		public ListFilterNeed()
		{
			sel = NeedDefOf.Food;  //Todo: beauty shows even if it's not on map
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(NeedDef def) => def.LabelCap;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref needRange, "needRange");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterNeed clone = (ListFilterNeed)base.Clone(map);
			clone.needRange = needRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
			(!pawn.RaceProps.Animal || pawn.Faction != null || DebugSettings.godMode) &&
				pawn.needs?.TryGetNeed(sel) is Need need && needRange.Includes(need.CurLevelPercentage);

		public override IEnumerable Options() => DefDatabase<NeedDef>.AllDefs;

		public override bool DrawSpecial(Rect rect, WidgetRow row)
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
			drawStyle = DropDownDrawStyle.OptionsAndDrawSpecial;
		}
		public override string NameFor(HediffDef def) => def.LabelCap;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref severityRange, "severityRange");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterHealth clone = (ListFilterHealth)base.Clone(map);
			clone.severityRange = severityRange;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			if (thing is Pawn pawn)
			{
				return sel == null ?
				!pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
				(pawn.health.hediffSet.GetFirstHediffOfDef(sel, !DebugSettings.godMode) is Hediff hediff &&
				(!severityRange.HasValue || severityRange.Value.Includes(hediff.Severity)));
			}
			return sel == null;
		}

		public override string NullOption() => "None";
		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(t => (t as Pawn)?.health.hediffSet.hediffs.Select(h => h.def) ?? Enumerable.Empty<HediffDef>()).OrderBy(h => h.label)
				: DefDatabase<HediffDef>.AllDefs.OrderBy(h => h.label);
		protected override void Callback(HediffDef o)
		{
			sel = o;
			severityRange = SeverityRangeFor(sel);
		}

		public override bool DrawSpecial(Rect rect, WidgetRow row)
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

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn && 
			(sel == WorkTags.None
				? pawn.story?.CombinedDisabledWorkTags == WorkTags.None
				: pawn.story?.WorkTagIsDisabled(sel) ?? false);

		public override IEnumerable Options() => Enum.GetValues(typeof(WorkTags));
	}

	enum TemperatureFilter { Cold, Cool, Okay, Warm, Hot }
	class ListFilterTemp : ListFilterDropDown<TemperatureFilter>
	{
		public override bool FilterApplies(Thing thing)
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
				case TemperatureFilter.Cold: return "Cold";
				case TemperatureFilter.Cool: return "A little cold";
				case TemperatureFilter.Okay: return "Comfortable";
				case TemperatureFilter.Warm: return "A little hot";
				case TemperatureFilter.Hot: return "Hot";
			}
			return "???";
		}

		public override IEnumerable Options() => Enum.GetValues(typeof(TemperatureFilter));
	}

	class ListFilterRestricted : ListFilterDropDown<Area>
	{
		public override void ResolveReference(string refName, Map map)
		{
			sel = map.areaManager.GetLabeled(refName);
			if (sel == null)
				Messages.Message($"Tried to load area Filter named ({refName}) but the current map doesn't have any by that name", MessageTypeDefOf.RejectInput);
		}

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn && pawn.playerSettings is Pawn_PlayerSettings set && set.AreaRestriction == sel;

		public override string NullOption() => "Unrestricted";
		public override IEnumerable Options() => Find.CurrentMap.areaManager.AllAreas.Where(a => a.AssignableAsAllowed());
		public override string NameFor(Area o) => o.Label;
	}

	class ListFilterMentalState : ListFilterDropDown<MentalStateDef>
	{
		public override string NameFor(MentalStateDef def) => def.LabelCap;

		public override bool FilterApplies(Thing thing) =>
			thing is Pawn pawn &&
				(sel == null
				? pawn.MentalState == null
				: pawn.MentalState?.def is MentalStateDef def && def == sel);

		public override IEnumerable Options() =>
			ContentsUtility.onlyAvailable
				? ContentsUtility.AvailableOnMap(t => (t as Pawn)?.MentalState?.def).OrderBy(NameFor)
				: DefDatabase<MentalStateDef>.AllDefs.OrderBy(NameFor);
		public override string NullOption() => "None";
	}
	
	class ListFilterPrisoner : ListFilterDropDown<PrisonerInteractionModeDef>
	{
		public ListFilterPrisoner() => sel = PrisonerInteractionModeDefOf.NoInteraction;

		public override bool FilterApplies(Thing thing)
		{
			if (extraOption == 2)
				return thing.GetRoom()?.isPrisonCell ?? false;

			Pawn pawn = thing as Pawn;
			//Default setting for interactionMode is NoInteraction so fail early if not prisoner
			//this also covers extraOption == 1, isPrisoner
			if (!pawn?.IsPrisoner ?? true)
				return false;

			return pawn.guest?.interactionMode == sel;
		}

		public override IEnumerable Options() => DefDatabase<PrisonerInteractionModeDef>.AllDefs;
		public override string NameFor(PrisonerInteractionModeDef o) => o.LabelCap;

		public override int ExtraOptionsCount => 2;
		public override string NameForExtra(int ex) =>
			ex == 1 ? "Is Prisoner" : "In Cell";
	}
}