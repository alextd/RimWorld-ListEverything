﻿using System;
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
		private FindDescription _findDesc;
		public FindDescription findDesc => _findDesc;
		private bool locked;
		public void SetFindDesc(FindDescription fd = null, bool l = false)
		{
			_findDesc = fd ?? new FindDescription(Find.CurrentMap);
			locked = l;
		}

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
			if (findDesc == null)
			{
				SetFindDesc();
				findDesc.Children.Add(ListFilterMaker.NameFilter());
				//Don't make the list - everything would match.
			}
			else
				findDesc.RemakeList();	
		}

		public override void DoWindowContents(Rect fillRect)
		{
			Rect filterRect = fillRect.LeftPart(0.60f);
			Rect listRect = fillRect.RightPart(0.39f);

			GUI.color = Color.grey;
			Widgets.DrawLineVertical(listRect.x - 3, 0, listRect.height);
			GUI.color = Color.white;

			DoFilter(filterRect);
			DoList(listRect);
		}

		//Filters:

		public static void OpenWith(FindDescription desc, bool locked = false, bool remake = true)
		{
			MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
			tab.SetFindDesc(desc, locked);
			if(remake)	tab.findDesc.RemakeList();
			Find.MainTabsRoot.SetCurrentTab(ListDefOf.TD_List);
		}

		//Draw Filters
		public void DoFilter(Rect rect)
		{
			bool changed = false;

			Text.Font = GameFont.Medium;
			Rect headerRect = rect.TopPartPixels(Text.LineHeight);
			Rect filterRect = rect.BottomPartPixels(rect.height - Text.LineHeight);

			//Header
			Rect headerButRect = headerRect.RightPartPixels(Text.LineHeight).ContractedBy(2f);
			Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - Text.LineHeight * 2, headerRect.height);

			if (Widgets.ButtonImage(headerButRect, TexButton.CancelTex))
			{
				SetFindDesc();
			}
			TooltipHandler.TipRegion(headerButRect, "ClearAll".Translate().CapitalizeFirst());

			headerButRect.x -= Text.LineHeight;
			if (Widgets.ButtonImage(headerButRect, locked ? TexButton.LockOn : TexButton.LockOff))
				locked = !locked;

			TooltipHandler.TipRegion(headerButRect, "TD.LockEditing".Translate());

			//Header Title
			Widgets.Label(labelRect, "TD.Listing".Translate() + findDesc.BaseType.TranslateEnum());
			Widgets.DrawHighlightIfMouseover(labelRect);
			if (Widgets.ButtonInvisible(labelRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in DebugSettings.godMode ? Enum.GetValues(typeof(BaseListType)) : BaseListNormalTypes.normalTypes)
					types.Add(new FloatMenuOption(type.TranslateEnum(), () => findDesc.BaseType = type));

				Find.WindowStack.Add(new FloatMenu(types));
			}

			Listing_StandardIndent listing = new Listing_StandardIndent()
			{ maxOneColumn = true };
			listing.Begin(filterRect);

			/* maybe don't show name box
			//Filter Name
			Rect nameRect = listing.GetRect(Text.LineHeight);
			WidgetRow nameRow = new WidgetRow(nameRect.x, nameRect.y);
			nameRow.Label("TD.Name".Translate());
			nameRect.xMin = nameRow.FinalX;
			findDesc.name = Widgets.TextField(nameRect, findDesc.name);
			listing.Gap();
			*/
			listing.GapLine();


			//Draw Filters!!!
			Rect listRect = listing.GetRect(500);

			//Lock out input to filters.
			if (locked &&
				Event.current.type != EventType.Repaint &&
				Event.current.type != EventType.Layout &&
				Event.current.type != EventType.Ignore &&
				Event.current.type != EventType.Used &&
				Event.current.type != EventType.ScrollWheel &&
				Mouse.IsOver(listRect))
			{
				Event.current.Use();
			}

			//Draw Filters:
			changed |= findDesc.Children.DrawFilters(listRect, locked);


			//Extra options:
			changed |= listing.CheckboxLabeledChanged(
				"TD.AllMaps".Translate(),
				ref findDesc.allMaps,
				"TD.CertainFiltersDontWorkForAllMaps-LikeZonesAndAreasThatAreObviouslySpecificToASingleMap".Translate());

			listing.GapLine();

			//Manage/Save/Load Buttons
			Rect savedRect = listing.GetRect(Text.LineHeight);
			savedRect = savedRect.LeftPart(0.25f);

			//Saved Filters
			if (Widgets.ButtonText(savedRect, "SaveButton".Translate()))
				Find.WindowStack.Add(new Dialog_Name(findDesc.name, name => Mod.settings.Save(name, findDesc)));

			savedRect.x += savedRect.width;
			if (Mod.settings.SavedNames().Count() > 0 &&
				Widgets.ButtonText(savedRect, "Load".Translate()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (string name in Mod.settings.SavedNames())
					options.Add(new FloatMenuOption(name, () =>
					{
						SetFindDesc(Mod.settings.Load(name), true);
						findDesc.RemakeList();
					}));

				Find.WindowStack.Add(new FloatMenu(options));
			}

			savedRect.x += savedRect.width * 2;
			if (Widgets.ButtonText(savedRect, "TD.ManageSaved".Translate()))
				Find.WindowStack.Add(new Dialog_ManageSavedLists());

			//Alerts
			Rect alertsRect = listing.GetRect(Text.LineHeight);
			alertsRect = alertsRect.LeftPart(0.25f);

			var comp = Current.Game.GetComponent<ListEverythingGameComp>();

			if (Widgets.ButtonText(alertsRect, "TD.MakeAlert".Translate()))
			{
				Find.WindowStack.Add(new Dialog_Name(findDesc.name,
					name => comp.AddAlert(name, findDesc)));
			}

			alertsRect.x += alertsRect.width;
			if (comp.AlertNames().Count() > 0 &&
				Widgets.ButtonText(alertsRect, "TD.LoadAlert".Translate()))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (string name in comp.AlertNames())
					options.Add(new FloatMenuOption(name, () =>
					{
						SetFindDesc(comp.LoadAlert(name), true);
						findDesc.RemakeList();
					}));

				Find.WindowStack.Add(new FloatMenu(options));
			}

			alertsRect.x += alertsRect.width * 2;
			if (Widgets.ButtonText(alertsRect, "TD.ManageAlerts".Translate()))
				Find.WindowStack.Add(new AlertByFindDialog());



			//Global Options
			listing.CheckboxLabeled(
			"TD.OnlyShowFilterOptionsForAvailableThings".Translate(),
			ref ContentsUtility.onlyAvailable,
			"TD.ForExampleDontShowTheOptionMadeFromPlasteelIfNothingIsMadeFromPlasteel".Translate());

			listing.End();

			//Update if needed
			if (changed)
				findDesc.RemakeList();
		}



		public static string LabelCountThings(IEnumerable<Thing> things)
		{
			return "TD.LabelCountThings".Translate(things.Sum(t => t.stackCount));
		}


		private Vector2 scrollPositionList = Vector2.zero;
		private float scrollViewHeightList;

		ThingDef selectAllDef;
		bool selectAll;
		public void DoList(Rect listRect)
		{
			//Top-row buttons
			//Select All
			Rect buttRect = listRect.LeftPartPixels(32);
			buttRect.height = 32;

			selectAll = Widgets.ButtonImage(buttRect, TexButton.SelectAll);
			TooltipHandler.TipRegion(buttRect, "TD.SelectAllGameAllowsUpTo80".Translate());

			//Manual refresh
			buttRect.x += 34;
			if (Widgets.ButtonImage(buttRect, TexUI.RotRightTex))
				findDesc.RemakeList();

			TooltipHandler.TipRegion(buttRect, "TD.RefreshTheListIsOnlySavedWhenTheFilterIsChangedOrTheTabIsOpened".Translate());

			//Continuous refresh
			buttRect.x += 34;
			ref bool refresh = ref Current.Game.GetComponent<ListEverythingGameComp>().continuousRefresh;
			if (Widgets.ButtonImage(buttRect, TexUI.ArrowTexRight, refresh ? Color.green : Color.white, Color.Lerp(Color.green,Color.white, 0.5f)))
				refresh = !refresh;
			GUI.color = Color.white;//Because Widgets.ButtonImage doesn't set it back

			// It's not updating while paused
			if (Find.TickManager.Paused)
			{
				GUI.color = new Color(1, 1, 1, .5f);
				GUI.DrawTexture(buttRect, TexButton.CancelTex);
				GUI.color = Color.white;
			}

			TooltipHandler.TipRegion(buttRect,
				Find.TickManager.Paused 
				? "(Does not refresh when paused)"
				: "TD.ContinuousRefreshAboutEverySecond".Translate());

			//Count text
			Text.Anchor = TextAnchor.UpperRight;
			Widgets.Label(listRect, LabelCountThings(findDesc.ListedThings));
			Text.Anchor = TextAnchor.UpperLeft;
			listRect.yMin += 34;

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
			if (scrollViewHeightList > listRect.height)
				viewWidth -= 16f;

			//Draw Scrolling list:
			Rect viewRect = new Rect(0f, 0f, viewWidth, scrollViewHeightList);
			Widgets.BeginScrollView(listRect, ref scrollPositionList, viewRect);
			Rect thingRect = new Rect(viewRect.x, 0, viewRect.width, 32);

			foreach (Thing thing in findDesc.ListedThings)
			{
				//Be smart about drawing only what's shown.
				if (thingRect.y + 32 >= scrollPositionList.y)
					DrawThingRow(thing, ref thingRect);

				thingRect.y += 34;

				if (thingRect.y > scrollPositionList.y + listRect.height)
					break;
			}

			if (Event.current.type == EventType.Layout)
				scrollViewHeightList = findDesc.ListedThings.Count() * 34f;

			//Select all 
			if (selectAll)
				foreach (Thing t in findDesc.ListedThings)
					TrySelect.Select(t, false);

			//Select all for double-click
			if (selectAllDef != null)
				foreach(Thing t in findDesc.ListedThings)
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
				if (Event.current.type == EventType.MouseDown)
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
				if (Event.current.type == EventType.MouseDrag)
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
