using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace List_Everything
{
	public static class IntRangeIncludesPlease
	{
		public static bool Includes(this IntRange range, int x) =>
			x >= range.min && x <= range.max;
	}
}
