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
			Rect filterRect = fillRect.LeftPart(0.50f);
			Rect listRect = fillRect.RightPart(0.49f);

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(filterRect.width+2, 0, filterRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}


		//Lists:

		//public ListerThings listerThings;
		bool listByName;
		string listByNameStr = "";
		List<Thing> allThingsByName;
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


		//Filters:

		//bool? filterForbidden;

		public void DoFilter(Rect rect)
		{
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(rect);
			listing.CheckboxLabeled("Thing by name", ref listByName);
			if (listByName)
			{
				string newStr = listing.TextEntry(listByNameStr);
				if (newStr != listByNameStr)
				{
					listByNameStr = newStr;
					allThingsByName = Find.CurrentMap.listerThings.AllThings.FindAll(t => t.Label.ToLower().Contains(listByNameStr.ToLower()));
				}
			}
			else
			{
				listByNameStr = "";
				allThingsByName = null;
			}
			listing.CheckboxLabeled("All Buildings", ref listAllBuildings);
			listing.CheckboxLabeled("Repairable Buildings", ref listRepairable);
			listing.CheckboxLabeled("Things Needing Hauling", ref listHaulable);
			listing.CheckboxLabeled("Stacks Needing Merging", ref listMergable);
			listing.CheckboxLabeled("Filth in Home Area", ref listFilth);
			//listing.GapLine();
			//listing.CheckboxLabeledSelectable("Forbidden", ref listFilth);

			listing.End();
		}
		public void DoList(Rect listRect)
		{
			Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			float totalHeight = 0f;

			if (!Input.GetMouseButton(0))
			{
				dragSelect = false;
				dragDeselect = false;
			}
			if (!Input.GetMouseButton(1))
				dragJump = false;

			selectAllDef = null;

			Map map = Find.CurrentMap;

			//Base lists
			IEnumerable<Thing> allThings = listByNameStr != "" ? allThingsByName.Where(t => t.Spawned) : Enumerable.Empty<Thing>();
			if (listAllBuildings)
				allThings = allThings.Concat(map.listerBuildings.allBuildingsColonist.Cast<Thing>());
			if (listRepairable)
				allThings = allThings.Concat(map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer));
			if (listHaulable)
				allThings = allThings.Concat(map.listerHaulables.ThingsPotentiallyNeedingHauling());
			if (listMergable)
				allThings = allThings.Concat(map.listerMergeables.ThingsPotentiallyNeedingMerging());
			if (listFilth)
				allThings = allThings.Concat(map.listerFilthInHomeArea.FilthInHomeArea);

			//Filters
			if (!DebugSettings.godMode)
				allThings = allThings.Where(t => !t.Fogged());

			//Sort
			var sorted = allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000);

			foreach (Thing thing in sorted)
				DrawThingRow(thing, ref totalHeight, viewRect);

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = totalHeight;

			if(selectAllDef != null)
			{
				foreach(Thing t in sorted)
				{
					if (t.def == selectAllDef)
						Find.Selector.Select(t, false);
				}
			}

			Widgets.EndScrollView();
		}

		bool dragSelect = false;
		bool dragDeselect = false;
		bool dragJump = false;
		ThingDef selectAllDef;
		private void DrawThingRow(Thing thing, ref float rowY, Rect fillRect)
		{
			Rect rect = new Rect(fillRect.x, rowY, fillRect.width, 32);
			rowY += 34;

			//Highlight selected
			if (Find.Selector.IsSelected(thing))
				Widgets.DrawHighlightSelected(rect);

			//Draw
			DrawThing(rect, thing);

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
					if (!thing.def.selectable)
					{
						CameraJumper.TryJump(thing);
						if (Event.current.alt)
							Find.MainTabsRoot.EscapeCurrentTab(false);
					}
					else if (Event.current.clickCount == 2 && Event.current.button == 0)
					{
						selectAllDef = thing.def;
					}
					else if (Event.current.shift)
					{
						if (Find.Selector.IsSelected(thing))
						{
							dragDeselect = true;
							Find.Selector.Deselect(thing);
						}
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
						if (Event.current.button == 1)
						{
							CameraJumper.TryJump(thing);
							dragJump = true;
						}
						else if (Find.Selector.IsSelected(thing))
						{
							CameraJumper.TryJump(thing);
							dragSelect = true;
						}
						else
						{
							Find.Selector.ClearSelection();
							Find.Selector.Select(thing);
							dragSelect = true;
						}
					}
				}
				if (Event.current.type == EventType.mouseDrag)
				{
					if (!thing.def.selectable)
						CameraJumper.TryJump(thing);
					else if (dragJump)
						CameraJumper.TryJump(thing);
					else if (dragSelect)
						Find.Selector.Select(thing, false);
					else if (dragDeselect)
						Find.Selector.Deselect(thing);
				}
			}
		}

		public static void DrawThing(Rect rect, Thing thing)
		{
			//Label
			Widgets.Label(rect, thing.LabelCap);

			ThingDef def = thing.def.entityDefToBuild as ThingDef ?? thing.def;
			Rect iconRect = rect.RightPartPixels(32 * (def.graphicData?.drawSize.x / def.graphicData?.drawSize.y ?? 1f));
			//Icon
			if (thing is Frame frame)
			{
				Widgets.ThingIcon(iconRect, def);
			}
			else if (def.graphic is Graphic_Linked && def.uiIconPath.NullOrEmpty())
			{
				Material iconMat = def.graphic.MatSingle;
				Rect texCoords = new Rect(iconMat.mainTextureOffset, iconMat.mainTextureScale);
				GUI.color = thing.DrawColor;
				Widgets.DrawTextureFitted(iconRect, def.uiIcon, 1f, Vector2.one, texCoords);
				GUI.color = Color.white;
			}
			else
			{
				Widgets.ThingIcon(iconRect, thing);
			}
		}
	}
}
