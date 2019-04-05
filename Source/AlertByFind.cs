using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using static System.Reflection.BindingFlags;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public static class AlertByFind
	{
		private static FieldInfo AllAlertsInfo = typeof(AlertsReadout).GetField("AllAlerts", NonPublic | Instance);
		private static List<Alert> AllAlerts
		{
			get
			{
				if ((Find.UIRoot as UIRoot_Play).alerts is AlertsReadout readout)
					return (List<Alert>)AllAlertsInfo.GetValue(readout);
				return null;
			}
		}

		private static FieldInfo activeAlertsInfo = typeof(AlertsReadout).GetField("activeAlerts", NonPublic | Instance);
		private static List<Alert> activeAlerts
		{
			get
			{
				if ((Find.UIRoot as UIRoot_Play).alerts is AlertsReadout readout)
					return (List<Alert>)activeAlertsInfo.GetValue(readout);
				return null;
			}
		}

		public static Alert_Find GetAlert(string name, Map map) =>
			AllAlerts.FirstOrDefault(a => a is Alert_Find af && af.GetLabel() == name && af.map == map) as Alert_Find;

		public static void AddAlert(string name, Map map, FindDescription desc, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(name, map) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?", () =>
					{
						RemoveAlert(name, map);
						AddAlert(name, map, desc, true, okAction);
					}));
			}
			else
			{
				AlertByFind.AllAlerts.Add(new Alert_Find(map, name, desc));
				okAction?.Invoke();
			}
		}

		public static void RemoveAlert(string name, Map map)
		{
			AllAlerts.RemoveAll(a => a is Alert_Find af && af.GetLabel() == name && af.map == map);
			activeAlerts.RemoveAll(a => a is Alert_Find af && af.GetLabel() == name && af.map == map);
		}

		public static void RenameAlert(string name, Map map, string newName, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(newName, map) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?", () => RenameAlert(name, map, newName, true, okAction)));
			}
			else
			{
				okAction?.Invoke();
				GetAlert(name, map)?.Rename(newName);
			}
		}
	}

	public class Alert_Find : Alert
	{
		public FindDescription desc;
		public int maxItems = 16;
		public Map map;

		public Alert_Find()
		{
			//The vanilla alert added to AllAlerts will be constructed but never be active with null filter
			defaultPriority = AlertPriority.Medium;
		}

		public Alert_Find(Map m, string label, FindDescription descNew) : this()
		{
			defaultLabel = label;
			desc = descNew;
			map = m;
		}

		public void Rename(string label) => defaultLabel = label;

		public override AlertReport GetReport()
		{
			return desc == null ? AlertReport.Inactive : 
				AlertReport.CulpritsAre(FoundThings());
		}

		public override string GetExplanation()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Your custom alert:");
			stringBuilder.AppendLine(map.Parent.LabelCap + " ("+defaultLabel+")");
			stringBuilder.AppendLine("");
			var things = FoundThings();
			foreach (Thing thing in things)
				stringBuilder.AppendLine("   " + thing.Label);
			if(things.Count() == maxItems)
				stringBuilder.AppendLine($"(Maximum {maxItems} displayed)");
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine($"(Right-click to open Find Tab)");

			return stringBuilder.ToString().TrimEndNewlines();
		}

		private IEnumerable<Thing> FoundThings()
		{
			int i = 0;
			foreach (Thing t in desc.Get(map))
			{
				yield return t;
				if (++i == maxItems)
					yield break;
			}
		}

		public override Rect DrawAt(float topY, bool minimized)
		{
			Text.Font = GameFont.Small;
			string label = this.GetLabel();
			float height = Text.CalcHeight(label, Alert.Width - 6); //Alert.TextWidth = 148f
			Rect rect = new Rect((float)UI.screenWidth - Alert.Width, topY, Alert.Width, height);
			//if (this.alertBounce != null)
				//rect.x -= this.alertBounce.CalculateHorizontalOffset();
			if (Event.current.button == 1 && Widgets.ButtonInvisible(rect, false))
			{
				MainTabWindow_List.OpenWith(desc.Clone(map));

				Event.current.Use();
			}
			return base.DrawAt(topY, minimized);
		}
	}
}
