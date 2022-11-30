using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
			Log.Warning($"Hey! List Everything has been turned into the mods Ctrl-F and Custom Alerts for 1.4. Go use those!");

			LongEventHandler.QueueLongEvent(() =>
				Find.WindowStack?.Add(new Dialog_MessageBox("Hey! List Everything has been turned into the mods Ctrl-F and Custom Alerts for 1.4. Go use those!")),
				"List Everyting", true, _ => { });
		}
	}
}