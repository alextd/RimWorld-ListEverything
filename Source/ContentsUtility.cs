using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Verse;
using RimWorld;

namespace List_Everything
{
	[StaticConstructorOnStartup]
	public static class ContentsUtility
	{
		public static bool IsValidHolder(this IThingHolder holder)
			=> holder.IsEnclosingContainer() && !(holder is MinifiedThing);


		private static FieldInfo contentsKnownInfo = typeof(Building_Casket).GetField("contentsKnown", BindingFlags.NonPublic | BindingFlags.Instance);
		public static bool get_contentsKnown(this Building_Casket building) =>
			(bool)contentsKnownInfo.GetValue(building);

		public static bool CanPeekInventory(this IThingHolder holder) =>
			DebugSettings.godMode ||
			(holder is Building_Casket c ? c.get_contentsKnown() : true);

		public static List<Thing> AllKnownThings(IThingHolder holder)
		{
			if (holder == null) return new List<Thing>();

			List<Thing> knownThings = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(holder, knownThings, true, ContentsUtility.CanPeekInventory);
			return knownThings;
		}

		public static bool onlyAvailable = true;
		public static HashSet<T> AvailableOnMap<T>(Func<Thing, IEnumerable<T>> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach (Thing t in ContentsUtility.AllKnownThings(Find.CurrentMap))
				foreach (T tDef in validGetter(t))
					ret.Add(tDef);

			return ret;
		}

		public static HashSet<T> AvailableOnMap<T>(Func<Thing, T> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach (Thing t in ContentsUtility.AllKnownThings(Find.CurrentMap))
				if(validGetter(t) is T def)
					ret.Add(def);

			return ret;
		}
	}
}
