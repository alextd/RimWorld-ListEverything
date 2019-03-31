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
		public override Vector2 RequestedTabSize
		{
			get
			{
				return new Vector2(900, base.RequestedTabSize.y);
			}
		}

		private Vector2 scrollPosition = Vector2.zero;

		private float scrollViewHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			base.DoWindowContents(fillRect);
			Rect filterRect = fillRect.LeftPart(0.6f);
			Rect listRect = fillRect.RightPart(0.39f);

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(filterRect.width, 0, filterRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}

		//public ListerThings listerThings;
		bool listAllThings;
		//TODO: by def, group

		//public ListerBuildings listerBuildings;
		bool listAllBuildings;
		//TODO: of class, by def

		//public ListerBuildingsRepairable listerBuildingsRepairable;
		bool listRepairable;

		//public ListerHaulables listerHaulables;
		bool listHaulable;

		//public ListerMergeables listerMergeables;
		bool listMergable;

		//public ListerFilthInHomeArea listerFilthInHomeArea;
		bool listFilth;

		public void DoFilter(Rect rect)
		{
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(rect);
			listing.CheckboxLabeled("All things", ref listAllThings);
			listing.CheckboxLabeled("All Buildings", ref listAllBuildings);
			listing.CheckboxLabeled("Repairable Buildings", ref listRepairable);
			listing.CheckboxLabeled("Haulable things", ref listHaulable);
			listing.CheckboxLabeled("Mergeable? things", ref listMergable);
			listing.CheckboxLabeled("All Filth", ref listFilth);

			listing.End();
		}
		public void DoList(Rect listRect)
		{
			Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			float totalHeight = 0f;

			if (!Input.GetMouseButton(0)) dragSelect = false;

			Map map = Find.CurrentMap;

			IEnumerable<Thing> list = Enumerable.Empty<Thing>();
			if(listAllThings)
				list = list.Concat(map.listerThings.AllThings);
			else
			{
				if (listAllBuildings)
					list = list.Concat(map.listerBuildings.allBuildingsColonist.Cast<Thing>());
				if (listRepairable)
					list = list.Concat(map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer));
				if (listHaulable)
					list = list.Concat(map.listerHaulables.ThingsPotentiallyNeedingHauling());
				if (listMergable)
					list = list.Concat(map.listerMergeables.ThingsPotentiallyNeedingMerging());
				if (listFilth)
					list = list.Concat(map.listerFilthInHomeArea.FilthInHomeArea);
			}
			foreach (Thing thing in list)
			{
				if (!thing.Fogged() || DebugSettings.godMode)
				{
					 DrawThingRow(thing, ref totalHeight, viewRect);
				}
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = totalHeight;
			Widgets.EndScrollView();
		}

		bool dragSelect = false;
		private void DrawThingRow(Thing thing, ref float rowY, Rect fillRect)
		{
			Rect rect = new Rect(fillRect.x, rowY, fillRect.width, 32);
			rowY += 34;
			
			//Highlight selected
			if (Find.Selector.IsSelected(thing))
				Widgets.DrawHighlightSelected(rect);

			//Draw
			Rect iconRect = rect.RightPartPixels(32 * (thing.def.graphicData?.drawSize.x / thing.def.graphicData?.drawSize.y ?? 1));
			Widgets.ThingIcon(iconRect, thing);
			Widgets.Label(rect, thing.LabelCap);

			//Draw arrow pointing to hovered thing
			if (Mouse.IsOver(rect))
			{
				Vector3 center = UI.UIToMapPosition((float)(UI.screenWidth / 2), (float)(UI.screenHeight / 2));
				bool arrow = (center - thing.DrawPos).MagnitudeHorizontalSquared() >= 121f;//Normal arrow is 9^2, using 11^1 seems good too.
				TargetHighlighter.Highlight(thing, arrow, true, true);
			}

			//Mouse event: select.
			if (Mouse.IsOver(rect))
			{
				if (Event.current.type == EventType.mouseDown)
				{
					if (Event.current.shift)
					{
						if (Find.Selector.IsSelected(thing))
							Find.Selector.Deselect(thing);
						else
						{
							dragSelect = true;
							Find.Selector.Select(thing);
						}
					}
					else if (Event.current.alt)
					{
						Find.MainTabsRoot.EscapeCurrentTab(false);
						CameraJumper.TryJumpAndSelect(thing);
					}
					else
					{
						if (Find.Selector.IsSelected(thing))
							CameraJumper.TryJump(thing);
						if (!Find.Selector.IsSelected(thing) || Find.Selector.NumSelected > 1 && Event.current.button == 1)
						{
							Find.Selector.ClearSelection();
							Find.Selector.Select(thing);
							dragSelect = true;
						}
					}
				}
				if (Event.current.type == EventType.mouseDrag && dragSelect)
					Find.Selector.Select(thing, false);
			}
		}
	}
}
