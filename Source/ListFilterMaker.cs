using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace List_Everything
{
	[DefOf]
	[StaticConstructorOnStartup]
	public static class ListFilterMaker
	{
		public static ListFilterDef Filter_Name;
		public static ListFilterDef Filter_Def;
		public static ListFilterDef Filter_Zone;
		public static ListFilterDef Filter_Group;

		public static ListFilter MakeFilter(ListFilterDef def, IFilterOwner owner)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			filter.owner = owner;
			return filter;
		}

		public static ListFilter NameFilter(FindDescription owner) =>
			ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name, owner);


		public static ListFilter FilterForSelected(FindDescription owner)
		{
			if (Find.Selector.SingleSelectedThing is Thing thing)
			{
				ThingDef def = thing.def;
				if (Find.Selector.SelectedObjectsListForReading.All(o => o is Thing t && t.def == def))
				{
					ListFilterThingDef filterDef = (ListFilterThingDef)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Def, owner);
					filterDef.sel = thing.def;
					return filterDef;
				}
			}
			else if (Find.Selector.SelectedZone is Zone zone)
			{
				ListFilterZone filterZone = (ListFilterZone)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Zone, owner);
				filterZone.sel = zone;
				return filterZone;
			}
			
			var defs = Find.Selector.SelectedObjectsListForReading.Select(o => (o as Thing).def).ToHashSet();

			if(defs.Count > 0)
			{
				ListFilterGroup groupDef = (ListFilterGroup)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Group, owner);
				foreach(ThingDef def in defs)
				{
					ListFilterThingDef filterDef = (ListFilterThingDef)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Def, groupDef);
					filterDef.sel = def;
					groupDef.Add(filterDef);
				}
				return groupDef;
			}

			return null;
		}


		// Categories and Filters that aren't grouped under a Category
		private static readonly List<ListFilterSelectableDef> rootFilters;

		static ListFilterMaker()
		{
			rootFilters = DefDatabase<ListFilterSelectableDef>.AllDefs.ToList();
			foreach (var listDef in DefDatabase<ListFilterCategoryDef>.AllDefs)
				foreach (var subDef in listDef.SubFilters)  // ?? because game explodes on config error
					rootFilters.Remove(subDef);
		}

		public static IEnumerable<ListFilterSelectableDef> SelectableList =>
			rootFilters.Where(d => (DebugSettings.godMode || !d.devOnly));
	}
}
