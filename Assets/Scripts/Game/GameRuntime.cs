using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class GameRuntime : IDisposable
    {
        public const double SecondsPerDay = 600d;
        public GameState State { get; private set; }
        public PauseService Pauses { get; } = new PauseService();
        public bool IsPaused => Pauses.IsPaused;
        public bool IsReady { get; private set; }
        public bool IsSleeping => Rest != null && Rest.IsSleeping;
        public string ErrorMessage { get; private set; }
        public string ErrorOperation { get; private set; }
        public double ElapsedSeconds => State.elapsedSeconds;
        public ShipModel Ship { get; private set; }
        public NavigationRules Navigation { get; private set; }
        public CrewBrain Crew { get; private set; }
        public EconomyRules Economy { get; private set; }
        public JournalRules Journal { get; private set; }
        public RestSession Rest { get; private set; }
        public SaveCoordinator Saves { get; private set; }
        public SimulationLoop Loop { get; private set; }
        public EventRules Events { get; private set; }
        public BattleRules Battle { get; private set; }
        public PrisonerRules Prisoners { get; private set; }
        public ShipyardRules Shipyard { get; private set; }
        public CrewLifeRules Life { get; private set; }
        public MarketRules Market { get; private set; }
        public PestRules Pests { get; private set; }
        public DetectionRules Detection { get; private set; }

        private IDisposable errorPause;
        private bool saveRequested;
        public event Action<string> Error;
        public event Action<bool> Changed;
        public GameRuntime(GameStartProfile profile, Vector2 startingPosition)
        {
            Saves = new SaveCoordinator(Application.isEditor ? profile.saveSlot : profile.playerSaveSlot);
            State = Saves.Load(profile.legacySourceCopy) ?? new GameState
            {
                playthroughId = Guid.NewGuid().ToString("D"),
                player = new PlayerLocation
                {
                    position = startingPosition
                }
            };
            State.shipInstanceId = string.IsNullOrEmpty(State.shipInstanceId) ? State.playthroughId + ":ship-1" : State.shipInstanceId;
            bool fresh = State.ship == null;
            if (fresh)
            {
                State.inventory.Gold = profile.economy.initialGold;
                State.inventory.cargoProductId = profile.economy.productId;
                State.inventory.Food = profile.crew.initialFood;
                State.inventory.RepairTools = profile.crew.initialRepairTools;
            }

            if (profile.items == null || profile.items.Any(item => item == null) || profile.items.Select(item => item.id).Distinct().Count() != profile.items.Length || State.inventory.items.Any(entry => !profile.items.Any(item => item.id == entry.productId)))
                throw new InvalidOperationException("Item definition/registry mismatch");
            var shipDefinitions = (profile.shipCatalog ?? Array.Empty<ShipDefinition>()).Concat(profile.legacyShips ?? Array.Empty<ShipDefinition>()).Concat(new[] { profile.ship }).GroupBy(value => value.id).Select(group => group.First()).ToArray();
            var shipDefinition = State.ship == null || State.ship.definitionId == profile.ship.id ? profile.ship : Array.Find(shipDefinitions, definition => definition.id == State.ship.definitionId);
            var crewSettings = State.crew == null || State.crew.settingsId == profile.crew.id ? profile.crew : Array.Find(profile.legacyCrew ?? Array.Empty<CrewSettings>(), settings => settings.id == State.crew.settingsId);
            if (shipDefinition == null || crewSettings == null)
                throw new InvalidOperationException("Unknown legacy definition ID; original copy preserved");
            Ship = new ShipModel(shipDefinition, State.player);
            if (State.cellSize > 0 && !Mathf.Approximately(State.cellSize, Ship.CellSize))
                throw new InvalidOperationException("Cell size changed; explicit position migration is required");
            State.cellSize = Ship.CellSize;
            State.ship = Ship.Initialize(State.ship);
            var navigationDefinition = State.navigation == null || State.navigation.definitionId == profile.navigation.id ? profile.navigation : Array.Find(profile.legacyNavigation ?? Array.Empty<NavigationDefinition>(), definition => definition.id == State.navigation.definitionId);
            if (!navigationDefinition) throw new InvalidOperationException("Unknown saved world definition; original save preserved");
            Navigation = new NavigationRules(navigationDefinition, this, Ship);
            int startingPort = profile.fixedDevelopmentStart ? 0 : new Guid(State.playthroughId).ToByteArray()[0] % navigationDefinition.ports.Length;
            State.navigation = Navigation.Initialize(State.navigation, State.playthroughId, navigationDefinition.ports[startingPort].id);
            Ship.SetAboard(Navigation.IsAboard);
            if (!Navigation.ValidPlayerPosition(State.player.position))
                throw new InvalidOperationException("Player location cannot be restored");
            Journal = new JournalRules(this, Navigation);
            State.journal = Journal.Initialize(State.journal);
            Navigation.journal = Journal;
            Crew = new CrewBrain(crewSettings, this, Ship, Navigation, State.inventory);
            State.crew = Crew.Initialize(State.crew);
            Navigation.crew = Crew;
            foreach (var member in State.crew.members)
                State.Register(member);
            Economy = new EconomyRules(profile.economy, this, Navigation, Crew, State.inventory);
            State.economy = Economy.Initialize(State.economy);
            Rest = new RestSession(this, Navigation, Ship, Crew, Journal, State.player);
            State.sleep = Rest.Initialize(State.sleep);
            Loop = new SimulationLoop(this);
            Events = new EventRules(this, profile.events);
            Battle = new BattleRules(this, profile.battle, profile.items);
            Prisoners = new PrisonerRules(this);
            Shipyard = new ShipyardRules(this, profile.shipCatalog ?? new[] { profile.ship }, shipDefinitions);
            Life = new CrewLifeRules(this, crewSettings);
            Market = new MarketRules(this, profile.items, profile.market);
            Pests = new PestRules(this, profile.events);
            Detection = new DetectionRules(this, profile.events);
            Loop.EventBoundaryReached += Events.BeginScheduled;
            IsReady = true;
        }

        public bool CanDepart() => Shipyard.CanDepart() && Economy.CanDepart();
        public void MarkInteraction() => Journal.MarkInteraction();
        public bool RequestSave()
        {
            saveRequested = true;
            return ErrorMessage == null;
        }

        public bool FlushSave(bool force = false)
        {
            if (ErrorMessage != null && !force)
                return false;
            if (!IsReady || (!force && !saveRequested))
                return ErrorMessage == null;
            try
            {
                Crew.CapturePositions();
                if (Navigation.IsAboard)
                    Ship.RememberPosition();
                Saves.Save(State);
                saveRequested = false;
                ErrorMessage = null;
                ErrorOperation = null;
                errorPause?.Dispose();
                errorPause = null;
                return true;
            }
            catch (Exception error)
            {
                Fail(error, "저장");
                return false;
            }
        }

        public void Fail(Exception error, string operation = "게임 실행")
        {
            if (ErrorMessage == error.Message && ErrorOperation == operation)
                return;
            ErrorMessage = error.Message;
            ErrorOperation = operation;
            Debug.LogException(error);
            if (errorPause == null)
                errorPause = Pauses.Acquire("SaveError");
            Error?.Invoke(ErrorMessage);
        }

        public void NotifyChanged(bool arrived = false) => Changed?.Invoke(arrived);
        public void Dispose()
        {
            Crew?.Dispose();
            Economy?.Dispose();
            errorPause?.Dispose();
            IsReady = false;
        }
    }
}
