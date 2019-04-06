using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace List_Everything
{
	public class Listing_StandardIndent : Listing_Standard
	{
		float totalIndent;
		private Stack<float> tabSizes = new Stack<float>();

		public void Indent(float size)
		{
			curX += size;
			totalIndent += size;
			SetWidthForIndent();
			tabSizes.Push(size);
		}

		public void EndIndent()
		{
			if (tabSizes.Count > 0)
			{
				float size = tabSizes.Pop();
				curX -= size;
				totalIndent -= size;
				SetWidthForIndent();
			}
		}

		public void SetWidthForIndent()
		{
			ColumnWidth = listingRect.width - totalIndent;
		}
	}
}
