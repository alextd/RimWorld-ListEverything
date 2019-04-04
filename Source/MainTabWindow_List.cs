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


		List<Thing> listedThings;
		public static void RemakeListPlease() =>
			Find.WindowStack.WindowOfType<MainTabWindow_List>()?.RemakeList();
		public void RemakeList() =>
			listedThings = findDesc.Get(Find.CurrentMap);

		//Filters:
		public FindDescription findDesc = new FindDescription();
		public void Reset()
		{
			findDesc = new FindDescription();
			RemakeList();
		}
		public void DoFilter(Rect rect)
		{
			Text.Font = GameFont.Medium;
			Rect headerRect = rect.TopPartPixels(Text.LineHeight);
			Rect filterRect = rect.BottomPartPixels(rect.height - Text.LineHeight);

			//Header
			Rect headerButRect = headerRect.RightPartPixels(Text.LineHeight).ContractedBy(2f);
			Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - Text.LineHeight * 2, headerRect.height);

			if (Widgets.ButtonImage(headerButRect, TexUI.RotRightTex))
				RemakeList();
			TooltipHandler.TipRegion(headerButRect, "Refresh (The list is only saved when the filter is changed or the tab is opened)");

			headerButRect.x -= Text.LineHeight;
			if (Widgets.ButtonImage(headerButRect, ListFilter.CancelTex))
				Reset();
			TooltipHandler.TipRegion(headerButRect, "Clear All");

			//Header Title
			Widgets.Label(labelRect, "Listing: "+ findDesc.baseType);
			Widgets.DrawHighlightIfMouseover(labelRect);
			if (Widgets.ButtonInvisible(labelRect))
			{
				List<FloatMenuOption> types = new List<FloatMenuOption>();
				foreach (BaseListType type in Prefs.DevMode ? Enum.GetValues(typeof(BaseListType)) : BaseListNormalTypes.normalTypes)
					types.Add(new FloatMenuOption(type.ToString(), () => findDesc.baseType = type));

				FloatMenu floatMenu = new FloatMenu(types) { onCloseCallback = RemakeList };
				floatMenu.vanishIfMouseDistant = true;
				Find.WindowStack.Add(floatMenu);
			}

			Listing_Standard listing = new Listing_Standard();
			listing.Begin(filterRect);

			//Filters
			listing.GapLine();
			if (DoFilters(listing, findDesc.filters))
				RemakeList();

			if (listing.ButtonImage(TexButton.Plus, Text.LineHeight, Text.LineHeight))
				AddFilterFloat(findDesc.filters);

			listing.GapLine();

			//Bottom Buttons
			Rect buttonRect = listing.GetRect(Text.LineHeight);
			buttonRect = buttonRect.LeftPart(0.25f);


			if (Widgets.ButtonText(buttonRect, "Make Alert"))
				Find.WindowStack.Add(new Dialog_Name(
					name => Current.Game.GetComponent<ListEverythingGameComp>().AddAlert(name, findDesc)));

			buttonRect.x += buttonRect.width * 2;
			if (Widgets.ButtonText(buttonRect, "Save"))
				Find.WindowStack.Add(new Dialog_Name(name => Save(name)));

			buttonRect.x += buttonRect.width;
			if (Settings.Get().savedFilters.Count > 0 &&
				Widgets.ButtonText(buttonRect, "Load"))
			{
				List<FloatMenuOption> options = new List<FloatMenuOption>();
				foreach (var kvp in Settings.Get().savedFilters)
				{
					options.Add(new FloatMenuOption(kvp.Key, () => findDesc = kvp.Value.Clone()));
				}

				FloatMenu floatMenu = new FloatMenu(options) { onCloseCallback = RemakeListPlease };
				floatMenu.vanishIfMouseDistant = true;
				Find.WindowStack.Add(floatMenu);
			}

			//Global Options
			listing.CheckboxLabeled(
				"Only show filter options for available things",
				ref ContentsUtility.onlyAvailable,
				"For example, don't show the option 'Made from Plasteel' if nothing is made form plasteel");

			listing.CheckboxLabeled(
				"Search every second",
				ref Current.Game.GetComponent<ListEverythingGameComp>().continuousRefresh,
				"The list usually only updates when filters are changed - If the search parameters are not stable, check this on");

			listing.End();
		}

		public static bool DoFilters(Listing_Standard listing, List<ListFilter> filters)
		{
			bool changed = false;
			foreach (ListFilter filter in filters)
				changed |= filter.Listing(listing);

			filters.RemoveAll(f => f.delete);
			return changed;
		}

		public static void AddFilterFloat(List<ListFilter> filters, params ListFilterDef[] exclude)
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (ListFilterDef def in DefDatabase<ListFilterDef>.AllDefs.Where(d => !exclude.Contains(d) && (Prefs.DevMode || !d.devOnly)))
				options.Add(new FloatMenuOption(def.LabelCap, () => filters.Add(ListFilterMaker.MakeFilter(def))));
			FloatMenu floatMenu = new FloatMenu(options) { onCloseCallback = RemakeListPlease };
			floatMenu.vanishIfMouseDistant = true;
			Find.WindowStack.Add(floatMenu);
		}

		public void Save(string name)
		{
			if (Settings.Get().Has(name))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Overwrite saved filter?",
					 () => Settings.Get().Save(name, findDesc)));
			}
			else
				Settings.Get().Save(name, findDesc);
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
						TrySelect.Select(t, false);
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
					Rand.PushState();
				Widgets.ThingIcon(iconRect, thing);
				if (thing.Graphic is Graphic_Cluster)
					Rand.PopState();
			}
		}
	}

	public class Dialog_Name : Dialog_Rename
	{
		Action<string> setNameAction;

		public Dialog_Name(Action<string> act)
		{
			curName = "";
			setNameAction = act;
		}

		protected override void SetName(string name)
		{
			setNameAction(name);
		}
	}
}
