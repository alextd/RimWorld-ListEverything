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
		public Dictionary<string, FindDescription> savedAlerts = new Dictionary<string, FindDescription>();

		public ListEverythingMapComp(Map map) :base(map) { }

		public void AddAlert(string name, FindDescription desc, bool overwrite = false)
		{
			//Save two FindDescriptions: One to be scribed with ref string, other put in alert with real refs
			FindDescription refDesc = desc.Clone(null); //This one has string
			FindDescription alertDesc = refDesc.Clone(map); //This one re-resolves reference for this map.
			AlertByFind.AddAlert(name, map, alertDesc, okAction: () => savedAlerts[name] = refDesc);
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
					AlertByFind.AddAlert(kvp.Key, map, kvp.Value, true);//Shouldn't need to overwrite, shouldn't popup window during ExposeData anyway
				}
			}
		}
	}
}
