using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public enum CompareType { Greater,Equal,Less}
	public class FindAlertData : IExposable
	{
		public Map map;
		public FindDescription desc;

		public FindAlertData() { }

		public FindAlertData(Map m, FindDescription d)
		{
			map = m;
			desc = d;
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref map, "map");
			Scribe_Deep.Look(ref desc, "desc");
		}

		public string Label => desc.name + " (" + (map?.Parent.LabelCap ?? "TD.AllMaps".Translate()) + ")";
	}


	public class Alert_Find : Alert
	{
		public FindAlertData alertData;
		public int maxItems = 16;
		int tickStarted;

		public Alert_Find()
		{
			//The vanilla alert added to AllAlerts will be constructed but never be active with null filter
		}

		public Alert_Find(FindAlertData d) : this()
		{
			defaultLabel = d.desc.name;
			defaultPriority = d.desc.alertPriority;
			alertData = d;
		}

		//copied from Alert_Critical
		private const float PulseFreq = 0.5f;
		private const float PulseAmpCritical = 0.6f;
		private const float PulseAmpTutorial = 0.2f;

		protected override Color BGColor
		{
			get
			{
				if (defaultPriority != AlertPriority.Critical) return base.BGColor;
				float i = Pulser.PulseBrightness(PulseFreq, Pulser.PulseBrightness(PulseFreq, PulseAmpCritical));
				return new Color(i, i, i) * Color.red;
			}
		}

		public void Rename(string name)
		{
			defaultLabel = name;
			alertData.desc.name = name;
		}
		public void SetPriority(AlertPriority p)
		{
			defaultPriority = p;
			alertData.desc.alertPriority = p;
		}
		public void SetTicks(int t) => alertData.desc.ticksToShowAlert = t;
		public void SetCount(int c) => alertData.desc.countToAlert = c;
		public void SetComp(CompareType c) => alertData.desc.countComp = c;
		
		public override AlertReport GetReport()
		{
			if (alertData == null)	//Alert_Find auto-added as an Alert subclass, exists but never displays anything
				return AlertReport.Inactive;

			List<Thing> things = FoundThings().ToList();
			int count = things.Count();
			bool active = false;
			switch(alertData.desc.countComp)
			{
				case CompareType.Greater: active = count > alertData.desc.countToAlert;	break;
				case CompareType.Equal:		active = count == alertData.desc.countToAlert;	break;
				case CompareType.Less:		active = count < alertData.desc.countToAlert;	break;
			}
			if (!active)
				tickStarted = Find.TickManager.TicksGame;
			else if (Find.TickManager.TicksGame - tickStarted >= alertData.desc.ticksToShowAlert)
				return AlertReport.CulpritsAre(things);
			return AlertReport.Inactive;
		}

		public override string GetExplanation()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(defaultLabel + " (" + (alertData.map?.Parent.LabelCap ?? "AllDays".Translate()) + ")");
			stringBuilder.AppendLine("");
			var things = FoundThings();
			foreach (Thing thing in things)
				stringBuilder.AppendLine("   " + thing.Label);
			if (things.Count() == maxItems)
				stringBuilder.AppendLine("TD.Maximum0Displayed".Translate(maxItems));
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("TD.Right-clickToOpenFindTab".Translate());

			return stringBuilder.ToString().TrimEndNewlines();
		}

		int currentTick;
		List<Thing> foundThingsCache;
		private IEnumerable<Thing> FoundThings()
		{
			if (Find.TickManager.TicksGame == currentTick && this.foundThingsCache != null)
				return this.foundThingsCache;

			foundThingsCache = new List<Thing>();
			currentTick = Find.TickManager.TicksGame;

			int i = 0;
			//Single map
			if (alertData.map != null)
				foreach (Thing t in alertData.desc.Get(alertData.map))
				{
					foundThingsCache.Add(t);
					if (++i == maxItems)
						break;
				}
			//All maps
			else
				foreach(Map m in Find.Maps)
					foreach (Thing t in alertData.desc.Get(m))
					{
						foundThingsCache.Add(t);
						if (++i == maxItems)
							break;
					}

			return foundThingsCache;
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
				MainTabWindow_List.OpenWith(alertData.desc.Clone(Find.CurrentMap));

				Event.current.Use();
			}
			return base.DrawAt(topY, minimized);
		}
	}
}
