using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace List_Everything
{
	public class Mod : Verse.Mod
	{
		public static Settings settings;
		public Mod(ModContentPack content) : base(content)
		{
			LongEventHandler.ExecuteWhenFinished(() => { settings = GetSettings<Settings>(); });
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "TD.ListEverything".Translate();
		}
	}
}