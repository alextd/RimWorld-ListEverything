using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace List_Everything
{
	// The FindDescription is the root owner of a set of filters,
	// (It's a little more than a mere ListFilterGroup)
	// - Holds the list of things and performs the search
	// - BaseListType which narrows what that things to look at
	// - Checkbox bool allMaps that apply to all nested filters
	// - Apparently support for alerts which I'll probably separate out
	public class FindDescription : IExposable, IFilterHolder
	{
		public string name = "TD.NewFindFilters".Translate();
		
		private List<Thing> listedThings = new();
		public IEnumerable<Thing> ListedThings => listedThings;

		private FilterHolder children;
		public FilterHolder Children => children;

		// from IFilterHolder
		public FindDescription RootFindDesc => this;

		//Map stuff. Not saved to file. Clone(map) to set map.
		private Map _map;
		public string mapLabel;
		public bool allMaps = false;
		public Map map
		{
			get => _map;
			set
			{
				_map = value;

				StringBuilder sb = new(" (");

				if (CurrentMapOnly())
					sb.Append("Current Map");
				else if (_map?.Parent.LabelCap is string label)
					sb.Append(label);
				else
					sb.Append("TD.AllMaps".Translate());

				sb.Append(")");

				mapLabel = sb.ToString();
			}
		}

		//Certain filters only work on the current map, so the entire tree will only work on the current map
		public bool CurrentMapOnly() => Children.Check(f => f.CurrentMapOnly);


		public FindDescription()
		{
			children = new FilterHolder(this);
		}

		public FindDescription(Map m)
		{
			map = m;
		}

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

		public void RemakeList()
		{
			listedThings.Clear();


			// "Current map only" overrides other choices
			if (CurrentMapOnly())
				listedThings.AddRange(Get(Find.CurrentMap));

			// All maps
			else if (allMaps)
				foreach (Map m in Find.Maps)
					listedThings.AddRange(Get(m));

			// Single map
			else if (map != null)
				listedThings.AddRange(Get(map));

			// Probably don't want to
			// Assume you want the current map!
			//else
			//	listedThings.AddRange(Get(Find.CurrentMap));
		}


		public void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Values.Look(ref _baseType, "baseType");
			Scribe_Values.Look(ref allMaps, "allMaps");
			//Does not save/load _map : Clone(map) a loaded FindDescription

			Children.ExposeData();
		}
		public FindDescription Clone(Map newMap, bool resolve = true)
		{
			FindDescription newDesc = new FindDescription()
			{
				_baseType = _baseType,
				name = name,
				allMaps = allMaps,
				map = newMap
			};
			newDesc.children = children.Clone(newDesc);
			if (resolve)
				foreach (var f in newDesc.Children.Filters)
					f.DoResolveReference(newMap);

			return newDesc;
		}

		private IEnumerable<Thing> Get(Map map)
		{
			IEnumerable<Thing> allThings = Enumerable.Empty<Thing>();
			switch (BaseType)
			{
				case BaseListType.Selectable: //Known as "Map"
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
			foreach (ListFilter filter in Children.Filters)
				allThings = filter.Apply(allThings);

			//Sort
			return allThings.OrderBy(t => t.def.shortHash).ThenBy(t => t.Stuff?.shortHash ?? 0).ThenBy(t => t.Position.x + t.Position.z * 1000).ToList();
		}

		//Probably a good filter
		public static bool ValidDef(ThingDef def) =>
			!typeof(Mote).IsAssignableFrom(def.thingClass) &&
			def.drawerType != DrawerType.None;
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

		//devmode options
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
