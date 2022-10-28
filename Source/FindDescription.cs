using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	// The FindDescription is the root owner of a set of filters.
	// (It's a little more than a mere ListFilterGroup)
	// - Get() method to actually perform the search
	// - BaseListType which narrows what that things to look at
	// - Checkbox bools that apply to all nested filters
	// - Apparently support for alerts which I'll probably separate out
	public class FindDescription : IExposable, IFilterOwnerAdder
	{
		public string name = "TD.NewFindFilters".Translate();
		public List<Thing> listedThings;

		public AlertPriority alertPriority;
		public int ticksToShowAlert;
		public int countToAlert;
		public CompareType countComp;

		public bool allMaps = false;

		private BaseListType _baseType;
		public BaseListType BaseType
		{
			get => _baseType;
			set
			{
				_baseType = value;
				RemakeList();
			}
		}
		public List<ListFilter> filters = new List<ListFilter>();

		public void RemakeList()
		{
			if (allMaps)
			{
				listedThings = new List<Thing>();
				foreach (Map map in Find.Maps)
					listedThings.AddRange(Get(map));
			}
			else
			{
				listedThings = Get(Find.CurrentMap).ToList();
			}
		}

		public virtual FindDescription Clone(Map map)
		{
			FindDescription newDesc = new FindDescription()
			{
				_baseType = _baseType,
				name = name,
				alertPriority = alertPriority,
				ticksToShowAlert = ticksToShowAlert,
				countToAlert = countToAlert,
				countComp = countComp,
				allMaps = allMaps
			};
			newDesc.filters = filters.Select(f => f.Clone(newDesc)).ToList();
			if(map != null)
				newDesc.filters.ForEach(f => f.DoResolveReference(map));
			return newDesc;
		}

		public IEnumerable<Thing> Get(Map map)
		{
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch (BaseType)
			{
				case BaseListType.Selectable:	//Known as "Map"
					allThings = map.listerThings.AllThings.Where(t => t.def.selectable);
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
				case BaseListType.All:
					allThings = ContentsUtility.AllKnownThings(map);
					break;

				//Devmode options:
				case BaseListType.Haulables:
					allThings = map.listerHaulables.ThingsPotentiallyNeedingHauling();
					break;
				case BaseListType.Mergables:
					allThings = map.listerMergeables.ThingsPotentiallyNeedingMerging();
					break;
				case BaseListType.FilthInHomeArea:
					allThings = map.listerFilthInHomeArea.FilthInHomeArea;
					break;
			}

			//Filters
			allThings = allThings.Where(t => !(t.ParentHolder is Corpse) && !(t.ParentHolder is MinifiedThing));
			if (!DebugSettings.godMode)
			{
				allThings = allThings.Where(t => ValidDef(t.def));
				allThings = allThings.Where(t => !t.PositionHeld.Fogged(map));
			}
			foreach (ListFilter filter in filters)
				allThings = filter.Apply(allThings);

			//Sort
			return allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) => 
			!typeof(Mote).IsAssignableFrom(def.thingClass) && 
			def.drawerType != DrawerType.None;

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref _baseType, "baseType");
			Scribe_Collections.Look(ref filters, "filters");
			Scribe_Values.Look(ref alertPriority, "alertPriority");
			Scribe_Values.Look(ref ticksToShowAlert, "ticksToShowAlert");
			Scribe_Values.Look(ref countToAlert, "countToAlert");
			Scribe_Values.Look(ref countComp, "countComp");
			Scribe_Values.Look(ref allMaps, "allMaps");

			//Should we set filters owner to this? Only map clones that are copied need it, and they get it in the clone.
		}


		public FindDescription RootFindDesc => this;

		public void Add(ListFilter newFilter)
		{
			filters.Add(newFilter);
			RemakeList();
		}
	}

	public enum BaseListType
	{
		Selectable,
		Everyone,
		Colonists,
		Animals,
		Items,
		Buildings,
		Natural,
		Plants,
		Inventory,
		All,
		Haulables,
		Mergables,
		FilthInHomeArea
	}

	public static class BaseListNormalTypes
	{
		public static readonly BaseListType[] normalTypes =
			{ BaseListType.Selectable, BaseListType.Everyone, BaseListType.Colonists, BaseListType.Animals, BaseListType.Items,
			BaseListType.Buildings, BaseListType.Natural, BaseListType.Plants, BaseListType.Inventory, BaseListType.All};
	}
}
