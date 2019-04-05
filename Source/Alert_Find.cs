using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class Alert_Find : Alert
	{
		public FindDescription desc;
		public int maxItems = 16;
		public Map map;
		int tickStarted;

		public Alert_Find()
		{
			//The vanilla alert added to AllAlerts will be constructed but never be active with null filter
		}

		public Alert_Find(Map m, FindDescription d) : this()
		{
			defaultLabel = d.name;
			defaultPriority = d.alertPriority;
			desc = d;
			map = m;
		}


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
			desc.name = name;
		}
		public void SetPriority(AlertPriority p)
		{
			defaultPriority = p;
			desc.alertPriority = p;
		}
		public void SetTicks(int t) => desc.ticksToShowAlert = t;
		public void SetCount(int c) => desc.countToAlert = c;
		
		public override AlertReport GetReport()
		{
			if (desc == null)
				return AlertReport.Inactive;

			List<Thing> things = FoundThings().ToList();
			if (things.Count() < desc.countToAlert)
				tickStarted = Find.TickManager.TicksGame;
			else if (Find.TickManager.TicksGame - tickStarted >= desc.ticksToShowAlert)
				return AlertReport.CulpritsAre(FoundThings());
			return AlertReport.Inactive;
		}

		public override string GetExplanation()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("Your custom alert:");
			stringBuilder.AppendLine(map.Parent.LabelCap + " (" + defaultLabel + ")");
			stringBuilder.AppendLine("");
			var things = FoundThings();
			foreach (Thing thing in things)
				stringBuilder.AppendLine("   " + thing.Label);
			if (things.Count() == maxItems)
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
