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
		private Stack<float> indentSizes = new Stack<float>();
		private Stack<float> indentHeights = new Stack<float>();

		public void Indent(float size)
		{
			curX += size;
			totalIndent += size;
			SetWidthForIndent();
			indentSizes.Push(size);
			indentHeights.Push(curY);
		}

		public void EndIndent()
		{
			if (indentSizes.Count > 0)
			{
				float size = indentSizes.Pop();
				curX -= size;
				totalIndent -= size;
				SetWidthForIndent();

				//Draw vertical line marking indention
				float startHeight = indentHeights.Pop();
				GUI.color = Color.grey;
				Widgets.DrawLineVertical(curX, startHeight, curY - startHeight - verticalSpacing);//TODO columns?
				GUI.color = Color.white;
			}
		}

		public void SetWidthForIndent()
		{
			ColumnWidth = listingRect.width - totalIndent;
		}
	}
}
