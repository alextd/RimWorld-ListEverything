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
	//GameComponent to handle keypress, contiuous refreshing list, and alerts
	class ListEverythingGameComp : GameComponent
	{
		public ListEverythingGameComp(Game g):base() { }
		
		//Ctrl-F handler
		public override void GameComponentOnGUI()
		{
			if (ListDefOf.OpenFindTab.IsDownEvent && Event.current.control)
			{
				FindDescription desc = new FindDescription(Find.CurrentMap);

				ListFilter filter = ListFilterMaker.FilterForSelected();
				bool selectedFilter = filter != null;

				if (!selectedFilter)
					filter = ListFilterMaker.NameFilter();

				desc.Children.Add(filter, focus: true);
				MainTabWindow_List.OpenWith(desc, locked: selectedFilter, remake: selectedFilter);
			}
		}

		//continuousRefresh
		public bool continuousRefresh = false;
		public override void GameComponentTick()
		{
			if (Find.TickManager.TicksGame % 60 != 0) return; //every second I guess?

			if (continuousRefresh)
			{
				MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
				if (tab.IsOpen)
					tab.findDesc.RemakeList();
			}
		}

		//Alerts:
		private Dictionary<string, FindAlertData> savedAlerts = new Dictionary<string, FindAlertData>();

		public IEnumerable<string> AlertNames() => savedAlerts.Keys;


		//NEW THINGS

		public FindAlertData GetAlert(string name) => savedAlerts[name];

		public void AddAlert(string name, FindDescription desc)
		{
			desc.name = name; //Remember for current copy
			
			// Copy from the edit dialog into the actual alert,
			// so editing it doesn't create live changes until saved.
			FindDescription refDesc = desc.Clone(desc.map);
			var newAlert = new FindAlertData(refDesc);

			AlertByFind.AddAlert(newAlert, okAction: () => savedAlerts[name] = newAlert);
		}

		public FindDescription LoadAlert(string name)
		{
			var desc = savedAlerts[name].desc;
			return desc.Clone(desc.map);
		}

		//-----
		// This section doubles up AlertByFind/savedAlerts
		// AleryByFind deals with the actual Alert in-game
		// Alerts have no ExposeData, that has to be done with savedAlerts 
		//----
		public void RenameAlert(string name, string newName)
		{
			FindAlertData findAlert = savedAlerts[name];
			AlertByFind.RenameAlert(name, newName, okAction:
				() =>
				{
					findAlert.desc.name = newName;
					savedAlerts[newName] = findAlert;
					savedAlerts.Remove(name);
				});
		}

		public void RemoveAlert(string name)
		{
			AlertByFind.RemoveAlert(name);
			savedAlerts.Remove(name);
		}

		public void SetPriority(string name, AlertPriority p)
		{
			AlertByFind.SetPriority(name, p);
			savedAlerts[name].alertPriority = p;
		}

		public void SetTicks(string name, int t)
		{
			AlertByFind.SetTicks(name, t);
			savedAlerts[name].ticksToShowAlert = t;
		}

		public void SetCount(string name, int c)
		{
			AlertByFind.SetCount(name, c);
			savedAlerts[name].countToAlert = c;
		}

		public void SetComp(string name, CompareType c)
		{
			AlertByFind.SetComp(name, c);
			savedAlerts[name].countComp = c;
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedAlerts, "alertsByFind");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (savedAlerts == null)	
					savedAlerts = new Dictionary<string, FindAlertData>();

				foreach (FindAlertData alert in savedAlerts.Values)
				{
					//alert's ExposeData set alert.desc.map Map. Still needs to be properly cloned though.
					alert.desc = alert.desc.Clone(alert.desc.map);
					AlertByFind.AddAlert(alert, overwrite: true);//Shouldn't need to overwrite, shouldn't popup window during ExposeData anyway
				}
			}
		}
	}
}
