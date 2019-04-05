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

		public Alert_Find()
		{
			//The vanilla alert added to AllAlerts will be constructed but never be active with null filter
			defaultPriority = AlertPriority.Medium;
		}

		public Alert_Find(Map m, FindDescription descNew) : this()
		{
			defaultLabel = descNew.name;
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
