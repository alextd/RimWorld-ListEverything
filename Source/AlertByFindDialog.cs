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

		//protected but using publicized assembly
		//protected override void SetInitialSizeAndPosition()
		public override void SetInitialSizeAndPosition()
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
			//Title
			var listing = new Listing_Standard();
			listing.Begin(inRect);
			Text.Font = GameFont.Medium;
			listing.Label("TD.CustomAlerts".Translate());
			Text.Font = GameFont.Small;
			listing.GapLine();
			listing.End();

			//Check off
			Rect enableRect = inRect.RightHalf().TopPartPixels(Text.LineHeight);
			Widgets.CheckboxLabeled(enableRect, "TD.EnableAlerts".Translate(), ref Alert_Find.enableAll);

			//Margin
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

				row.Label(alert.desc.name + alert.desc.mapLabel, rowRect.width / 4);

				if(row.ButtonText("Rename".Translate()))
					Find.WindowStack.Add(new Dialog_Name(newName => comp.RenameAlert(name, newName)));

				if (row.ButtonText("Load".Translate()))
					MainTabWindow_List.OpenWith(alert.desc.Clone(alert.desc.map), true);
				
				if (row.ButtonText("Delete".Translate()))
					remove = name;

				bool crit = alert.alertPriority == AlertPriority.Critical;
				row.ToggleableIcon(ref crit, TexButton.PassionMajorIcon, "TD.CriticalAlert".Translate());
				comp.SetPriority(name, crit ? AlertPriority.Critical : AlertPriority.Medium);

				row.Label("TD.SecondsUntilShown".Translate());
				int sec = alert.ticksToShowAlert / 60;
				string secStr = sec.ToString();
				Rect textRect = row.GetRect(64); textRect.height -= 4; textRect.width -= 4;
				Widgets.TextFieldNumeric(textRect, ref sec, ref secStr, 0, 999999);
				TooltipHandler.TipRegion(textRect, "TD.Tip1000SecondsInARimworldDay".Translate());
				comp.SetTicks(name, sec * 60);

				row.Label("TD.ShowWhen".Translate());
				if (row.ButtonIcon(TexFor(alert.countComp)))
					comp.SetComp(name, (CompareType)((int)(alert.countComp + 1) % 3));

				int count = alert.countToAlert;
				string countStr = count.ToString();
				textRect = row.GetRect(64); textRect.height -= 4; textRect.width -= 4;
				Widgets.TextFieldNumeric(textRect, ref count, ref countStr, 0, 999999);
				comp.SetCount(name, count);
			}
			

			scrollViewHeight = RowHeight * comp.AlertNames().Count();
			Widgets.EndScrollView();

			if (remove != null)
			{
				if (Event.current.shift)
					comp.RemoveAlert(remove);
				else
					Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
						"TD.Delete0".Translate(remove), () => comp.RemoveAlert(remove)));
			}
		}

		public static Texture2D TexFor(CompareType comp) =>
			comp == CompareType.Equal ? TexButton.Equals :
			comp == CompareType.Greater ? TexButton.GreaterThan :
			TexButton.LessThan;
	}
}
