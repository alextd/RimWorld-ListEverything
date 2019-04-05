using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace List_Everything
{
	class AlertByFindDialog : Window
	{
		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(900f, 700f);
			}
		}

		public AlertByFindDialog()
		{
			this.forcePause = true;
			this.doCloseX = true;
			this.doCloseButton = true;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
		}

		public override void DoWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Map map = Find.CurrentMap;
			listing.Label($"Alerts for {map.Parent.LabelCap}:");
			string remove = null;


			ListEverythingMapComp comp = map.GetComponent<ListEverythingMapComp>();
			foreach (string name in comp.savedAlerts.Keys)
			{
				if (listing.ButtonTextLabeled(name, "Delete"))
					remove = name;
				if (listing.ButtonTextLabeled("", "Rename"))
				{
					Find.WindowStack.Add(new Dialog_Name(newName =>
						AlertByFind.RenameAlert(name, map, newName, okAction: () =>
						{
							comp.savedAlerts[newName] = comp.savedAlerts[name];
							comp.savedAlerts.Remove(name);
						})));
				}
			}

			if (remove != null)
			{
				//Remove saved ref
				comp.savedAlerts.Remove(remove);

				//Remove in-game alerts
				AlertByFind.RemoveAlert(remove, map);
			}

			listing.End();
		}
	}
}
