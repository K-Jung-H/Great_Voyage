using System;

namespace GreatVoyage
{
    // S1 coordinates presentation and input only. Time, persistent state and saves join in S2.
    public sealed class GameFlow : IDisposable
    {
        private readonly GameInput input;
        private readonly PlayerMovement player;
        private readonly InteractionController interactions;
        private readonly UIRoot ui;
        private bool active;

        public GameFlow(GameInput input, PlayerMovement player, InteractionController interactions, UIRoot ui)
        {
            this.input = input;
            this.player = player;
            this.interactions = interactions;
            this.ui = ui;
            ui.CloseRequested += CloseJournal;
        }

        public void Activate()
        {
            ui.Initialize();
            active = true;
            player.SetInputBlocked(false);
            input.Enable();
        }

        public void Tick()
        {
            if (!active) return;
            if (ui.BlocksWorldInput)
            {
                if (input.CancelPressed) CloseJournal();
                return;
            }
            IInteractable target = interactions.FindTarget();
            ui.ShowPrompt(target?.Prompt);
            if (input.InteractPressed && target != null) target.Interact();
        }

        public void OpenJournal()
        {
            if (!active || ui.BlocksWorldInput) return;
            player.SetInputBlocked(true);
            ui.SetJournalVisible(true);
        }

        private void CloseJournal()
        {
            if (!active) return;
            ui.SetJournalVisible(false);
            player.SetInputBlocked(false);
        }

        public void Suspend()
        {
            if (!active) return;
            active = false;
            input.Disable();
            if (player != null) player.SetInputBlocked(true);
            if (ui != null) { ui.SetJournalVisible(false); ui.ShowPrompt(null); }
        }

        public void Dispose()
        {
            Suspend();
            if (ui != null) ui.CloseRequested -= CloseJournal;
        }
    }
}
