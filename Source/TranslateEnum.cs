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
			string type = e.GetType().Name;
			string name = e.ToString();
			string key = ("TD." + type + "." + name);

			TaggedString result;
			if (key.TryTranslate(out result))
				return result;
			if (name.TryTranslate(out result))
				return result;
			//return key.Translate(); //And get markings on letters, nah.
			return name;
		}
	}
}
