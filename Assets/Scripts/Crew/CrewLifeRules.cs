using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class CrewLifeRules
    {
        private readonly GameRuntime game;
        private readonly CrewSettings settings;
        public CrewLifeRules(GameRuntime game, CrewSettings settings)
        {
            this.game = game; this.settings = settings;
            foreach (var member in game.State.characters)
            {
                member.career ??= new System.Collections.Generic.List<string>();
                member.wish ??= new CrewWish();
            }
        }

        public float Performance(CrewState member, CrewDuty duty)
        {
            float stat = duty == CrewDuty.Maintenance ? member.dexterity : duty == CrewDuty.Navigation || duty == CrewDuty.Lookout ? member.intelligence : member.strength;
            return Mathf.Max(.25f, (.25f + .75f * member.health / 100) * (.7f + stat * settings.jobStatWeight + Mathf.Min(50, member.navigationExperience) * .01f) * (.7f + .3f * member.happiness / 100));
        }

        public void Advance(float seconds)
        {
            bool toilet = game.Ship.Facilities.Any(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Toilet);
            foreach (var member in game.State.crew.members.Where(value => value.availability == CrewAvailability.Ready))
            {
                if (game.Navigation.IsDocked) member.happiness = Mathf.Min(100, member.happiness + settings.portHappinessRecovery * seconds);
                else if (!toilet) member.happiness = Mathf.Max(0, member.happiness - settings.portHappinessRecovery * .1f * seconds);
                if (member.promiseChecked && member.acceptedPromise != null && member.acceptedPromise.kind != CrewWishKind.Destination && !Fulfilled(member, member.acceptedPromise, game.Navigation.DockedPort)) member.promiseBreached = true;
                if (member.promiseBreached) member.trust = Mathf.Max(0, member.trust - settings.brokenPromiseTrustLoss * seconds);
                else if (member.happiness < 30) member.trust = Mathf.Max(0, member.trust - settings.unhappyTrustLoss * seconds);
                else if (member.happiness > 80) member.trust = Mathf.Min(100, member.trust + settings.trustRecovery * seconds);
            }
        }

        public void CompleteVoyage(CrewState member, string port, int sequence)
        {
            member.career ??= new System.Collections.Generic.List<string>();
            member.career.Add("항해 " + sequence + " · " + port);
            member.navigationExperience++;
            member.rank = member.navigationExperience >= 10 ? "숙련 선원" : "선원";
            if (!member.promiseChecked && member.acceptedPromise != null)
            {
                member.promiseBreached = !Fulfilled(member, member.acceptedPromise, port);
                member.promiseChecked = true;
            }
        }

        private bool Fulfilled(CrewState member, CrewWish promise, string port)
        {
            return promise.kind == CrewWishKind.None || (promise.kind == CrewWishKind.Duty && member.primaryDuty == promise.duty) || (promise.kind == CrewWishKind.Destination && promise.targetId == port) || (promise.kind == CrewWishKind.Facility && game.Ship.Facilities.Any(value => value.definitionId == promise.targetId && value.durability > 0));
        }
    }
}
