using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class BattleRules
    {
        private readonly GameRuntime game;
        private readonly BattleSettings settings;
        private readonly ItemDefinition[] items;
        public BattleState State => game.State.events.current?.battle;
        public bool Running => State != null && !State.finished && game.State.events.current.phase == EventPhase.Running;
        public BattleSettings Settings => settings;
        public CrewState Character(string id) => game.State.characters.Find(value => value.id == id);
        public bool IsCrew(string id) => game.Crew.FindMember(id) != null;
        public BattleRules(GameRuntime game, BattleSettings settings, ItemDefinition[] items)
        {
            this.game = game;
            this.settings = settings;
            this.items = items;
            if (!settings || game.Ship.GetFloorDefinition(settings.floorId) == null || settings.pirateCount < 1 || settings.pirateCount > 32 || settings.pirateHealth <= 0 || settings.movementSpeed <= 0 || settings.projectileSpeed <= 0 || settings.cooldowns.x <= 0 || settings.cooldowns.y <= 0 || settings.cooldowns.z <= 0 || settings.handoverReward < 0)
                throw new InvalidDataException("Invalid battle settings");
        }

        public void Begin()
        {
            var encounter = game.State.events.current;
            if (encounter.battle != null)
                return;
            encounter.battle = new BattleState();
            foreach (var member in game.State.crew.members.Where(value => value.health > 0 && value.availability == CrewAvailability.Ready).ToArray())
            {
                game.Crew.SetAvailability(member.id, CrewAvailability.Combat);
                State.actors.Add(new BattleActor { characterId = member.id, enemy = encounter.kind == "mutiny" && member.trust <= 10, floorId = member.floorId, position = member.position });
            }
            if (encounter.kind == "mutiny")
            {
                float initialDamage = game.Ship.Facilities.Where(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Cannon).Sum(value => value.level) * settings.cannonDamage;
                foreach (var actor in State.actors.Where(value => value.enemy)) Damage(actor, initialDamage);
                return;
            }
            var cells = FreeCells();
            if (cells.Length == 0)
                throw new InvalidDataException("No walkable battle deployment cell");
            float cannonDamage = game.Ship.Facilities.Where(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Cannon).Sum(value => value.level) * settings.cannonDamage;
            bool kraken = encounter.kind == "kraken";
            for (int i = 0; i < (kraken ? 1 : settings.pirateCount); i++)
            {
                var position = cells[cells.Length - 1 - i % cells.Length];
                var enemy = new CrewState
                {
                    id = encounter.id + ":pirate:" + i,
                    displayName = kraken ? "크라켄" : "해적 " + (i + 1),
                    species = kraken ? "kraken" : null,
                    health = Mathf.Max(0, (kraken ? settings.krakenHealth : settings.pirateHealth) - cannonDamage),
                    strength = settings.pirateStrength * (kraken ? 2 : 1),
                    floorId = settings.floorId,
                    position = position,
                    availability = CrewAvailability.Combat,
                    weapon = i % 2 == 0 ? WeaponKind.Sword : WeaponKind.Gun,
                    armed = true
                };
                game.State.Register(enemy);
                State.actors.Add(new BattleActor { characterId = enemy.id, enemy = true, visitedArmory = true, floorId = enemy.floorId, position = position });
            }
        }

        private Vector2[] FreeCells()
        {
            var floor = game.Ship.GetFloorDefinition(settings.floorId);
            return Enumerable.Range(0, floor.size.x * floor.size.y)
                .Select(index => (floor.origin + new Vector2(index % floor.size.x + .5f, index / floor.size.x + .5f)) * game.Ship.CellSize)
                .Where(position => game.Ship.HasCrewClearance(settings.floorId, position, .28f)).ToArray();
        }

        public void Advance(float seconds)
        {
            if (!Running)
                return;
            State.elapsed += seconds;
            foreach (var actor in State.actors)
            {
                var character = Character(actor.characterId);
                actor.position = character.position;
                actor.floorId = character.floorId;
                if (!Alive(actor))
                    continue;
                if (IsCrew(actor.characterId) && !PrepareCrew(actor, character))
                    continue;
                var target = State.actors.Where(value => value.enemy != actor.enemy && Alive(value))
                    .OrderBy(value => value.floorId == actor.floorId ? Vector2.SqrMagnitude(value.position - actor.position) : float.MaxValue)
                    .ThenBy(value => value.characterId, StringComparer.Ordinal).FirstOrDefault();
                if (target == null)
                    continue;
                var weapon = character.armed ? character.weapon : WeaponKind.Unarmed;
                float range = Range(weapon) * game.Ship.CellSize;
                bool inRange = target.floorId == actor.floorId && Vector2.Distance(actor.position, target.position) <= range;
                actor.cooldown = Mathf.Max(0, actor.cooldown - seconds);
                if (!inRange)
                    Move(actor, target, seconds);
                else if (actor.cooldown <= 0)
                {
                    if (!actor.enemy && character.activity == CrewActivity.EncounterMoving)
                        game.Crew.MoveForEncounter(character.id, character.floorId, character.position);
                    Attack(actor, target, weapon);
                    actor.cooldown = Cooldown(weapon);
                }
            }
            foreach (var projectile in State.projectiles.ToArray())
            {
                var target = State.actors.Find(value => value.characterId == projectile.targetId);
                if (target == null || !Alive(target))
                {
                    State.projectiles.Remove(projectile);
                    continue;
                }
                projectile.position = Vector2.MoveTowards(projectile.position, target.position, settings.projectileSpeed * game.Ship.CellSize * seconds);
                if (Vector2.Distance(projectile.position, target.position) <= .05f * game.Ship.CellSize)
                {
                    Damage(target, projectile.damage);
                    State.projectiles.Remove(projectile);
                }
            }
            if (!State.actors.Any(value => !value.enemy && Alive(value)) || !State.actors.Any(value => value.enemy && Alive(value)))
                Finish();
        }

        private bool PrepareCrew(BattleActor actor, CrewState character)
        {
            if (actor.visitedArmory || character.armed)
                return true;
            var armory = game.Ship.Facilities.FirstOrDefault(value => value.durability > 0 && game.Ship.GetFacilityDefinition(value.definitionId).role == FacilityRole.Armory);
            if (armory != null && game.Ship.TryGetAccessCell(armory.id, out Vector2 access))
            {
                if (character.floorId != armory.floorId || Vector2.Distance(character.position, access) > .35f * game.Ship.CellSize)
                {
                    if (character.activity != CrewActivity.EncounterMoving && !game.Crew.MoveForEncounter(character.id, armory.floorId, access))
                        actor.visitedArmory = true;
                    return actor.visitedArmory;
                }
                EquipInternal(character.id, WeaponKind.Sword);
                if (!character.armed)
                    EquipInternal(character.id, WeaponKind.Gun);
            }
            actor.visitedArmory = true;
            return true;
        }

        public bool Equip(string id, WeaponKind weapon)
        {
            return !Running && EquipInternal(id, weapon);
        }

        private bool EquipInternal(string id, WeaponKind weapon)
        {
            var member = game.Crew.FindMember(id);
            if (member == null || !Enum.IsDefined(typeof(WeaponKind), weapon) || (member.availability != CrewAvailability.Ready && member.availability != CrewAvailability.Combat))
                return false;
            if (weapon != WeaponKind.Unarmed)
            {
                string product = weapon == WeaponKind.Sword ? "sword" : "gun";
                int assigned = game.State.crew.members.Count(value => value.id != id && value.armed && value.weapon == weapon);
                if (game.State.inventory.Get(product) <= assigned)
                    return false;
            }
            member.weapon = weapon;
            member.armed = weapon != WeaponKind.Unarmed;
            game.RequestSave();
            return true;
        }

        private void Move(BattleActor actor, BattleActor target, float seconds)
        {
            var character = Character(actor.characterId);
            if (IsCrew(actor.characterId))
            {
                if (character.activity != CrewActivity.EncounterMoving || character.destination == null || character.destination.floorId != target.floorId || Vector2.Distance(character.destination.position, target.position) > game.Ship.CellSize)
                    game.Crew.MoveForEncounter(character.id, target.floorId, target.position);
                return;
            }
            if (actor.floorId != target.floorId || !game.Ship.TryFindPath(actor.floorId, actor.position, target.position, out var path))
                return;
            var next = path.FirstOrDefault(position => Vector2.Distance(position, character.position) > .05f * game.Ship.CellSize);
            if (path.Count > 0)
                character.position = Vector2.MoveTowards(character.position, next, settings.movementSpeed * game.Ship.CellSize * seconds);
        }

        private void Attack(BattleActor source, BattleActor target, WeaponKind weapon)
        {
            var member = Character(source.characterId);
            float maximum = member.species == "kraken" ? settings.krakenHealth : source.enemy && !IsCrew(source.characterId) ? settings.pirateHealth : 100;
            float damage = Mathf.Max(1, member.strength * .6f + Mathf.Min(member.navigationExperience, 50) * .2f) * (.25f + .75f * Mathf.Clamp01(member.health / maximum)) * (weapon == WeaponKind.Gun ? 1.8f : 1);
            if (weapon == WeaponKind.Gun)
                State.projectiles.Add(new BattleProjectile { sourceId = source.characterId, targetId = target.characterId, origin = source.position, position = source.position, damage = damage });
            else if (weapon == WeaponKind.Sword)
                foreach (var victim in State.actors.Where(value => value.enemy != source.enemy && Alive(value) && value.floorId == source.floorId && Vector2.Distance(value.position, source.position) <= Range(weapon) * game.Ship.CellSize).ToArray())
                    Damage(victim, damage);
            else
                Damage(target, damage);
        }

        private void Damage(BattleActor actor, float damage)
        {
            var member = Character(actor.characterId);
            member.health = Mathf.Max(0, member.health - damage);
            if (member.health == 0)
            {
                member.availability = CrewAvailability.Dead;
                if (IsCrew(actor.characterId))
                {
                    game.Crew.SetAvailability(member.id, CrewAvailability.Dead);
                    game.Economy.BreakContract(member.id);
                }
            }
            else if (actor.enemy && member.health <= settings.surrenderHealth)
            {
                actor.surrendered = true;
                if (IsCrew(actor.characterId)) game.Crew.SetAvailability(actor.characterId, CrewAvailability.Combat);
            }
        }

        private bool Alive(BattleActor actor) => Character(actor.characterId).health > 0 && !actor.surrendered;
        public float Cooldown(WeaponKind kind) => kind == WeaponKind.Gun ? settings.cooldowns.z : kind == WeaponKind.Sword ? settings.cooldowns.y : settings.cooldowns.x;
        private float Range(WeaponKind kind) => kind == WeaponKind.Gun ? settings.ranges.z : kind == WeaponKind.Sword ? settings.ranges.y : settings.ranges.x;

        private void Finish()
        {
            if (State.finished)
                return;
            State.finished = true;
            State.victory = State.actors.Any(value => !value.enemy && Alive(value));
            State.projectiles.Clear();
            foreach (var actor in State.actors.Where(value => IsCrew(value.characterId)))
                if (Character(actor.characterId).health > 0)
                    game.Crew.SetAvailability(actor.characterId, CrewAvailability.Ready);
            if (!State.victory && !State.lootApplied)
            {
                uint seed = game.State.events.current.randomState;
                float proportion = .1f + EventRules.Next(ref seed) * .6f;
                var cargo = game.State.inventory.items.Where(value => value.ownerId == "player" && value.quantity > 0 && items.Any(item => item.id == value.productId && item.use == ItemUse.TradeGoods)).ToArray();
                if (cargo.Length > 0)
                    foreach (var item in cargo)
                        item.quantity -= Mathf.Clamp(Mathf.RoundToInt(item.quantity * proportion), 1, item.quantity);
                else
                    game.State.inventory.Gold -= Mathf.RoundToInt(game.State.inventory.Gold * (.3f + EventRules.Next(ref seed) * .4f));
                game.State.events.current.randomState = seed;
                State.lootApplied = true;
            }
            game.Events.Resolve(State.victory ? game.Events.Title + " 전투에서 승리했습니다." : game.Events.Title + " 전투에서 패배하여 약탈당했습니다.");
        }
    }
}
