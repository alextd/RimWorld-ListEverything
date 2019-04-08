using System.Reflection;
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
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			GetSettings<Settings>().DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "List Everything";
		}
	}
}