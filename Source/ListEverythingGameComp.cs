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
			//Save two FindDescriptions: One to be scribed with ref string, other put in alert with real refs
			FindDescription refDesc = desc.Clone();	//This one has string
			FindDescription alertDesc = refDesc.Clone(); //This one re-resolves reference.
			alertsByFind[name] = refDesc;
			AlertByFind.AllAlerts.Add(new Alert_Find(name, alertDesc));
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


		public override void ExposeData()
		{
			Scribe_Collections.Look(ref alertsByFind, "alertsByFind");
			if(Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (alertsByFind == null)
					alertsByFind = new Dictionary<string, FindDescription>();
				foreach (var kvp in alertsByFind)
				{
					AlertByFind.AllAlerts.Add(new Alert_Find(kvp.Key, kvp.Value.Clone()));
				}
			}
		}
	}
}
