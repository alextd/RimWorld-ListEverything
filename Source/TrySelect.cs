using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;


namespace List_Everything
{
	static class TrySelect
	{
		public static void Select(Thing t, bool playSound = true)
		{
			if(t.Spawned)
				Find.Selector.Select(t, playSound);
		}
	}
}
