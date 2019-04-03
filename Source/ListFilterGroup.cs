using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace List_Everything
{
	[StaticConstructorOnStartup]
	static class TexButton
	{
		public static readonly Texture2D Reveal = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Reveal", true);
		public static readonly Texture2D Collapse = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Collapse", true);
		public static readonly Texture2D Plus = ContentFinder<Texture2D>.Get("UI/Buttons/Plus", true);

	}
	class ListFilterGroup : ListFilter
	{
		List<ListFilter> filters = new List<ListFilter>();
		public override bool FilterApplies(Thing t) => filters.Any(f => f.AppliesTo(t));
		
		public override bool DrawMore(Listing_Standard outerListing)
		{
			//Get rect enough for each filter + Add row
			int neededRows = (1 + filters.Count);//No groups inside groups;
			Rect groupRect = outerListing.GetRect((Text.LineHeight + outerListing.verticalSpacing) * (1 + filters.Count));
			Rect nestedRect = groupRect.LeftPartPixels(Text.LineHeight);
			groupRect.xMin += Text.LineHeight + outerListing.verticalSpacing;
			outerListing.Gap();

			//Draw Indent
			Listing_Standard nestListing = new Listing_Standard();
			nestListing.Begin(nestedRect);
			foreach (var _ in Enumerable.Range(0, neededRows))
				nestListing.Label(":");// DrawTexture(TexButton.Reveal, Text.LineHeight, Text.LineHeight);
			nestListing.End();

			//Draw filters
			Listing_Standard groupListing = new Listing_Standard();
			groupListing.Begin(groupRect);
			bool changed = MainTabWindow_List.DoFilters(groupListing, filters);
			if (groupListing.ButtonImage(TexButton.Plus, Text.LineHeight, Text.LineHeight))
			{
				MainTabWindow_List.AddFilterFloat(filters, ListFilterMaker.Filter_Group);
			}
			groupListing.End();

			return changed;
		}
	}

	public static class ListingEx
	{
		public static void DrawTexture(this Listing_Standard listing, Texture2D tex, float width, float height)
		{
			Rect texRect = listing.GetRect(height);
			texRect.width = width;
			GUI.DrawTexture(texRect, tex);
			listing.Gap(listing.verticalSpacing);
		}
	}
}
