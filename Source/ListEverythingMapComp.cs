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
			if(AlertByFind.AllAlerts.Any(a => a is Alert_Find af && af.GetLabel() == name && af.map == map))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?",
					 () =>
					 {
						 AlertByFind.RemoveAlert(name, map);
						 AddAlert(name, desc, true);
					 }));
				return;
			}
			//Save two FindDescriptions: One to be scribed with ref string, other put in alert with real refs
			FindDescription refDesc = desc.Clone(null); //This one has string
			FindDescription alertDesc = refDesc.Clone(map); //This one re-resolves reference for this map.
			savedAlerts[name] = refDesc;
			AlertByFind.AllAlerts.Add(new Alert_Find(map, name, alertDesc));
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
					AlertByFind.AllAlerts.Add(new Alert_Find(map, kvp.Key, kvp.Value.Clone(map)));
				}
			}
		}
	}
}
