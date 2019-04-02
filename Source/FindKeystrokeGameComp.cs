using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	[DefOf]
	public static class ListDefOf
	{
		public static KeyBindingDef OpenFindTab;
		public static MainButtonDef TD_List;
	}
	class FindKeystrokeGameComp : GameComponent
	{
		public FindKeystrokeGameComp(Game g) { }
		public override void GameComponentOnGUI()
		{
			if (ListDefOf.OpenFindTab.IsDownEvent && Event.current.control)
			{
				Find.MainTabsRoot.SetCurrentTab(ListDefOf.TD_List);
				MainTabWindow_List tab = ListDefOf.TD_List.TabWindow as MainTabWindow_List;
				tab.Reset();
			}
		}
	}
}
