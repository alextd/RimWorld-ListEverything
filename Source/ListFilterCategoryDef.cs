using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace List_Everything
{
	//Both ListFilterDef and ListFilterCategoryDef extend ListFilterSelectableDef, so they show up in the main list alongside each other in order of the xml
	public abstract class ListFilterSelectableDef : Def
	{
		public bool devOnly;
	}

	// There are too many filter subclasses to globally list them
	// So group them in categories
	// Then only the filters not nested under category will be globally listed,
	// subfilters popup when the category is selected
	public class ListFilterCategoryDef : ListFilterSelectableDef
	{ 
		private List<ListFilterDef> subFilters = null;
		public IEnumerable<ListFilterDef> SubFilters =>
			subFilters ?? Enumerable.Empty<ListFilterDef>();

		public override IEnumerable<string> ConfigErrors()
		{
			if (subFilters.NullOrEmpty())
				yield return "ListFilterCategoryDef needs to set subFilters";
		}
	}
}
