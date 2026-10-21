using System;
using System.Collections.Generic;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FacilityState
    {
        public string id;
        public string definitionId;
        public string floorId;
        public int x;
        public int y;
        public float durability;
        public bool sailDeployed = true;
        public int level = 1;
        public string fixedTileId;
    }

    [Serializable]
    public sealed class FloorVisit
    {
        public string floorId;
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class ShipState
    {
        public string definitionId;
        public string activeFloor;
        public int layoutVersion;
        public List<string> unlockedTiles = new List<string>();
        public List<FacilityState> facilities = new List<FacilityState>();
        public List<FloorVisit> visits = new List<FloorVisit>();
    }

    [Serializable]
    public sealed class OwnedShipState
    {
        public string id;
        public string portId;
        public ShipState ship;
    }
}
