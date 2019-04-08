using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;
using RimWorld;

namespace List_Everything
{
	class Dialog_ManageSavedLists : Window
	{
		public override Vector2 InitialSize
		{
			get
			{
				return new Vector2(900f, 700f);
			}
		}

		protected override void SetInitialSizeAndPosition()
		{
			base.SetInitialSizeAndPosition();
			windowRect.x = UI.screenWidth - windowRect.width;
			windowRect.y = UI.screenHeight - MainButtonDef.ButtonHeight - this.windowRect.height;
		}

		public Dialog_ManageSavedLists()
		{
			this.forcePause = true;
			this.doCloseX = true;
			this.doCloseButton = true;
			this.closeOnClickedOutside = true;
			this.absorbInputAroundWindow = true;
		}
		public override void DoWindowContents(Rect inRect)
		{
			var listing = new Listing_Standard();
			listing.Begin(inRect);

			Text.Font = GameFont.Medium;
			listing.Label("TD.SavedFindFilters".Translate());
			Text.Font = GameFont.Small;
			listing.GapLine();
			listing.End();

			inRect.yMin += listing.CurHeight;


			LoadedModManager.GetMod<Mod>().DoSettingsWindowContents(inRect);
		}
		public override void PreClose()
		{
			base.PreClose();
			LoadedModManager.GetMod<Mod>().WriteSettings();
		}
	}
}