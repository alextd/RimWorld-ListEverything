using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace List_Everything
{
	public class Dialog_Name : Dialog_Rename
	{
		Action<string> setNameAction;

		public Dialog_Name(Action<string> act)
		{
			curName = "";
			setNameAction = act;
		}

		public Dialog_Name(string name, Action<string> act)
		{
			curName = name;
			setNameAction = act;
		}

		//protected but using publicized assembly
		//protected override void SetName(string name)
		public override void SetName(string name)
		{
			setNameAction(name);
		}
	}
}
