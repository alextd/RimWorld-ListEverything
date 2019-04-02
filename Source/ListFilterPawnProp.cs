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
		Hediff,
		Item
	}
	public static class PawnPropEx
	{
		public static string Label(this PawnFilterProp prop)
		{
			switch (prop)
			{
				case PawnFilterProp.Skill: return "Skill";
				case PawnFilterProp.Trait: return "Trait";
				case PawnFilterProp.Hediff: return "Health";
				case PawnFilterProp.Item: return "Inventory";
			}
			return "???";
		}
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

		HediffDef hediffDef;
		//string itemName;

		public override bool Applies(Thing thing)
		{
			Pawn pawn = thing as Pawn;
			if (pawn == null)
			{
				switch (prop)
				{
					case PawnFilterProp.Trait: return traitDef == null;
					case PawnFilterProp.Hediff: return hediffDef == null;
				}
				return false;
			}
			switch (prop)
			{
				case PawnFilterProp.Skill:
					return pawn.skills?.GetSkill(skillDef) is SkillRecord rec && !rec.TotallyDisabled && rec.Level >= skillRange.min && rec.Level <= skillRange.max;
				case PawnFilterProp.Trait:
					return pawn.story?.traits.GetTrait(traitDef) is Trait t && t.Degree == traitDegree;
				case PawnFilterProp.Hediff:
					return hediffDef == null ?
						!pawn.health.hediffSet.hediffs.Any(h => h.Visible || DebugSettings.godMode) :
						pawn.health.hediffSet.HasHediff(hediffDef, !DebugSettings.godMode);
					//case PawnFilterProp.Item: return "Inventory";
			}
			return false;
		}

		public override bool DrawOption(Rect rect)
		{
			WidgetRow row = new WidgetRow(rect.xMin, rect.yMin);
			if (row.ButtonText(prop.Label()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (PawnFilterProp p in Enum.GetValues(typeof(PawnFilterProp)))
				{
					options.Add(new FloatMenuOption(p.Label(), () => prop = p));
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
					Rect rangeRect = rect;
					rangeRect.xMin = row.FinalX;
					IntRange newRange = skillRange;
					Widgets.IntRange(rangeRect, id, ref newRange, SkillRecord.MinLevel, SkillRecord.MaxLevel);
					if (newRange != skillRange)
					{
						skillRange = newRange;
						return true;
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
				case PawnFilterProp.Hediff:
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
					//case PawnFilterProp.Item: return itemName;
			}
			return false;
		}
	}
}