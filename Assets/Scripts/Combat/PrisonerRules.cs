using System;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class PrisonerRules
    {
        private readonly GameRuntime game;
        public PrisonerRules(GameRuntime game) => this.game = game;
        public int Capacity => game.Ship.Facilities.Where(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Prison).Sum(value => game.Ship.GetFacilityDefinition(value.definitionId).capacity * value.level);
        public PrisonerState[] Held => game.State.prisoners.Where(value => !value.handedOver && !value.executed && !value.pardoned).ToArray();
        public BattleActor[] Pending => game.Battle.State?.victory == true ? game.Battle.State.actors.Where(value => value.enemy && string.IsNullOrEmpty(game.Battle.Character(value.characterId).species) && game.Battle.Character(value.characterId).health > 0 && !game.State.prisoners.Any(prisoner => prisoner.characterId == value.characterId)).ToArray() : Array.Empty<BattleActor>();

        public bool DisposeTarget(string id, string decision)
        {
            if (!Pending.Any(value => value.characterId == id) || (decision != "capture" && decision != "execute" && decision != "pardon"))
                return false;
            if (decision == "capture" && Held.Length >= Capacity)
                return false;
            var member = game.Battle.Character(id);
            game.State.prisoners.Add(new PrisonerState
            {
                characterId = id,
                sourceEvent = game.State.events.current.id,
                executed = decision == "execute",
                pardoned = decision == "pardon"
            });
            if (decision != "pardon")
            {
                game.Economy.BreakContract(id);
                if (game.Crew.FindMember(id) != null) game.State.Register(game.Crew.RemoveMember(id));
            }
            member.availability = decision == "capture" ? CrewAvailability.Imprisoned : decision == "execute" ? CrewAvailability.Dead : CrewAvailability.Ready;
            if (decision == "execute")
                member.health = 0;
            game.RequestSave();
            return true;
        }

        public bool Handover(string id)
        {
            if (!game.Navigation.IsDocked || game.Navigation.IsAboard || game.State.player.areaId != "town")
                return false;
            var prisoner = Held.FirstOrDefault(value => value.characterId == id);
            if (prisoner == null || (long)game.State.inventory.Gold + game.Battle.Settings.handoverReward > int.MaxValue)
                return false;
            prisoner.handedOver = true;
            game.State.inventory.Gold += game.Battle.Settings.handoverReward;
            game.RequestSave();
            return true;
        }

        public void Advance()
        {
            int day = (int)(game.ElapsedSeconds / GameRuntime.SecondsPerDay) + 1;
            if (game.State.prisonersFedDay == 0)
                game.State.prisonersFedDay = day;
            while (game.State.prisonersFedDay < day)
            {
                foreach (var prisoner in Held)
                    if (game.State.inventory.HasMeal)
                        game.State.inventory.ConsumeMeal();
                    else
                    {
                        var character = game.Battle.Character(prisoner.characterId);
                        character.health = Mathf.Max(1, character.health - 5);
                    }
                game.State.prisonersFedDay++;
                game.RequestSave();
            }
        }
    }
}
