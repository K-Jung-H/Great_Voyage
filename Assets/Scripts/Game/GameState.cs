using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class PlayerLocation
    {
        public string id = "player";
        public Vector2 position;
        public Vector2 linearVelocity;
        public string areaId = "aboard";
        public List<AreaVisit> visits = new List<AreaVisit>();
    }

    [Serializable]
    public sealed class AreaVisit
    {
        public string id;
        public Vector2 position;
    }

    public sealed class GameState
    {
        public string playthroughId;
        public string shipInstanceId;
        public double elapsedSeconds = 200;
        public float cellSize;
        public PlayerLocation player = new PlayerLocation();
        public ShipState ship;
        public NavigationState navigation;
        public CrewRosterState crew;
        public EconomyState economy;
        public JournalState journal;
        public PlayerSleepState sleep;
        public InventoryState inventory = new InventoryState();
        public EventState events = new EventState();
        public List<PrisonerState> prisoners = new List<PrisonerState>();
        public int prisonersFedDay;
        public List<OwnedShipState> inactiveShips = new List<OwnedShipState>();
        public List<CrewState> characters = new List<CrewState>();
        public void Register(CrewState member)
        {
            if (member == null)
                return;
            int index = characters.FindIndex(value => value.id == member.id);
            if (index < 0)
                characters.Add(member);
            else
                characters[index] = member;
        }
    }
}
