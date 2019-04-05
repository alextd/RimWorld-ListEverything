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
			Text.Font = GameFont.Medium;
			listing.Label($"Alerts for {map.Parent.LabelCap}:");
			Text.Font = GameFont.Small;
			listing.Gap();
			string remove = null;


			ListEverythingMapComp comp = map.GetComponent<ListEverythingMapComp>();
			foreach (string name in comp.AlertNames())
			{
				FindDescription alert = comp.GetAlert(name);
				if (listing.ButtonTextLabeled(name, "Delete"))
					remove = name;

				if (listing.ButtonTextLabeled("", "Rename"))
					Find.WindowStack.Add(new Dialog_Name(newName => comp.RenameAlert(name, newName)));
				
				bool crit = alert.alertPriority == AlertPriority.Critical;
				listing.CheckboxLabeled("Critical Alert", ref crit);
				comp.SetPriority(name, crit ? AlertPriority.Critical : AlertPriority.Medium);

				int sec = alert.ticksToShowAlert / 60;
				string secStr = $"{sec}";
				listing.TextFieldNumericLabeled("Seconds until shown", ref sec, ref secStr, 0, 600);
				comp.SetTicks(name, sec * 60);
			}

			if (remove != null)
				comp.RemoveAlert(remove);

			listing.End();
		}
	}
}
