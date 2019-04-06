using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	//Pawn properties is a big one
	public enum PawnFilterProp
	{
		Skill,
		Trait,
		Thought,
		Need,
		Health,
		Incapable
	}
	class ListFilterPawnProp : ListFilter
	{
		PawnFilterProp prop = PawnFilterProp.Skill;

		SkillDef skillDef = SkillDefOf.Animals;
		IntRange skillRange = new IntRange(10, 20);

		TraitDef traitDef = TraitDefOf.Beauty;  //Todo: beauty shows even if it's not on map
		int traitDegree = TraitDefOf.Beauty.degreeDatas.First().degree;
		public static string TraitName(TraitDef def) =>
			def.degreeDatas.Count == 1 ?
				def.degreeDatas.First().label.CapitalizeFirst() :
				def.defName + "*";//TraitDefs don't have labels

		ThoughtDef thoughtDef = ThoughtDefOf.AteWithoutTable;
		public static string ThoughtName(ThoughtDef def)
		{
			string label =
				def.label?.CapitalizeFirst() ??
				def.stages?.FirstOrDefault(d => d?.label != null).label.CapitalizeFirst() ??
				def.stages?.FirstOrDefault(d => d?.labelSocial != null).labelSocial.CapitalizeFirst() ?? "???";

			return def.stages?.Count > 1 ? label + "*" : label;
		}
		int thoughtStage = 0;

		NeedDef needDef = NeedDefOf.Food;
		FloatRange needRange = new FloatRange(0, 0.5f);

		HediffDef hediffDef;
		FloatRange? severityRange = new FloatRange(0, 1);

		WorkTags incapableWork;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref prop, "prop");

			Scribe_Defs.Look(ref skillDef, "skillDef");
			Scribe_Values.Look(ref skillRange, "skillRange");

			Scribe_Defs.Look(ref traitDef, "traitDef");
			Scribe_Values.Look(ref traitDegree, "traitDegree");

			Scribe_Defs.Look(ref thoughtDef, "thoughtDef");
			Scribe_Values.Look(ref thoughtStage, "thoughtDegree");

			Scribe_Defs.Look(ref needDef, "needDef");
			Scribe_Values.Look(ref needRange, "needRange");

			Scribe_Defs.Look(ref hediffDef, "hediffDef");
			Scribe_Values.Look(ref severityRange, "severityRange");

			Scribe_Values.Look(ref incapableWork, "incapableWork");
		}
		public override ListFilter Clone(Map map)
		{
			ListFilterPawnProp clone = (ListFilterPawnProp)base.Clone(map);
			clone.prop = prop;

			clone.skillDef = skillDef;
			clone.skillRange = skillRange;

			clone.traitDef = traitDef;
			clone.traitDegree = traitDegree;

			clone.thoughtDef = thoughtDef;
			clone.thoughtStage = thoughtStage;

			clone.needDef = needDef;
			clone.needRange = needRange;

			clone.hediffDef = hediffDef;
			clone.severityRange = severityRange;

			clone.incapableWork = incapableWork;
			return clone;
		}

		public override bool FilterApplies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null)
			{
				switch (prop)
				{
					case PawnFilterProp.Trait: return traitDef == null;
					case PawnFilterProp.Thought: return thoughtDef == null;
					case PawnFilterProp.Health: return hediffDef == null;
				}
				return false;
			}
			switch (prop)
			{
				case PawnFilterProp.Skill:
					return pawn.skills?.GetSkill(skillDef) is SkillRecord rec &&
						!rec.TotallyDisabled && rec.Level >= skillRange.min && rec.Level <= skillRange.max;

				case PawnFilterProp.Trait:
					return pawn.story?.traits.GetTrait(traitDef) is Trait trait &&
						trait.Degree == traitDegree;

				case PawnFilterProp.Thought:
					if (pawn.needs?.TryGetNeed<Need_Mood>() is Need_Mood mood)
					{
						//memories
						if (mood.thoughts.memories.Memories.Any(t => t.def == thoughtDef && t.CurStageIndex == thoughtStage))
							return true;

						//situational
						List<Thought> thoughts = new List<Thought>();
						mood.thoughts.situational.AppendMoodThoughts(thoughts);
						if (thoughts.Any(t => t.def == thoughtDef && t.CurStageIndex == thoughtStage))
							return true;
					}
					return false;
				case PawnFilterProp.Need:
					return (!pawn.RaceProps.Animal || pawn.Faction != null || DebugSettings.godMode) &&
						pawn.needs?.TryGetNeed(needDef) is Need need && needRange.Includes(need.CurLevelPercentage);

				case PawnFilterProp.Health:
					return hediffDef == null ?
						!pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
						(pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef, !DebugSettings.godMode) is Hediff hediff &&
						(!severityRange.HasValue || severityRange.Value.Includes(hediff.Severity)));

				case PawnFilterProp.Incapable:
					return incapableWork == WorkTags.None ? 
						pawn.story?.CombinedDisabledWorkTags == WorkTags.None:
						pawn.story?.WorkTagIsDisabled(incapableWork) ?? false;
			}
			return false;
		}

		public override bool DrawOption(Rect rect)
		{
			WidgetRow row = new WidgetRow(rect.xMin, rect.yMin);
			if (row.ButtonText(prop.ToString()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (PawnFilterProp p in Enum.GetValues(typeof(PawnFilterProp)))
				{
					options.Add(new FloatMenuOption(p.ToString(), () => prop = p));
				}
				MainTabWindow_List.DoFloatMenu(options); 
			}
			switch (prop)
			{
				case PawnFilterProp.Skill:
					if (row.ButtonText(skillDef.LabelCap))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();
						foreach (SkillDef sDef in DefDatabase<SkillDef>.AllDefs)
						{
							options.Add(new FloatMenuOption(sDef.LabelCap, () => skillDef = sDef));
						}
						MainTabWindow_List.DoFloatMenu(options); 
					}
					{
						Rect rangeRect = rect;
						rangeRect.xMin = row.FinalX;
						IntRange newRange = skillRange;
						Widgets.IntRange(rangeRect, id, ref newRange, SkillRecord.MinLevel, SkillRecord.MaxLevel);
						if (newRange != skillRange)
						{
							skillRange = newRange;
							return true;
						}
					}
					break;
				case PawnFilterProp.Trait:
					if (row.ButtonText(TraitName(traitDef)))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();

						IEnumerable<TraitDef> traitsOnMap = ContentsUtility.onlyAvailable
							? ContentsUtility.AvailableOnMap(t => (t as Pawn)?.story?.traits.allTraits.Select(tr => tr.def) ?? Enumerable.Empty<TraitDef>())
							: DefDatabase<TraitDef>.AllDefs;

						foreach (TraitDef tDef in traitsOnMap.OrderBy(TraitName))
						{
							options.Add(new FloatMenuOption(TraitName(tDef), () =>
							{
								traitDef = tDef;
								traitDegree = tDef.degreeDatas.First().degree;
							}));
						}
						MainTabWindow_List.DoFloatMenu(options); 
					}
					if (traitDef.degreeDatas.Count > 1 &&
						row.ButtonText(traitDef.DataAtDegree(traitDegree).label.CapitalizeFirst()))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();
						foreach (TraitDegreeData deg in traitDef.degreeDatas)
						{
							options.Add(new FloatMenuOption(deg.label.CapitalizeFirst(), () => traitDegree = deg.degree));
						}
						MainTabWindow_List.DoFloatMenu(options); 
					}
					break;
				case PawnFilterProp.Thought:
					if (row.ButtonText(ThoughtName(thoughtDef)))  //ThoughtDef defines its own Label instead of LabelCap, and it sometimes fails
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();

						IEnumerable<ThoughtDef> thoughtsOnMap = ContentsUtility.onlyAvailable ?
							thoughtsOnMap = ContentsUtility.AvailableOnMap(ThoughtsForThing) :
							thoughtsOnMap = DefDatabase<ThoughtDef>.AllDefs;

						foreach (ThoughtDef tDef in thoughtsOnMap.OrderBy(tDef => ThoughtName(tDef)))
						{
							options.Add(new FloatMenuOption(ThoughtName(tDef), () =>
							{
								thoughtDef = tDef;
								thoughtStage = 0;
							}));
						}
						MainTabWindow_List.DoFloatMenu(options); 
					}
					break;
				case PawnFilterProp.Need:
					if (row.ButtonText(needDef.LabelCap))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();

						foreach (NeedDef nDef in DefDatabase<NeedDef>.AllDefs)
						{
							options.Add(new FloatMenuOption(nDef.LabelCap, () => needDef = nDef));
						}
						MainTabWindow_List.DoFloatMenu(options);
					}
					{
						Rect rangeRect = rect;
						rangeRect.xMin = row.FinalX;
						FloatRange newRange = needRange;
						Widgets.FloatRange(rangeRect, id, ref newRange, valueStyle: ToStringStyle.PercentOne);
						if (newRange != needRange)
						{
							needRange = newRange;
							return true;
						}
					}
					break;
				case PawnFilterProp.Health:
					if (row.ButtonText(hediffDef?.LabelCap ?? "None"))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();
						options.Add(new FloatMenuOption("None", () => hediffDef = null));

						IEnumerable<HediffDef> hediffsOnMap = ContentsUtility.onlyAvailable
							? ContentsUtility.AvailableOnMap(t => (t as Pawn)?.health.hediffSet.hediffs.Select(h => h.def) ?? Enumerable.Empty<HediffDef>())
							: DefDatabase<HediffDef>.AllDefs;

						foreach (HediffDef hDef in hediffsOnMap.OrderBy(h => h.label))
							options.Add(new FloatMenuOption(hDef.LabelCap, () =>
							{
								hediffDef = hDef;
								severityRange = SeverityRangeFor(hediffDef);
							}));

						MainTabWindow_List.DoFloatMenu(options);
					}
					if (hediffDef != null && severityRange.HasValue)
					{
						Rect rangeRect = rect;
						rangeRect.xMin = row.FinalX;
						FloatRange newRange = severityRange.Value;
						FloatRange boundRange = SeverityRangeFor(hediffDef).Value;
						Widgets.FloatRange(rangeRect, id, ref newRange, boundRange.min, boundRange.max, valueStyle: ToStringStyle.FloatOne);
						if (newRange != severityRange.Value)
						{
							severityRange = newRange;
							return true;
						}
					}
					break;
				case PawnFilterProp.Incapable:
					if (row.ButtonText(incapableWork.LabelTranslated().CapitalizeFirst()))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();

						foreach (WorkTags tag in Enum.GetValues(typeof(WorkTags)))
							options.Add(new FloatMenuOption(tag.LabelTranslated().CapitalizeFirst(), () => incapableWork = tag));

						MainTabWindow_List.DoFloatMenu(options);
					}
					break;
			}
			return false;
		}

		public override bool DrawMore(Listing_StandardIndent listing)
		{
			if (prop != PawnFilterProp.Thought || thoughtDef.stages.Count <= 1) return false;

			Rect nextRect = listing.GetRect(Text.LineHeight);
			listing.Gap(listing.verticalSpacing);

			WidgetRow row = new WidgetRow(nextRect.xMin, nextRect.yMin);
			if (row.ButtonText(thoughtDef.stages[thoughtStage].label.CapitalizeFirst()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				IEnumerable<int> stages = ContentsUtility.onlyAvailable ?
					ContentsUtility.AvailableOnMap(t => ThoughtStagesForThing(t, thoughtDef)) :
					Enumerable.Range(0, thoughtDef.stages.Count);
				foreach (int i in stages)
				{
					int localI = i;
					options.Add(new FloatMenuOption(thoughtDef.stages[i].label.CapitalizeFirst(), () => thoughtStage = localI));
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

		public static FloatRange? SeverityRangeFor(HediffDef hediffDef)
		{
			float min = hediffDef.minSeverity;
			float max = hediffDef.maxSeverity;
			if (hediffDef.lethalSeverity != -1f)
				max = Math.Min(max, hediffDef.lethalSeverity);

			if (max == float.MaxValue) return null;
			return new FloatRange(min, max);
		}
	}
}