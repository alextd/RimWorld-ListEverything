using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public static class AlertByFind
	{
		// Vanilla game works by statically listing all alerts
		private static List<Alert> AllAlerts =>
			((Find.UIRoot as UIRoot_Play)?.alerts as AlertsReadout)?.AllAlerts;

		// ... and copying them to activeAlerts to be displayed
		// (We only need this to remove from it)
		private static List<Alert> activeAlerts =>
			((Find.UIRoot as UIRoot_Play)?.alerts as AlertsReadout)?.activeAlerts;

		private static Alert_Find GetAlert(string name) =>
			AllAlerts.FirstOrDefault(a => a is Alert_Find af && af.GetLabel() == name) as Alert_Find;

		public static void AddAlert(FindAlertData alert, bool overwrite = false, Action okAction = null)
		{
			if (!overwrite && GetAlert(alert.desc.name) != null)
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"TD.OverwriteAlert".Translate(), () =>
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
					"TD.OverwriteAlert".Translate(), () =>
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

		public static void SetComp(string name, CompareType c) =>
			GetAlert(name)?.SetComp(c);
	}
}
