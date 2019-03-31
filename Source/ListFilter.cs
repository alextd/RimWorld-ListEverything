using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	class ListFilter
	{
		string name = "";
		public IEnumerable<Thing> Apply(IEnumerable<Thing> list)
		{
			if (name.NullOrEmpty())
				return list;
			return list.Where(t => t.Label.ToLower().Contains(name.ToLower()));
		}

		public bool Listing(Listing_Standard listing)
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
}
