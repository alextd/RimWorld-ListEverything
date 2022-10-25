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
				FindDescription desc = new FindDescription();
				ListFilter filter = ListFilterMaker.NameFilter(desc);
				desc.filters.Add(filter);
				filter.Focus();
				MainTabWindow_List.OpenWith(desc);
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
					tab.RemakeList();
			}
		}

		//Alerts:
		private Dictionary<string, FindAlertData> savedAlerts = new Dictionary<string, FindAlertData>();

		public IEnumerable<string> AlertNames() => savedAlerts.Keys;


		//NEW THINGS

		public FindAlertData GetAlert(string name) => savedAlerts[name];
		public Map GetMapFor(string name) => savedAlerts[name].map;

		public void AddAlert(string name, FindDescription desc)
		{
			desc.name = name; //Remember for current copy

			Map map = desc.allMaps ? null : Find.CurrentMap;
			
			//Save two FindDescriptions: One to be scribed with ref string, other put in alert with real refs
			//This was a good idea at one point but now I don't care to consolidate them into one ist
			FindDescription refDesc = desc.Clone(null); //This one has ref string
			refDesc.name = name;
			FindDescription alertDesc = refDesc.Clone(map); //This one re-resolves reference for this map.

			AlertByFind.AddAlert(new FindAlertData(map, alertDesc), okAction: () => savedAlerts[name] = new FindAlertData(map, refDesc));
		}

		public FindDescription LoadAlert(string name)
		{
			return savedAlerts[name].desc.Clone(Find.CurrentMap);
		}

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
			savedAlerts[name].desc.alertPriority = p;
		}

		public void SetTicks(string name, int t)
		{
			AlertByFind.SetTicks(name, t);
			savedAlerts[name].desc.ticksToShowAlert = t;
		}

		public void SetCount(string name, int c)
		{
			AlertByFind.SetCount(name, c);
			savedAlerts[name].desc.countToAlert = c;
		}

		public void SetComp(string name, CompareType c)
		{
			AlertByFind.SetComp(name, c);
			savedAlerts[name].desc.countComp = c;
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedAlerts, "alertsByFind");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (savedAlerts == null)	
					savedAlerts = new Dictionary<string, FindAlertData>();
				foreach (var kvp in savedAlerts)
				{
					AlertByFind.AddAlert(new FindAlertData(kvp.Value.map, kvp.Value.desc.Clone(kvp.Value.map)), overwrite: true);//Shouldn't need to overwrite, shouldn't popup window during ExposeData anyway
				}
			}
		}
	}
}
