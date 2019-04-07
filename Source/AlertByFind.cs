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

		private static Alert_Find GetAlert(string name) =>
			AllAlerts.FirstOrDefault(a => a is Alert_Find af && af.GetLabel() == name) as Alert_Find;

		public static void AddAlert(FindAlertData alert, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(alert.desc.name) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?", () =>
					{
						RemoveAlert(alert.desc.name);
						AddAlert(alert, true, okAction);
					}));
			}
			else
			{
				AllAlerts.Add(new Alert_Find(alert));
				okAction?.Invoke();
			}
		}

		public static void RemoveAlert(string name)
		{
			AllAlerts.RemoveAll(a => a is Alert_Find af && af.GetLabel() == name);
			activeAlerts.RemoveAll(a => a is Alert_Find af && af.GetLabel() == name);
		}

		public static void RenameAlert(string name, string newName, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(newName) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?", () =>
					{
						RemoveAlert(newName);
						RenameAlert(name, newName, true, okAction);
					}));
			}
			else
			{
				okAction?.Invoke();
				GetAlert(name)?.Rename(newName);
			}
		}

		public static void SetPriority(string name, AlertPriority p) =>
			GetAlert(name)?.SetPriority(p);

		public static void SetTicks(string name, int t) =>
			GetAlert(name)?.SetTicks(t);

		public static void SetCount(string name, int c) =>
			GetAlert(name)?.SetCount(c);
	}
}
