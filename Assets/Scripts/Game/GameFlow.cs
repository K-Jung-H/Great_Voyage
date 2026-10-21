using System;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class GameFlow : IDisposable
    {
        private readonly GameInput input;
        private readonly PlayerMovement player;
        private readonly InteractionController interactions;
        private readonly UIRoot ui;
        private readonly GameRuntime game;
        private readonly WorldView world;
        private readonly ScreenStack stack;
        private readonly EconomyScreens economy;
        private readonly JournalScreenController journal;
        private readonly SessionScreenController session;
        private readonly ContextPresenter context;
        private readonly BattleScreens battle;
        private readonly ShipyardScreens shipyard;
        private bool active;
        public GameFlow(GameInput input, PlayerMovement player, InteractionController interactions, UIRoot ui, GameRuntime game, WorldView world, ScreenStack stack, EconomyScreens economy, JournalScreenController journal, SessionScreenController session, ContextPresenter context, BattleScreens battle, ShipyardScreens shipyard)
        {
            this.input = input;
            this.player = player;
            this.interactions = interactions;
            this.ui = ui;
            this.game = game;
            this.world = world;
            this.stack = stack;
            this.economy = economy;
            this.journal = journal;
            this.session = session;
            this.context = context;
            this.battle = battle;
            this.shipyard = shipyard;
            economy.SettlementConfirmed += AfterSettlement;
        }

        public void Activate()
        {
            active = true;
            input.Enable();
        }

        public void Tick()
        {
            if (!active || !game.IsReady)
                return;
            if (context.IsTransitioning)
            {
                player.SetInputBlocked(true);
                ui.ShowPrompt(null);
                return;
            }

            player.SetInputBlocked(game.IsPaused || game.IsSleeping || game.Navigation.IsSteering || game.Battle.Running);
            if (stack.IsOpen)
            {
                ui.ShowPrompt(null);
                if (input.CancelPressed)
                    stack.CloseTop();
                return;
            }

            if (game.IsPaused)
                return;
            if (game.Battle.Running)
            {
                ui.ShowPrompt("관전 전투 · ESC 메뉴");
                if (input.CancelPressed) session.OpenMenu();
                return;
            }
            if (game.Navigation.IsSteering)
            {
                ui.ShowPrompt("조타: WASD · E/ESC 종료");
                if (input.CancelPressed || input.InteractPressed)
                    game.Navigation.EndSteering();
                else
                    game.Navigation.Steer(input.Move, Mathf.Min(Time.unscaledDeltaTime, .25f));
                return;
            }

            if (input.CancelPressed)
            {
                session.OpenMenu();
                return;
            }

            if (game.IsSleeping)
            {
                ui.ShowPrompt(null);
                return;
            }

            var target = interactions.FindTarget();
            ui.ShowPrompt(target?.Prompt);
            if (input.InteractPressed && target != null)
            {
                target.Interact();
                game.MarkInteraction();
                game.RequestSave();
            }
        }

        public void OpenJournal()
        {
            if (!active || game.IsPaused || game.IsSleeping)
                return;
            game.MarkInteraction();
            journal.Open();
            player.SetInputBlocked(true);
        }

        public bool Available(WorldInteraction item)
        {
            if (!active || !game.IsReady || game.IsPaused || game.IsSleeping)
                return false;
            if (item.Action == WorldAction.DeferredService)
                return false;
            if (item.Action == WorldAction.Helm)
                return game.Navigation.HelmOperational;
            if (item.Action == WorldAction.Gangway || item.Action == WorldAction.BoardShip)
                return game.Navigation.IsDocked;
            if (item.Action == WorldAction.Bed)
                return game.Rest.CanUseBed(item.TargetId);
            return true;
        }

        public void Interact(WorldInteraction item)
        {
            world.CapturePlayer();
            if (item.Action != WorldAction.Bed)
                game.MarkInteraction();
            switch (item.Action)
            {
                case WorldAction.Stair:
                    if (game.Ship.Travel(item.TargetId, item.Destination * game.Ship.CellSize))
                    {
                        world.ApplySpace(true);
                        game.RequestSave();
                    }

                    break;
                case WorldAction.Helm:
                    game.Navigation.BeginSteering();
                    break;
                case WorldAction.Gangway:
                    game.State.inventory.LandFreshFish();
                    game.Navigation.SetAboard(false);
                    world.MoveArea("dock", item.Destination);
                    break;
                case WorldAction.BoardShip:
                    game.Navigation.SetAboard(true);
                    game.State.player.areaId = "aboard";
                    game.State.player.position = game.Navigation.State.boardingPosition;
                    world.ApplySpace(true);
                    game.RequestSave();
                    break;
                case WorldAction.Portal:
                    world.MoveArea(item.TargetId, item.Destination);
                    break;
                case WorldAction.Merchant:
                    economy.OpenMerchant(item.TargetId);
                    break;
                case WorldAction.Broker:
                    economy.Open("BrokerScreen");
                    break;
                case WorldAction.Tavern:
                    economy.Open("TavernServiceScreen");
                    break;
                case WorldAction.Contracts:
                    economy.Open("ContractsScreen");
                    break;
                case WorldAction.Bed:
                    game.Rest.UseBed(item.TargetId);
                    break;
                case WorldAction.Armory:
                    battle.OpenArmory();
                    break;
                case WorldAction.TownHall:
                    battle.OpenTownHall();
                    break;
                case WorldAction.Shipyard:
                    shipyard.Open();
                    break;
                case WorldAction.Sail:
                    var sail = game.Ship.GetFacility(item.TargetId);
                    if (sail != null) { sail.sailDeployed = !sail.sailDeployed; game.RequestSave(); }
                    break;
                case WorldAction.Telescope:
                    context.ObserveSea();
                    break;
            }

            player.SetInputBlocked(game.IsPaused || game.IsSleeping || game.Navigation.IsSteering);
        }

        private void AfterSettlement()
        {
            if (game.Economy.State.gameOver)
                session.ShowGameOver();
            else
                game.Navigation.RequestDeparture();
        }

        public void Suspend()
        {
            active = false;
            input.Disable();
            if (player)
                player.SetInputBlocked(true);
            if (ui)
                ui.ShowPrompt(null);
        }

        public void Dispose()
        {
            Suspend();
            economy.SettlementConfirmed -= AfterSettlement;
        }
    }
}
