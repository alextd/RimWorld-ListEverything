using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace List_Everything
{
	static class TranslateEnumEx
	{
		public static string TranslateEnum(this object e)
		{
			return ("TD."+e.ToString()).Translate();
		}
	}
}
