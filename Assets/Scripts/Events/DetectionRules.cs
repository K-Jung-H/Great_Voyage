using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class DetectionRules
    {
        private readonly GameRuntime game;
        private readonly EventSettings settings;
        public DetectionRules(GameRuntime game, EventSettings settings) { this.game = game; this.settings = settings; }
        public bool HasLookout => game.State.crew.members.Any(value => value.activity == CrewActivity.Lookingout && value.availability == CrewAvailability.Ready);
        public bool HasTarget => game.State.events.nextKind == "pirates" || game.State.events.nextKind == "island";
        public string Observe()
        {
            var state = game.State.events;
            float distance = Vector2.Distance(game.Navigation.WorldPosition, state.nextPosition);
            var observer = game.State.crew.members.FirstOrDefault(value => value.activity == CrewActivity.Lookingout && value.availability == CrewAvailability.Ready);
            float range = settings.detectionRange * (observer == null ? 1 : game.Life.Performance(observer, CrewDuty.Lookout));
            float accuracy = .8f + (observer?.specialty == CrewSpecialty.Lookout ? .15f : 0) - (state.current?.kind == "storm" && game.Events.Running ? .4f : 0);
            uint seed = state.nextSeed == 0 ? 1 : state.nextSeed;
            if (!HasTarget || distance > range || EventRules.Next(ref seed) > accuracy) return "빈 바다 · 관측 정확도 " + accuracy.ToString("P0");
            return EventRules.TitleFor(state.nextKind) + " 징후 · 거리 " + distance.ToString("0") + " · 정확도 " + accuracy.ToString("P0");
        }
    }
}
