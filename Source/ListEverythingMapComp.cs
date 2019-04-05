using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	class ListEverythingMapComp : MapComponent
	{
		private Dictionary<string, FindDescription> savedAlerts = new Dictionary<string, FindDescription>();

		public ListEverythingMapComp(Map map) :base(map) { }

		public IEnumerable<string> AlertNames() => savedAlerts.Keys;

		public FindDescription GetAlert(string name) => savedAlerts[name];

		public void AddAlert(string name, FindDescription desc)
		{
			//Save two FindDescriptions: One to be scribed with ref string, other put in alert with real refs
			FindDescription refDesc = desc.Clone(null); //This one has ref string
			refDesc.name = name;
			FindDescription alertDesc = refDesc.Clone(map); //This one re-resolves reference for this map.
			AlertByFind.AddAlert(map, alertDesc, okAction: () => savedAlerts[name] = refDesc);
		}

		public void RenameAlert(string name, string newName)
		{
			AlertByFind.RenameAlert(name, map, newName, okAction:
				() =>
				{
					FindDescription desc = savedAlerts[name];
					desc.name = newName;
					savedAlerts[newName] = desc;
					savedAlerts.Remove(name);
				});
		}

		public void RemoveAlert(string name)
		{
			AlertByFind.RemoveAlert(name, map);
			savedAlerts.Remove(name);
		}

		public void SetPriority(string name, AlertPriority p)
		{
			AlertByFind.SetPriority(name, map, p);
			savedAlerts[name].alertPriority = p;
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref savedAlerts, "alertsByFind");
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (savedAlerts == null)
					savedAlerts = new Dictionary<string, FindDescription>();
				foreach (var kvp in savedAlerts)
				{
					AlertByFind.AddAlert(map, kvp.Value, overwrite: true);//Shouldn't need to overwrite, shouldn't popup window during ExposeData anyway
				}
			}
		}
	}
}
