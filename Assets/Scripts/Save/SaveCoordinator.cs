using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FoundationSaveData
    {
        public string format = "GreatVoyage.Game";
        public int version = 9;
        public string playthroughId, shipInstanceId, sourcePath, sourceSha256;
        public int sourceVersion;
        public string sourceSpace;
        public double elapsedSeconds;
        public float cellSize;
        public PlayerLocation player;
        public ShipState ship;
        public NavigationState navigation;
        public CrewSaveData crew;
        public EconomySaveData economy;
        public JournalState journal;
        public PlayerSleepState sleep;
        public InventoryState inventory;
        public List<CrewState> inactiveCharacters = new List<CrewState>();
    }

    [Serializable]
    public sealed class CrewSaveData
    {
        public string settingsId;
        public double remainder;
        public List<CrewState> members;
    }

    [Serializable]
    public sealed class MarketSaveData
    {
        public string portId;
    }

    [Serializable]
    public sealed class ContractSaveData
    {
        public string id, crewId, endedPort;
        public ContractPayment payment;
        public ContractStatus status;
        public int totalPrice, remainingVoyages, lastVoyage, debt, endedVoyage;
    }

    [Serializable]
    public sealed class EconomySaveData
    {
        public string settingsId;
        public float reputation;
        public bool gameOver, settlementPending;
        public int contractSequence, lastVoyage;
        public List<MarketSaveData> markets;
        public List<ContractSaveData> contracts;
    }

    public sealed class SaveCoordinator
    {
        public string FilePath { get; }

        private string sourcePath, sourceHash;
        private int sourceVersion;
        private string sourceSpace;
        public SaveCoordinator(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || !slot.StartsWith("reconstruction-", StringComparison.Ordinal) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains("/") || slot.Contains("\\"))
                throw new InvalidDataException("Only reconstruction slots are allowed");
            FilePath = Path.Combine(Application.persistentDataPath, "Saves", Application.isEditor ? "Editor" : "Player", slot + ".json");
        }

        public GameState Load(string legacyCopy)
        {
            if (File.Exists(FilePath + ".tmp"))
                throw new InvalidDataException("Unfinished save preserved");
            if (!File.Exists(FilePath))
            {
                if (File.Exists(FilePath + ".bak"))
                    throw new InvalidDataException("Missing last save; backup preserved");
                if (string.IsNullOrWhiteSpace(legacyCopy))
                    return null;
                sourcePath = Path.GetFullPath(legacyCopy);
                string importRoot = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "ReconstructionImports")) + Path.DirectorySeparatorChar;
                if (!sourcePath.StartsWith(importRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Place an approved source copy in ReconstructionImports; original slots are never imported directly");
                byte[] bytes = File.ReadAllBytes(sourcePath);
                if (bytes.Length > 1024 * 1024)
                    throw new InvalidDataException("Legacy save exceeds size limit");
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    sourceHash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                string json = Encoding.UTF8.GetString(bytes);
                var header = JsonUtility.FromJson<SaveData>(json);
                if (!new[]
                {
                    "captains-cabin",
                    "crew-prototype",
                    "port-prototype"
                }.Contains(header.spaceId))
                    throw new InvalidDataException("Unknown legacy space");
                sourceVersion = header.version;
                sourceSpace = header.spaceId;
                return LegacySaveAdapter.Convert(new LegacySaveAdapter().Decode(json, header.spaceId));
            }

            if (new FileInfo(FilePath).Length > 1024 * 1024)
                throw new InvalidDataException("Save exceeds size limit");
            var data = JsonUtility.FromJson<FoundationSaveData>(File.ReadAllText(FilePath, Encoding.UTF8));
            Validate(data);
            sourcePath = data.sourcePath;
            sourceHash = data.sourceSha256;
            sourceVersion = data.sourceVersion;
            sourceSpace = data.sourceSpace;
            var state = new GameState
            {
                playthroughId = data.playthroughId,
                shipInstanceId = data.shipInstanceId,
                elapsedSeconds = data.elapsedSeconds,
                cellSize = data.cellSize,
                player = data.player,
                ship = data.ship,
                navigation = data.navigation,
                crew = JsonUtility.FromJson<CrewRosterState>(JsonUtility.ToJson(data.crew)),
                economy = JsonUtility.FromJson<EconomyState>(JsonUtility.ToJson(data.economy)),
                journal = data.journal,
                sleep = data.sleep,
                inventory = data.inventory
            };
            foreach (var member in state.crew.members)
                state.Register(member);
            foreach (var member in data.inactiveCharacters)
            {
                if (state.characters.Any(c => c.id == member.id))
                    throw new InvalidDataException("Duplicate character ID");
                state.Register(member);
            }

            return state;
        }

        public void Save(GameState state)
        {
            var data = new FoundationSaveData
            {
                playthroughId = state.playthroughId,
                shipInstanceId = state.shipInstanceId,
                sourcePath = sourcePath,
                sourceSha256 = sourceHash,
                elapsedSeconds = state.elapsedSeconds,
                cellSize = state.cellSize,
                player = state.player,
                ship = state.ship,
                navigation = state.navigation,
                crew = JsonUtility.FromJson<CrewSaveData>(JsonUtility.ToJson(state.crew)),
                economy = JsonUtility.FromJson<EconomySaveData>(JsonUtility.ToJson(state.economy)),
                journal = state.journal,
                sleep = state.sleep,
                inventory = state.inventory,
                inactiveCharacters = state.characters.Where(member => !state.crew.members.Any(active => active.id == member.id)).ToList()
            };
            data = JsonUtility.FromJson<FoundationSaveData>(JsonUtility.ToJson(data));
            data.sourceVersion = sourceVersion;
            data.sourceSpace = sourceSpace;
            Validate(data);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
            if (bytes.Length > 1024 * 1024)
                throw new InvalidDataException("Save exceeds size limit; existing file preserved");
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            string temporary = FilePath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (File.Exists(FilePath))
                File.Replace(temporary, FilePath, FilePath + ".bak");
            else
                File.Move(temporary, FilePath);
        }

        private static void Validate(FoundationSaveData data)
        {
            if (data == null || data.format != "GreatVoyage.Game" || data.version != 9 || !Guid.TryParse(data.playthroughId, out _) || string.IsNullOrEmpty(data.shipInstanceId) || data.player == null || data.ship == null || data.navigation == null || data.crew?.members == null || data.economy == null || data.journal == null || data.sleep == null || data.inventory?.items == null || data.inactiveCharacters == null)
                throw new InvalidDataException("Invalid v9 save structure");
            if (double.IsNaN(data.elapsedSeconds) || double.IsInfinity(data.elapsedSeconds) || data.elapsedSeconds < 0 || data.elapsedSeconds > 600d * 365000d || !Finite(data.player.position.x) || !Finite(data.player.position.y) || string.IsNullOrEmpty(data.player.areaId) || data.inventory.gold < 0)
                throw new InvalidDataException("Invalid time, position or wallet");
            if (!Finite(data.cellSize) || data.cellSize <= 0 || data.player.id != "player" || data.player.visits == null)
                throw new InvalidDataException("Invalid coordinate scale or player identity");
            var ids = new HashSet<string>();
            foreach (var visit in data.player.visits)
                if (visit == null || string.IsNullOrEmpty(visit.id) || !Finite(visit.position.x) || !Finite(visit.position.y) || !ids.Add(visit.id))
                    throw new InvalidDataException("Invalid area visits");
            ids.Clear();
            foreach (var item in data.inventory.items)
                if (item == null || string.IsNullOrEmpty(item.ownerId) || string.IsNullOrEmpty(item.productId) || item.quantity < 0 || !ids.Add(item.ownerId + ":" + item.productId))
                    throw new InvalidDataException("Invalid inventory");
            ids.Clear();
            foreach (var member in data.crew.members.Concat(data.inactiveCharacters))
                if (member == null || string.IsNullOrEmpty(member.id) || !ids.Add(member.id))
                    throw new InvalidDataException("Invalid character registry");
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
