using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	class ListFilterClassType : ListFilterDropDown<Type>
	{
		public ListFilterClassType() => sel = typeof(Thing);

		protected override bool FilterApplies(Thing thing) =>
			sel.IsAssignableFrom(thing.GetType());

		public static List<Type> types = typeof(Thing).AllSubclassesNonAbstract().OrderBy(t => t.ToString()).ToList();
		public override IEnumerable<Type> Options() =>
			ContentsUtility.OnlyAvailable ?
				ContentsUtility.AvailableInGame(t => t.GetType()).OrderBy(NameFor).ToList() :
				types;
	}

	class ListFilterDrawerType : ListFilterDropDown<DrawerType>
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.def.drawerType == sel;
	}

	class ListFilterFogged : ListFilter
	{
		protected override bool FilterApplies(Thing thing) =>
			thing.PositionHeld.Fogged(thing.MapHeld);
	}
}
