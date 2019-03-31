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

		public override void PreOpen()
		{
			base.PreOpen();
			RemakeBaseList();
		}

		private Vector2 scrollPosition = Vector2.zero;

		private float scrollViewHeight;

		private float listHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			base.DoWindowContents(fillRect);
			Rect filterRect = fillRect.LeftPart(0.50f);
			Rect listRect = fillRect.RightPart(0.49f);
			listHeight = listRect.height;

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(listRect.x-3, 0, listRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}


		//Base Lists:
		enum BaseListType
		{
			All,
			Name,
			Buildings,
			Haulables,
			Mergables,
			Filth
		};
		BaseListType baseType = BaseListType.All;

		string listByNameStr = "";
		//TODO: list all by def, group
		//TODO: building of class, by def


		List<Thing> baseList;
		public void RemakeBaseList()
		{
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch(baseType)
			{
				case BaseListType.All:
					allThings = Find.CurrentMap.listerThings.AllThings;
					break;
				case BaseListType.Name:
					allThings = Find.CurrentMap.listerThings.AllThings.Where(t => t.Label.ToLower().Contains(listByNameStr.ToLower()));
					break;
				case BaseListType.Buildings:
					allThings = Find.CurrentMap.listerBuildings.allBuildingsColonist.Cast<Thing>();
					break;
				case BaseListType.Haulables:
					allThings = Find.CurrentMap.listerHaulables.ThingsPotentiallyNeedingHauling();
					break;
				case BaseListType.Mergables:
					allThings = Find.CurrentMap.listerMergeables.ThingsPotentiallyNeedingMerging();
					break;
				case BaseListType.Filth:
					allThings = Find.CurrentMap.listerFilthInHomeArea.FilthInHomeArea;
					break;
			}

			//Sort
			baseList = allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}

		public string BaseTypeDesc()
		{
			switch (baseType)
			{
				case BaseListType.Name:
					return $"Name: \"{listByNameStr}\"";
				case BaseListType.Haulables:
					return "Things to be hauled";
				case BaseListType.Mergables:
					return "Stacks to be merged";
				case BaseListType.Filth:
					return "Filth in home area";
			}
			return baseType.ToString();
		}

		//Filters:

		//bool? filterForbidden;

		bool showBase;
		bool focusOptions;
		[StaticConstructorOnStartup]
		static class TexButtonNotInternalForReal
		{
			public static readonly Texture2D Reveal = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Reveal", true);
			public static readonly Texture2D Collapse = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Collapse", true);
		}
		public void DoFilter(Rect rect)
		{
			Text.Font = GameFont.Medium;
			Rect headerRect = rect.TopPartPixels(Text.LineHeight);
			Rect filterRect = rect.BottomPartPixels(rect.height - Text.LineHeight);

			//Header
			Rect switchRect = headerRect.LeftPartPixels(Text.LineHeight);
			Rect labelRect = headerRect.RightPartPixels(headerRect.width - Text.LineHeight);

			Texture2D tex = showBase ? TexButtonNotInternalForReal.Collapse : TexButtonNotInternalForReal.Reveal;
			if (Widgets.ButtonImage(switchRect, tex))
				showBase = !showBase;

			Widgets.Label(labelRect, $"Category: {BaseTypeDesc()}");
			Widgets.DrawHighlightIfMouseover(labelRect);
			if (Widgets.ButtonInvisible(labelRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in Enum.GetValues(typeof(BaseListType)))
				{
					types.Add(new FloatMenuOption(type.ToString(), () => baseType = type));
				}

				FloatMenu floatMenu = new FloatMenu(types)
				{
					onCloseCallback = delegate
					{
						RemakeBaseList();
						showBase = true;
						focusOptions = true;
					}
				};
				floatMenu.vanishIfMouseDistant = true;
				Find.WindowStack.Add(floatMenu);
			}

			Listing_Standard listing = new Listing_Standard();
			listing.Begin(filterRect);

			//List base
			if (showBase)
			{
				switch (baseType)
				{
					case BaseListType.Name:
						GUI.SetNextControlName("BaseListTypeOptions");
						string newStr = listing.TextEntry(listByNameStr);
						if (newStr != listByNameStr)
						{
							listByNameStr = newStr;
							RemakeBaseList();
						}
						break;
				}
			}

			//Filters
			//listing.GapLine();
			//listing.CheckboxLabeledSelectable("Forbidden", ref listFilth);

			listing.End();

			if (focusOptions)
			{
				UI.FocusControl("BaseListTypeOptions", this);
				focusOptions = false;
			}
		}
		public void DoList(Rect listRect)
		{
			//Handle mouse selection
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
			IEnumerable<Thing> listedThings = baseList;

			//Filters
			//if (!DebugSettings.godMode)
			//	listedThings = listedThings.Where(t => !t.Fogged());

			//Draw Scrolling List:
			Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, scrollViewHeight);
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			Rect thingRect = new Rect(viewRect.x, 0, viewRect.width, 32);

			foreach (Thing thing in listedThings)
			{
				//Be smart about drawing only what's shown.
				if (thingRect.y + 32 >= scrollPosition.y)
					DrawThingRow(thing, ref thingRect);

				thingRect.y += 34;

				if (thingRect.y > scrollPosition.y + listHeight)
					break;
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = listedThings.Count() * 34f;

			//Select all for double-click
			if(selectAllDef != null)
			{
				foreach(Thing t in listedThings)
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
		private void DrawThingRow(Thing thing, ref Rect rect)
		{
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
