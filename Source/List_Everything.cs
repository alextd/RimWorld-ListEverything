using System.Reflection;
using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;
using Harmony;

namespace List_Everything
{
	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
#if DEBUG
			HarmonyInstance.DEBUG = true;
#endif

			HarmonyInstance harmony = HarmonyInstance.Create("Uuugggg.rimworld.List_Everything.main");
			
			harmony.PatchAll();
		}
	}
}