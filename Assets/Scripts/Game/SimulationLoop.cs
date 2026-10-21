using System;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class SimulationLoop
    {
        private readonly GameRuntime game;
        public double EventBoundary { get; private set; } = double.PositiveInfinity;

        public event Action EventBoundaryReached;
        public SimulationLoop(GameRuntime game) => this.game = game;
        public void ScheduleEventBoundary(double time)
        {
            if (!double.IsNaN(time) && !double.IsInfinity(time) && time >= game.ElapsedSeconds)
                EventBoundary = time;
        }

        public void Advance(float realSeconds)
        {
            if (!game.IsReady || game.IsPaused)
                return;
            game.Events.PrepareBoundary();
            float budget = Mathf.Min(realSeconds, .25f) * game.Rest.TimeScale;
            bool fast = game.IsSleeping;
            while (budget > .000001f && !game.IsPaused)
            {
                float step = game.Rest.LimitStep(Mathf.Min(budget, .05f));
                step = (float)Math.Min(step, Math.Max(0, EventBoundary - game.ElapsedSeconds));
                if (step <= .000001f)
                {
                    if (EventBoundary <= game.ElapsedSeconds + .00001)
                    {
                        EventBoundary = double.PositiveInfinity;
                        EventBoundaryReached?.Invoke();
                        if (game.State.events.current?.kind != "watch") game.Rest.Wake();
                    }
                    else
                        game.Rest.Advance(0);
                    break;
                }

                var from = game.Navigation.WorldPosition;
                bool moving = !game.Navigation.IsDocked;
                float consumed = game.Battle.Running ? step : game.Navigation.AdvanceTime(step);
                game.State.elapsedSeconds += consumed;
                game.Crew.Advance(consumed, game.ElapsedSeconds);
                game.Life.Advance(consumed);
                if (fast)
                    game.Crew.AdvanceSleepMovement(consumed);
                int day = (int)(game.ElapsedSeconds / GameRuntime.SecondsPerDay) + 1;
                if (moving)
                    game.Journal.ObserveMovement(from, game.Navigation.WorldPosition, game.Rest.RecordDay(day), game.State.sleep.mode != PlayerRest.Awake);
                bool arrived = moving && game.Navigation.IsDocked;
                if (arrived)
                {
                    game.Journal.Arrive(game.Navigation.DockedPort);
                    game.Rest.Wake();
                }
                else
                    game.Rest.Advance(consumed);
                game.Events.Advance(consumed);
                game.Prisoners.Advance();
                game.Market.Advance();
                game.Navigation.NotifyArrival();
                game.NotifyChanged(arrived);
                if (arrived)
                    game.RequestSave();
                budget -= consumed;
                if (arrived || (fast && !game.IsSleeping) || consumed <= 0)
                    break;
            }
        }
    }
}
