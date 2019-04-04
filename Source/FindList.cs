using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	class SavedFilter : IExposable
	{
		public BaseListType baseType;
		public List<ListFilter> list = new List<ListFilter>();

		public void ExposeData()
		{
			Scribe_Values.Look(ref baseType, "baseType");
			Scribe_Collections.Look(ref list, "list");
		}
	}

	public enum BaseListType
	{
		All,
		Items,
		Everyone,
		Colonists,
		Animals,
		Buildings,
		Plants,
		Inventory,
		ThingRequestGroup,
		Haulables,
		Mergables,
		Filth
	};

	public static class FindList
	{
		public static List<Thing> Get(BaseListType baseType, List<ListFilter> filters)
		{
			Map map = Find.CurrentMap;
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch (baseType)
			{
				case BaseListType.All:
					allThings = ContentsUtility.AllKnownThings(map);
					break;
				case BaseListType.Buildings:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
					break;
				case BaseListType.Plants:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant);
					break;
				case BaseListType.Inventory:
					List<IThingHolder> holders = new List<IThingHolder>();
					map.GetChildHolders(holders);
					List<Thing> list = new List<Thing>();
					foreach (IThingHolder holder in holders.Where(ContentsUtility.CanPeekInventory))
						list.AddRange(ContentsUtility.AllKnownThings(holder));
					allThings = list;
					break;
				case BaseListType.Items:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableAlways);
					break;
				case BaseListType.Everyone:
					allThings = map.mapPawns.AllPawnsSpawned.Cast<Thing>();
					break;
				case BaseListType.Colonists:
					allThings = map.mapPawns.FreeColonistsSpawned.Cast<Thing>();
					break;
				case BaseListType.Animals:
					allThings = map.mapPawns.AllPawnsSpawned.Where(p => !p.RaceProps.Humanlike).Cast<Thing>();
					break;
				case BaseListType.Haulables:
					allThings = map.listerHaulables.ThingsPotentiallyNeedingHauling();
					break;
				case BaseListType.Mergables:
					allThings = map.listerMergeables.ThingsPotentiallyNeedingMerging();
					break;
				case BaseListType.Filth:
					allThings = map.listerFilthInHomeArea.FilthInHomeArea;
					break;
			}

			//Filters
			allThings = allThings.Where(t => !(t.ParentHolder is Corpse) && !(t.ParentHolder is MinifiedThing));
			if (!DebugSettings.godMode)
			{
				allThings = allThings.Where(t => t.def.drawerType != DrawerType.None);//Probably a good filter
				allThings = allThings.Where(t => !t.PositionHeld.Fogged(map));
			}
			foreach (ListFilter filter in filters)
				allThings = filter.Apply(allThings);

			//Sort
			return allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}
	}
}
