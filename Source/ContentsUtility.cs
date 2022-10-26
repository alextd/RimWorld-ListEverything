using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	[StaticConstructorOnStartup]
	public static class ContentsUtility
	{
		public static bool IsValidHolder(this IThingHolder holder)
			=> holder.IsEnclosingContainer() && !(holder is MinifiedThing);


		public static bool CanPeekInventory(this IThingHolder holder) =>
			DebugSettings.godMode ||
			(holder is Building_Casket c ? c.contentsKnown : true) &&
			!(holder is TradeShip);

		public static List<Thing> AllKnownThings(IThingHolder holder)
		{
			if (holder == null) return new List<Thing>();

			List<Thing> knownThings = new List<Thing>();
			ThingOwnerUtility.GetAllThingsRecursively(holder, knownThings, true, ContentsUtility.CanPeekInventory);
			return knownThings.FindAll(t => DebugSettings.godMode || !t.PositionHeld.Fogged(t.MapHeld));
		}

		public static bool onlyAvailable = true;
		public static HashSet<T> AvailableInGame<T>(Func<Thing, IEnumerable<T>> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach(Map map in Find.Maps)
				foreach (Thing t in ContentsUtility.AllKnownThings(map))
					foreach (T tDef in validGetter(t))
						ret.Add(tDef);

			return ret;
		}

		public static HashSet<T> AvailableInGame<T>(Func<Thing, T> validGetter)
		{
			HashSet<T> ret = new HashSet<T>();
			foreach (Map map in Find.Maps)
				foreach (Thing t in ContentsUtility.AllKnownThings(map))
					if(validGetter(t) is T def)
						ret.Add(def);

			return ret;
		}
	}
}
