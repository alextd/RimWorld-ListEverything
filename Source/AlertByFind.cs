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

		private static Alert_Find GetAlert(string name, Map map) =>
			AllAlerts.FirstOrDefault(a => a is Alert_Find af && af.GetLabel() == name && af.map == map) as Alert_Find;

		public static void AddAlert(Map map, FindDescription desc, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(desc.name, map) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite Alert?", () =>
					{
						RemoveAlert(desc.name, map);
						AddAlert(map, desc, true, okAction);
					}));
			}
			else
			{
				AllAlerts.Add(new Alert_Find(map, desc));
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
					"Overwrite Alert?", () =>
					{
						RemoveAlert(newName, map);
						RenameAlert(name, map, newName, true, okAction);
					}));
			}
			else
			{
				okAction?.Invoke();
				GetAlert(name, map)?.Rename(newName);
			}
		}

		public static void SetPriority(string name, Map map, AlertPriority p)
		{
			GetAlert(name, map)?.SetPriority(p);
		}
	}
}
