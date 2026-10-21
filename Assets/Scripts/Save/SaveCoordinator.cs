using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class FoundationSaveData
    {
        public string format = "GreatVoyage.Game";
        public int version = 10;
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
        public EventState events;
        public List<PrisonerState> prisoners = new List<PrisonerState>();
        public int prisonersFedDay;
        public List<OwnedShipState> inactiveShips = new List<OwnedShipState>();
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
        public int day;
        public float localStanding;
        public string condition;
        public List<MarketQuote> quotes;
        public List<ResidentRequest> requests;
        public List<PortQuest> quests;
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
        private sealed class SaveFieldResolver : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(field => !field.IsDefined(typeof(NonSerializedAttribute), true))
                    .Select(field => base.CreateProperty(field, MemberSerialization.Fields)).ToList();
            }
        }

        private static readonly JsonSerializerSettings SnapshotSettings = new JsonSerializerSettings
        {
            ContractResolver = new SaveFieldResolver(),
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            TypeNameHandling = TypeNameHandling.None,
            MaxDepth = 64
        };

        // Role: null인 선택 상태를 보존하고 기존 공개 필드 형식만 직렬화한다.
        internal static string EncodeSnapshot(object value, bool pretty = false) => JsonConvert.SerializeObject(value, pretty ? Formatting.Indented : Formatting.None, SnapshotSettings);
        internal static T DecodeSnapshot<T>(string json) => JsonConvert.DeserializeObject<T>(json, SnapshotSettings);

        public string FilePath { get; }

        private string sourcePath, sourceHash;
        private int sourceVersion;
        private string sourceSpace;
        public SaveCoordinator(string slot)
        {
            if (string.IsNullOrWhiteSpace(slot) || (slot != "great-voyage" && !slot.StartsWith("reconstruction-", StringComparison.Ordinal)) || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || slot.Contains("/") || slot.Contains("\\"))
                throw new InvalidDataException("Invalid game save slot");
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
            var data = DecodeSnapshot<FoundationSaveData>(File.ReadAllText(FilePath, Encoding.UTF8));
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
                crew = DecodeSnapshot<CrewRosterState>(EncodeSnapshot(data.crew)),
                economy = DecodeSnapshot<EconomyState>(EncodeSnapshot(data.economy)),
                journal = data.journal,
                sleep = data.sleep,
                inventory = data.inventory,
                events = data.events ?? new EventState(),
                prisoners = data.prisoners ?? new List<PrisonerState>(),
                prisonersFedDay = data.prisonersFedDay,
                inactiveShips = data.inactiveShips ?? new List<OwnedShipState>()
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
            var data = Capture(state);
            var active = state.events?.current;
            if (active != null && active.phase != EventPhase.Resolved)
            {
                data = DecodeSnapshot<FoundationSaveData>(active.checkpoint);
                var restart = DecodeSnapshot<EventState>(EncodeSnapshot(state.events));
                restart.current.phase = EventPhase.Announced;
                restart.current.progressedSeconds = 0;
                restart.current.randomState = restart.current.initialSeed;
                restart.current.choice = null;
                restart.current.effectsApplied = false;
                restart.current.result = null;
                restart.current.battle = null;
                restart.current.pests = null;
                restart.current.dispatchedCrewId = null;
                data.events = restart;
            }
            data.sourcePath = sourcePath;
            data.sourceSha256 = sourceHash;
            data.sourceVersion = sourceVersion;
            data.sourceSpace = sourceSpace;
            Write(data);
        }

        public static FoundationSaveData Capture(GameState state)
        {
            return new FoundationSaveData
            {
                playthroughId = state.playthroughId,
                shipInstanceId = state.shipInstanceId,
                elapsedSeconds = state.elapsedSeconds,
                cellSize = state.cellSize,
                player = state.player,
                ship = state.ship,
                navigation = state.navigation,
                crew = DecodeSnapshot<CrewSaveData>(EncodeSnapshot(state.crew)),
                economy = DecodeSnapshot<EconomySaveData>(EncodeSnapshot(state.economy)),
                journal = state.journal,
                sleep = state.sleep,
                inventory = state.inventory,
                events = state.events,
                prisoners = state.prisoners,
                prisonersFedDay = state.prisonersFedDay,
                inactiveShips = state.inactiveShips,
                inactiveCharacters = state.characters.Where(member => !state.crew.members.Any(active => active.id == member.id)).ToList()
            };
        }

        private void Write(FoundationSaveData data)
        {
            data = DecodeSnapshot<FoundationSaveData>(EncodeSnapshot(data));
            Validate(data);
            byte[] bytes = Encoding.UTF8.GetBytes(EncodeSnapshot(data, true));
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

        public static void ValidateCheckpoint(FoundationSaveData data) => Validate(data, true);

        private static void Validate(FoundationSaveData data, bool checkpoint = false)
        {
            if (data == null || data.format != "GreatVoyage.Game" || (data.version != 9 && data.version != 10) || !Guid.TryParse(data.playthroughId, out _) || string.IsNullOrEmpty(data.shipInstanceId) || data.player == null || data.ship == null || data.navigation == null || data.crew?.members == null || data.economy == null || data.journal == null || data.sleep == null || data.inventory?.items == null || data.inactiveCharacters == null)
                throw new InvalidDataException("Invalid foundation save structure");
            if (data.version == 10 && data.events == null && !checkpoint)
                throw new InvalidDataException("Missing event state");
            EventRules.Validate(data.events, data.playthroughId);
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
                if (member == null || string.IsNullOrEmpty(member.id) || !ids.Add(member.id) || !Finite(member.health) || member.health < 0 || !Finite(member.strength) || member.strength < 0 || !Finite(member.trust) || member.trust < 0 || member.trust > 100 || !Enum.IsDefined(typeof(WeaponKind), member.weapon))
                    throw new InvalidDataException("Invalid character registry");
            if (data.prisonersFedDay < 0 || data.prisonersFedDay > (int)(data.elapsedSeconds / GameRuntime.SecondsPerDay) + 1)
                throw new InvalidDataException("Invalid prisoner feeding date");
            var prisoners = new HashSet<string>();
            foreach (var prisoner in data.prisoners ?? new List<PrisonerState>())
                if (prisoner == null || !ids.Contains(prisoner.characterId) || !prisoners.Add(prisoner.characterId) || string.IsNullOrEmpty(prisoner.sourceEvent) || (prisoner.handedOver ? 1 : 0) + (prisoner.executed ? 1 : 0) + (prisoner.pardoned ? 1 : 0) > 1)
                    throw new InvalidDataException("Invalid prisoner identity or disposition");
            var ships = new HashSet<string> { data.shipInstanceId };
            foreach (var owned in data.inactiveShips ?? new List<OwnedShipState>())
                if (owned == null || string.IsNullOrEmpty(owned.id) || !ships.Add(owned.id) || string.IsNullOrEmpty(owned.portId) || owned.ship?.facilities == null || string.IsNullOrEmpty(owned.ship.definitionId))
                    throw new InvalidDataException("Invalid owned ship identity or location");
            var battle = data.events?.current?.battle;
            if (battle != null)
            {
                var actors = new HashSet<string>();
                if (battle.actors == null || battle.projectiles == null || !Finite(battle.elapsed) || battle.elapsed < 0)
                    throw new InvalidDataException("Invalid battle state");
                foreach (var actor in battle.actors)
                    if (actor == null || !ids.Contains(actor.characterId) || !actors.Add(actor.characterId) || !Finite(actor.cooldown) || actor.cooldown < 0 || !Finite(actor.position.x) || !Finite(actor.position.y))
                        throw new InvalidDataException("Invalid battle actor");
                foreach (var projectile in battle.projectiles)
                    if (projectile == null || !actors.Contains(projectile.sourceId) || !actors.Contains(projectile.targetId) || !Finite(projectile.damage) || projectile.damage < 0 || !Finite(projectile.position.x) || !Finite(projectile.position.y))
                        throw new InvalidDataException("Invalid battle projectile");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
