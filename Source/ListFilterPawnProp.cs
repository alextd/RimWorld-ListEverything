﻿using System;
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
		Need,
		Health,
		Inventory
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
				def.defName;

		NeedDef needDef = NeedDefOf.Food;
		FloatRange needRange = new FloatRange(0, 0.5f);

		HediffDef hediffDef;

		ListFilter nameFilter = ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name);
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref prop, "prop");

			Scribe_Defs.Look(ref skillDef, "skillDef");
			Scribe_Values.Look(ref skillRange, "skillRange");

			Scribe_Defs.Look(ref traitDef, "traitDef");
			Scribe_Values.Look(ref traitDegree, "traitDegree");

			Scribe_Defs.Look(ref needDef, "needDef");
			Scribe_Values.Look(ref needRange, "needRange");

			Scribe_Defs.Look(ref hediffDef, "hediffDef");
			Scribe_Deep.Look(ref nameFilter, "nameFilter");
		}
		public override ListFilter Clone()
		{
			ListFilterPawnProp clone = (ListFilterPawnProp)base.Clone();
			clone.prop = prop;

			clone.skillDef = skillDef;
			clone.skillRange = skillRange;

			clone.traitDef = traitDef;
			clone.traitDegree = traitDegree;

			clone.needDef = needDef;
			clone.needRange = needRange;

			clone.hediffDef = hediffDef;
			clone.nameFilter = nameFilter;
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
					case PawnFilterProp.Health: return hediffDef == null;
				}
				return false;
			}
			switch (prop)
			{
				case PawnFilterProp.Skill:
					return pawn.skills?.GetSkill(skillDef) is SkillRecord rec && !rec.TotallyDisabled && rec.Level >= skillRange.min && rec.Level <= skillRange.max;
				case PawnFilterProp.Trait:
					return pawn.story?.traits.GetTrait(traitDef) is Trait trait && trait.Degree == traitDegree;
				case PawnFilterProp.Need:
					return pawn.needs?.TryGetNeed(needDef) is Need need && needRange.Includes(need.CurLevelPercentage);
				case PawnFilterProp.Health:
					return hediffDef == null ?
						!pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
						pawn.health.hediffSet.HasHediff(hediffDef, !DebugSettings.godMode);
				case PawnFilterProp.Inventory:
					return ThingOwnerUtility.GetAllThingsRecursively(pawn)
						.Any(t => nameFilter.FilterApplies(t));
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
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
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
						Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
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
						Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
					}
					if (traitDef.degreeDatas.Count > 1 &&
						row.ButtonText(traitDef.DataAtDegree(traitDegree).label.CapitalizeFirst()))
					{
						List<FloatMenuOption> options = new List<FloatMenuOption>();
						foreach (TraitDegreeData deg in traitDef.degreeDatas)
						{
							options.Add(new FloatMenuOption(deg.label.CapitalizeFirst(), () => traitDegree = deg.degree));
						}
						Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
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
						Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
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
							options.Add(new FloatMenuOption(hDef.LabelCap, () => hediffDef = hDef));

						Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = MainTabWindow_List.RemakeListPlease });
					}
					break;
				case PawnFilterProp.Inventory:
					Rect subRect = rect;
					subRect.xMin = row.FinalX;
					return nameFilter.DrawOption(subRect);
			}
			return false;
		}
	}
}