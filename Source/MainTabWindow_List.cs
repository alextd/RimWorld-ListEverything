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
			RemakeList();
		}

		private Vector2 scrollPosition = Vector2.zero;

		private float scrollViewHeight;

		public override void DoWindowContents(Rect fillRect)
		{
			base.DoWindowContents(fillRect);
			Rect filterRect = fillRect.LeftPart(0.60f);
			Rect listRect = fillRect.RightPart(0.39f);

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(listRect.x - 3, 0, listRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}


		List<Thing> listedThings;
		public static void DoFloatMenu(List<FloatMenuOption> options)
		{
			if (options.NullOrEmpty())
				Messages.Message($"There are no options available (Perhaps you should uncheck 'Only available things')", MessageTypeDefOf.RejectInput);
			else
				Find.WindowStack.Add(new FloatMenu(options) { onCloseCallback = RemakeListPlease });
		}
		public static void RemakeListPlease() =>
			Find.WindowStack.WindowOfType<MainTabWindow_List>()?.RemakeList();
		public void RemakeList() =>
			listedThings = findDesc.Get(Find.CurrentMap);

		//Filters:
		public FindDescription findDesc = new FindDescription();

		public static void OpenWith(FindDescription desc)
		{
			MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
			tab.findDesc = desc;
			tab.RemakeList();
			Find.MainTabsRoot.SetCurrentTab(ListDefOf.TD_List);
		}

		//Draw Filters
		public void DoFilter(Rect rect)
		{
			Text.Font = GameFont.Medium;
			Rect headerRect = rect.TopPartPixels(Text.LineHeight);
			Rect filterRect = rect.BottomPartPixels(rect.height - Text.LineHeight);

			//Header
			Rect headerButRect = headerRect.RightPartPixels(Text.LineHeight).ContractedBy(2f);
			Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - Text.LineHeight, headerRect.height);

			if (Widgets.ButtonImage(headerButRect, TexButton.CancelTex))
			{
				findDesc = new FindDescription();
				RemakeList();
			}
			TooltipHandler.TipRegion(headerButRect, "Clear All");

			//Header Title
			Widgets.Label(labelRect, "Listing: "+ findDesc.baseType);
			Widgets.DrawHighlightIfMouseover(labelRect);
			if (Widgets.ButtonInvisible(labelRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in Prefs.DevMode ? Enum.GetValues(typeof(BaseListType)) : BaseListNormalTypes.normalTypes)
					types.Add(new FloatMenuOption(type.ToString(), () => findDesc.baseType = type));
				
				Find.WindowStack.Add(new FloatMenu(types) { onCloseCallback = RemakeList });
			}

			Listing_StandardIndent listing = new Listing_StandardIndent();
			listing.Begin(filterRect);

			//Find Name
			Rect nameRect = listing.GetRect(Text.LineHeight);
			WidgetRow nameRow = new WidgetRow(nameRect.x, nameRect.y);
			nameRow.Label("Name:");
			nameRect.xMin = nameRow.FinalX;
			findDesc.name = Widgets.TextField(nameRect, findDesc.name);

			//Filters
			listing.GapLine();
			if (DoFilters(listing, findDesc.filters))
				RemakeList();

			if (listing.ButtonImage(TexButton.Plus, Text.LineHeight, Text.LineHeight))
				AddFilterFloat(findDesc.filters);

			//Extra options:
			listing.CheckboxLabeled(
				"All maps",
				ref findDesc.allMaps,
				"Certain filters don't work for all maps - like zones and areas that are obviously specific to a single map");

			listing.GapLine();

			//Bottom Buttons
			Rect buttonRect = listing.GetRect(Text.LineHeight);
			buttonRect = buttonRect.LeftPart(0.25f);
			
			if (Widgets.ButtonText(buttonRect, "Make Alert"))
				Find.WindowStack.Add(new Dialog_Name(findDesc.name,
					name => Find.CurrentMap.GetComponent<ListEverythingMapComp>().AddAlert(name, findDesc)));
			
			buttonRect.x += buttonRect.width;
			if (Widgets.ButtonText(buttonRect, "Manage Alerts"))
				Find.WindowStack.Add(new AlertByFindDialog());

			buttonRect.x += buttonRect.width;
			if (Widgets.ButtonText(buttonRect, "Save"))
				Find.WindowStack.Add(new Dialog_Name(findDesc.name, name => Settings.Get().Save(name, findDesc)));

			buttonRect.x += buttonRect.width;
			if (Settings.Get().SavedNames().Count() > 0 &&
				Widgets.ButtonText(buttonRect, "Load"))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (string name in Settings.Get().SavedNames())
					options.Add(new FloatMenuOption(name, () => findDesc = Settings.Get().Load(name)));

				DoFloatMenu(options);
			}

			//Global Options
			listing.CheckboxLabeled(
				"Only show filter options for available things",
				ref ContentsUtility.onlyAvailable,
				"For example, don't show the option 'Made from Plasteel' if nothing is made from plasteel");

			listing.End();
		}

		public static bool DoFilters(Listing_StandardIndent listing, List<ListFilter> filters)
		{
			bool changed = false;
			foreach (ListFilter filter in filters)
			{
				Rect highlightRect = listing.GetRect(0);
				float heightBefore = listing.CurHeight;
				changed |= filter.Listing(listing);
				if (!(filter is ListFilterGroup) && Find.Selector.SelectedObjects.Any(o => o is Thing t && filter.AppliesTo(t)))
				{
					highlightRect.height = listing.CurHeight - heightBefore;
					Widgets.DrawHighlight(highlightRect);
				}
			}

			filters.RemoveAll(f => f.delete);
			return changed;
		}

		public static void AddFilterFloat(List<ListFilter> filters)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in DefDatabase<ListFilterDef>.AllDefs.Where(d => d.parent == null && (Prefs.DevMode || !d.devOnly)))
				options.Add(new FloatMenuOption(def.LabelCap, () => filters.Add(ListFilterMaker.MakeFilter(def))));
			DoFloatMenu(options);
		}


		ThingDef selectAllDef;
		bool selectAll;
		public void DoList(Rect listRect)
		{
			//Top-row buttons
			Rect buttRect = listRect.LeftPartPixels(32);
			buttRect.height = 32;
			listRect.yMin += 34;

			selectAll = Widgets.ButtonImage(buttRect, TexButton.SelectAll);
			TooltipHandler.TipRegion(buttRect, "Select All ( game allows up to 80 )");

			buttRect.x += 34;
			if (Widgets.ButtonImage(buttRect, TexUI.RotRightTex))
				RemakeList();
			TooltipHandler.TipRegion(buttRect, "Refresh (The list is only saved when the filter is changed or the tab is opened)");

			buttRect.x += 34;
			ref bool refresh = ref Current.Game.GetComponent<ListEverythingGameComp>().continuousRefresh;
			if (Widgets.ButtonImage(buttRect, TexUI.ArrowTexRight, refresh ? Color.yellow : Color.white, Color.Lerp(Color.yellow,Color.white, 0.5f)))
				refresh = !refresh;
			GUI.color = Color.white;//Because Widgets.ButtonImage doesn't set it back
			TooltipHandler.TipRegion(buttRect, "Continuous Refresh (about every second)");


			//Handle mouse selection
			if (!Input.GetMouseButton(0))
			{
				dragSelect = false;
				dragDeselect = false;
			}
			if (!Input.GetMouseButton(1))
				dragJump = false;

			selectAllDef = null;

			//Draw Scrolling List:

			//Draw box:
			GUI.color = Color.gray;
			Widgets.DrawBox(listRect);
			GUI.color = Color.white;

			//Nudge in so it's not touching box
			listRect = listRect.ContractedBy(1);
			listRect.width -= 2; listRect.x += 1;

			//Keep full width if nothing to scroll:
			float viewWidth = listRect.width;
			if (scrollViewHeight > listRect.height)
				viewWidth -= 16f;

			//Draw Scrolling list:
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollViewHeight);
			Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
			Rect thingRect = new Rect(viewRect.x, 0, viewRect.width, 32);

			foreach (Thing thing in listedThings)
			{
				//Be smart about drawing only what's shown.
				if (thingRect.y + 32 >= scrollPosition.y)
					DrawThingRow(thing, ref thingRect);

				thingRect.y += 34;

				if (thingRect.y > scrollPosition.y + listRect.height)
					break;
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeight = listedThings.Count() * 34f;

			//Select all 
			if (selectAll)
				foreach (Thing t in listedThings)
					TrySelect.Select(t, false);

			//Select all for double-click
			if (selectAllDef != null)
				foreach(Thing t in listedThings)
					if (t.def == selectAllDef)
						TrySelect.Select(t, false);

			Widgets.EndScrollView();
		}

		bool dragSelect = false;
		bool dragDeselect = false;
		bool dragJump = false;
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
					if (!thing.def.selectable || !thing.Spawned)
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
							TrySelect.Select(thing);
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
							TrySelect.Select(thing);
							dragSelect = true;
						}
					}
				}
				if (Event.current.type == EventType.mouseDrag)
				{
					if (!thing.def.selectable || !thing.Spawned)
						CameraJumper.TryJump(thing);
					else if (dragJump)
						CameraJumper.TryJump(thing);
					else if (dragSelect)
						TrySelect.Select(thing, false);
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
				if (thing.Graphic is Graphic_Cluster)
					Rand.PushState(123456);
				Widgets.ThingIcon(iconRect, thing);
				if (thing.Graphic is Graphic_Cluster)
					Rand.PopState();
			}
		}
	}
}
