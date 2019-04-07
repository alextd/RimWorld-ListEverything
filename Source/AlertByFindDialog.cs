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

		protected override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = UI.screenWidth - windowRect.width;
			windowRect.y = UI.screenHeight - MainButtonDef.ButtonHeight - this.windowRect.height;
		}

		public AlertByFindDialog()
		{
			this.forcePause = true;
			this.doCloseX = true;
			this.doCloseButton = true;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
		}

		private const float RowHeight = WidgetRow.IconSize + 6;

		private Vector2 scrollPosition = Vector2.zero;
		private float scrollViewHeight;
		public override void DoWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Map map = Find.CurrentMap;
			Text.Font = GameFont.Medium;
			listing.Label($"Custom Alerts:");
			Text.Font = GameFont.Small;
			listing.GapLine();
			listing.End();

			inRect.yMin += listing.CurHeight;

			//Useful things:
			ListEverythingGameComp comp = Current.Game.GetComponent<ListEverythingGameComp>();
			string remove = null;

			//Scrolling!
			Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

			Rect rowRect = viewRect; rowRect.height = RowHeight;
			foreach (string name in comp.AlertNames())
			{
				FindAlertData alert = comp.GetAlert(name);
				WidgetRow row = new WidgetRow(rowRect.x, rowRect.y, UIDirection.RightThenDown, rowRect.width);
				rowRect.y += RowHeight;

				row.Label(alert.Label, rowRect.width / 4);

				if(row.ButtonText("Rename"))
					Find.WindowStack.Add(new Dialog_Name(newName => comp.RenameAlert(name, newName)));

				if (row.ButtonText("Load"))
					MainTabWindow_List.OpenWith(alert.desc.Clone(map));
				
				if (row.ButtonText("Delete"))
					remove = name;

				bool crit = alert.desc.alertPriority == AlertPriority.Critical;
				row.ToggleableIcon(ref crit, TexButton.PassionMajorIcon, "Critical Alert");
				comp.SetPriority(name, crit ? AlertPriority.Critical : AlertPriority.Medium);

				row.Label("Seconds until shown:");
				int sec = alert.desc.ticksToShowAlert / 60;
				string secStr = $"{sec}";
				Rect textRect = row.GetRect(32); textRect.height -= 4; textRect.width -= 4;
				Widgets.TextFieldNumeric(textRect, ref sec, ref secStr, 0, 600);
				comp.SetTicks(name, sec * 60);

				row.Label("# matching required to show alert:");
				int count = alert.desc.countToAlert;
				string countStr = $"{count}";
				textRect = row.GetRect(32); textRect.height -= 4; textRect.width -= 4;
				Widgets.TextFieldNumeric(textRect, ref count, ref countStr, 1, 600);
				comp.SetCount(name, count);
			}
			

			scrollViewHeight = RowHeight * comp.AlertNames().Count();
			Widgets.EndScrollView();

			if (remove != null)
				comp.RemoveAlert(remove);
		}
	}
}
