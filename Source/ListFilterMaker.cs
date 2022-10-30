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

		public static ListFilter MakeFilter(ListFilterDef def, IFilterHolder holder)
		{
			ListFilter filter = (ListFilter)Activator.CreateInstance(def.filterClass);
			filter.def = def;
			filter.parent = holder;
			return filter;
		}

		public static ListFilter NameFilter(IFilterHolder holder) =>
			ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Name, holder);


		public static ListFilter FilterForSelected(IFilterHolder holder)
		{
			if (Find.Selector.SingleSelectedThing is Thing thing)
			{
				ThingDef def = thing.def;
				if (Find.Selector.SelectedObjectsListForReading.All(o => o is Thing t && t.def == def))
				{
					ListFilterThingDef filterDef = (ListFilterThingDef)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Def, holder);
					filterDef.sel = thing.def;
					return filterDef;
				}
			}
			else if (Find.Selector.SelectedZone is Zone zone)
			{
				ListFilterZone filterZone = (ListFilterZone)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Zone, holder);
				filterZone.sel = zone;
				return filterZone;
			}
			
			var defs = Find.Selector.SelectedObjectsListForReading.Select(o => (o as Thing).def).ToHashSet();

			if(defs.Count > 0)
			{
				ListFilterGroup groupFilter = (ListFilterGroup)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Group, holder);
				foreach(ThingDef def in defs)
				{
					ListFilterThingDef defFilter = (ListFilterThingDef)ListFilterMaker.MakeFilter(ListFilterMaker.Filter_Def, groupFilter);
					defFilter.sel = def;
					groupFilter.Children.Add(defFilter);
				}
				return groupFilter;
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
