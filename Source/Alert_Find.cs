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
		public bool currentMapOnly;
		public string mapLabel;

		private FindDescription _desc;
		public FindDescription desc
		{
			get => _desc;
			set
			{
				_desc = value;
				currentMapOnly = desc.Children.Check(f => f.CurrentMapOnly);

				StringBuilder sb = new(" (");

				if (currentMapOnly)
				{
					sb.Append("Current Map");
					map = null;
				}
				else if (map?.Parent.LabelCap is string label)
					sb.Append(label);
				else
					sb.Append("TD.AllMaps".Translate());

				sb.Append(")");

				mapLabel = sb.ToString();
			}
		}


		public FindAlertData() { }

		public FindAlertData(Map m, FindDescription d)
		{
			map = m;
			desc = d;
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref map, "map");
			Scribe_Deep.Look(ref _desc, "desc");

			//couuld re-regenate these.
			Scribe_Values.Look(ref currentMapOnly, "currentMapOnly");
			Scribe_Values.Look(ref mapLabel, "mapLabel");
		}
	}


	public class Alert_Find : Alert
	{
		public FindAlertData alertData;
		public int maxItems = 16;
		int tickStarted;

		public static bool enableAll = true;

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
		
		//protected but using publicized assembly
		//protected override Color BGColor
		public override Color BGColor
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
			if (alertData == null || !enableAll)	//Alert_Find auto-added as an Alert subclass, exists but never displays anything
				return AlertReport.Inactive;

			List<Thing> things = FoundThings();
			int count = things.Sum(t => t.stackCount);
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
			{
				if (count == 0)
					return AlertReport.Active;
				return AlertReport.CulpritsAre(things.Take(maxItems).ToList());
			}
			return AlertReport.Inactive;
		}

		public override TaggedString GetExplanation()
		{
			var things = FoundThings();
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(defaultLabel + alertData.mapLabel);
			stringBuilder.AppendLine(" - " + MainTabWindow_List.LabelCountThings(things));
			stringBuilder.AppendLine("");
			foreach (Thing thing in things.Take(maxItems))
				stringBuilder.AppendLine("   " + thing.Label);
			if (things.Count() > maxItems)
				stringBuilder.AppendLine("TD.Maximum0Displayed".Translate(maxItems));
			stringBuilder.AppendLine("");
			stringBuilder.AppendLine("TD.Right-clickToOpenFindTab".Translate());

			return stringBuilder.ToString().TrimEndNewlines();
		}

		int currentTick;
		List<Thing> foundThingsCache;
		private List<Thing> FoundThings()
		{
			if (Find.TickManager.TicksGame == currentTick && this.foundThingsCache != null)
				return this.foundThingsCache;

			foundThingsCache = new List<Thing>();
			currentTick = Find.TickManager.TicksGame;
			
			//Current map only
			if (alertData.currentMapOnly)
				foreach (Thing t in alertData.desc.Get(Find.CurrentMap))
					foundThingsCache.Add(t);

			//Single map
			else if (alertData.map != null)
				foreach (Thing t in alertData.desc.Get(alertData.map))
					foundThingsCache.Add(t);

			//All maps
			else
				foreach(Map m in Find.Maps)
					foreach (Thing t in alertData.desc.Get(m))
						foundThingsCache.Add(t);

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
				MainTabWindow_List.OpenWith(alertData.desc.Clone(Find.CurrentMap), true);

				Event.current.Use();
			}
			return base.DrawAt(topY, minimized);
		}
	}
}
