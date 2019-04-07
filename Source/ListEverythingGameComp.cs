using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	[DefOf]
	public static class ListDefOf
	{
		public static KeyBindingDef OpenFindTab;
		public static MainButtonDef TD_List;
	}

	//GameComponent to handle keypress and contiuous refreshing list
	class ListEverythingGameComp : GameComponent
	{
		public bool continuousRefresh = false;

		public ListEverythingGameComp(Game g):base() { }
		
		public override void GameComponentOnGUI()
		{
			if (ListDefOf.OpenFindTab.IsDownEvent && Event.current.control)
			{
				FindDescription desc = new FindDescription();
				ListFilter filter = ListFilterMaker.NameFilter(desc);
				desc.filters.Add(filter);
				filter.Focus();
				MainTabWindow_List.OpenWith(desc);
			}
		}

		public override void GameComponentTick()
		{
			if (Find.TickManager.TicksGame % 60 != 0) return; //every second I guess?

			if (continuousRefresh)
			{
				MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
				tab.RemakeList();
			}
		}
	}
}
