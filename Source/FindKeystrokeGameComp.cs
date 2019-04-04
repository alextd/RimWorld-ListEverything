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
	class ListEverythingGameComp : GameComponent
	{
		public bool continuousRefresh = false;
		public Dictionary<string, FindDescription> alertsByFind = new Dictionary<string, FindDescription>();

		public ListEverythingGameComp(Game g) { }

		public void AddAlert(string name, FindDescription desc)
		{
			alertsByFind[name] = desc;
			AlertByFind.AllAlerts.Add(new Alert_Find(name, desc));
		}
		
		public override void GameComponentOnGUI()
		{
			if (ListDefOf.OpenFindTab.IsDownEvent && Event.current.control)
			{
				Find.MainTabsRoot.SetCurrentTab(ListDefOf.TD_List);
				MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
				tab.Reset();
				ListFilter filter = ListFilterMaker.NameFilter;
				tab.findDesc.filters.Add(filter);
				filter.Focus();
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
