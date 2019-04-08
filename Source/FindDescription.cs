using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	public class FindDescription : IExposable
	{
		public string name = "TD.NewFindFilters".Translate();
		public AlertPriority alertPriority;
		public int ticksToShowAlert;
		public int countToAlert;
		public CompareType countComp;
		public bool allMaps = false;
		public bool locked = false;

		public BaseListType baseType;
		public List<ListFilter> filters = new List<ListFilter>();

		public virtual FindDescription Clone(Map map)
		{
			FindDescription newDesc = new FindDescription()
			{
				baseType = baseType,
				name = name,
				alertPriority = alertPriority,
				ticksToShowAlert = ticksToShowAlert,
				countToAlert = countToAlert,
				countComp = countComp,
				allMaps = allMaps,
				locked = locked
			};
			newDesc.filters = filters.Select(f => f.Clone(map, newDesc)).ToList();
			return newDesc;
		}

		public List<Thing> Get(Map map)
		{
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch (baseType)
			{
				case BaseListType.All:
					allThings = ContentsUtility.AllKnownThings(map);
					break;
				case BaseListType.Buildings:
					allThings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);
					break;
				case BaseListType.Natural:
					allThings = map.listerThings.AllThings.Where(t => t.def.filthLeaving == ThingDefOf.Filth_RubbleRock); 
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

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref baseType, "baseType");
			Scribe_Collections.Look(ref filters, "filters");
			Scribe_Values.Look(ref alertPriority, "alertPriority");
			Scribe_Values.Look(ref ticksToShowAlert, "ticksToShowAlert");
			Scribe_Values.Look(ref countToAlert, "countToAlert");
			Scribe_Values.Look(ref countComp, "countComp");
			Scribe_Values.Look(ref allMaps, "allMaps");
			Scribe_Values.Look(ref locked, "locked");

			//Should we set filters owner to this? Only map clones that are copied need it, and they get it in the clone.
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
		Natural,
		Plants,
		Inventory,
		ThingRequestGroup,
		Haulables,
		Mergables,
		Filth
	}

	public static class BaseListNormalTypes
	{
		public static readonly BaseListType[] normalTypes =
			{ BaseListType.All, BaseListType.Items, BaseListType.Everyone, BaseListType.Colonists, BaseListType.Animals,
			BaseListType.Buildings, BaseListType.Natural, BaseListType.Plants, BaseListType.Inventory};
	}
}
