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
			LongEventHandler.ExecuteWhenFinished(() => {
				Log.Warning($"Hey! List Everything has been turned into the mods Ctrl-F and Custom Alerts for 1.4. Go use those!");

				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
					"Hey! List Everything has been turned into the mods Ctrl-F and Custom Alerts for 1.4. Go use those!", () => { }));
			});
		}
	}
}