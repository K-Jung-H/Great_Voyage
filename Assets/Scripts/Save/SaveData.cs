using System;

namespace GreatVoyage
{
    [Serializable]
    public sealed class SaveData
    {
        public string format = "GreatVoyage.CabinSession";
        public int version = 8;
        public string playthroughId;
        public string playerId = "player";
        public string spaceId = "captains-cabin";
        public double elapsedSeconds = 200d;
        public float playerX;
        public float playerY;
        public ShipState ship;
        public NavigationState navigation;
        public CrewRosterState crew;
        public EconomyState economy;
        public bool economyEnabled;
        public bool journalEnabled;
        public JournalState journal;
        public PlayerSleepState sleep;
    }
}
