using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class MainTabWindow_List : MainTabWindow
	{
		private Vector2 scrollPosition = Vector2.zero;

		private float scrollViewHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			base.DoWindowContents(fillRect);
			GUI.BeginGroup(fillRect);
			GUI.color = Color.white;
			
			Rect viewRect = new Rect(0f, 0f, fillRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(fillRect, ref scrollPosition, viewRect);
			float totalHeight = 0f;

			foreach (Thing thing in Find.CurrentMap.listerThings.AllThings)
			{
				if (!thing.Fogged() || DebugSettings.godMode)
				{
					 DrawThingRow(thing, ref totalHeight, viewRect);
				}
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = totalHeight;
			Widgets.EndScrollView();
			GUI.EndGroup();
		}

		private static void DrawThingRow(Thing thing, ref float rowY, Rect fillRect)
		{
			Rect rect = new Rect(fillRect.x, rowY, fillRect.width, 32);
			rowY += 34;
			Rect iconRect = rect.LeftPartPixels(32);
			Rect labelRect = new Rect(rect.x + 34, rect.y, rect.width - 34, rect.height);

			Widgets.ThingIcon(iconRect, thing);
			Widgets.Label(labelRect, thing.LabelCap);
		}
	}
}
