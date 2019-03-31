using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	abstract class ListFilter
	{
		public abstract IEnumerable<Thing> Apply(IEnumerable<Thing> list);

		public abstract bool Listing(Listing_Standard listing);
	}

	class ListFilterName : ListFilter
	{
		string name = "";
		public override IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			if (name.NullOrEmpty())
				return list;
			return list.Where(t => t.Label.ToLower().Contains(name.ToLower()));
		}

		public override bool Listing(Listing_Standard listing)
		{
			string newStr = listing.TextEntry(name);
			if (newStr != name)
			{
				name = newStr;
				return true;
			}
			return false;
		}
	}

	class ListFilterForbidden : ListFilter
	{
		bool show;
		public override IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			return list.Where(t => t.IsForbidden(Faction.OfPlayer) == show);
		}

		public override bool Listing(Listing_Standard listing)
		{
			if(listing.ButtonTextLabeled(show ? "Showing forbidden items" : "Showing allowed items", "Swap"))
			{
				show = !show;
				return true;
			}
			return false;
		}
	}
}
