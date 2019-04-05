using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using static System.Reflection.BindingFlags;
using Verse;
using RimWorld;

namespace List_Everything
{
	public static class AlertByFind
	{
		public static FieldInfo AllAlertsInfo = typeof(AlertsReadout).GetField("AllAlerts", NonPublic | Instance);
		public static List<Alert> AllAlerts
		{
			get
			{
				if ((Find.UIRoot as UIRoot_Play).alerts is AlertsReadout readout)
					return (List<Alert>)AllAlertsInfo.GetValue(readout);
				return null;
			}
		}

		public static FieldInfo activeAlertsInfo = typeof(AlertsReadout).GetField("activeAlerts", NonPublic | Instance);
		public static List<Alert> activeAlerts
		{
			get
			{
				if ((Find.UIRoot as UIRoot_Play).alerts is AlertsReadout readout)
					return (List<Alert>)activeAlertsInfo.GetValue(readout);
				return null;
			}
		}
	}

	public class Alert_Find : Alert
	{
		public FindDescription filter;
		public int maxItems = 16;

		public Alert_Find()
		{
			//The vanilla alert added to AllAlerts will be constructed but never trigger
			defaultPriority = AlertPriority.Medium;
		}

		public Alert_Find(string label, FindDescription f) : this()
		{
			defaultLabel = label;
			filter = f;
		}

		public override AlertReport GetReport()
		{
			return filter == null ? AlertReport.Inactive : 
				AlertReport.CulpritsAre(FoundThings());
		}

		public override string GetExplanation()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Your custom alert:");
			stringBuilder.AppendLine("");
			var things = FoundThings();
			foreach (Thing thing in things)
				stringBuilder.AppendLine("   " + thing.Label);
			if(things.Count() == maxItems)
				stringBuilder.AppendLine($"(Maximum {maxItems} displayed)");

			return stringBuilder.ToString().TrimEndNewlines();
		}

		private IEnumerable<Thing> FoundThings()
		{
			int i = 0;
			foreach (Map map in Find.Maps.Where(m => m.IsPlayerHome))
				foreach (Thing t in filter.Get(map))
				{
					yield return t;
					if (++i == maxItems)
						yield break;
				}
		}
	}
}
