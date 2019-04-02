using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	public static class GetInnerThingUtility
	{
		public static Thing GetInnerThing(this Thing outerThing)
		{
			if (outerThing is MinifiedThing mt) return mt.InnerThing;
			if (outerThing is Corpse corpse) return corpse.InnerPawn;
			return outerThing;
		}

		public static Thing GetParentOfInnerThing(this Thing innerThing)
		{
			if (innerThing.ParentHolder is MinifiedThing mt) return mt;
			if (innerThing.ParentHolder is Corpse corpse) return corpse;
			return innerThing;
		}
	}
}
