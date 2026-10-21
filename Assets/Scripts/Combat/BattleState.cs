using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class BattleActor
    {
        public string characterId;
        public bool enemy;
        public bool surrendered;
        public bool visitedArmory;
        public float cooldown;
        public string floorId;
        public Vector2 position;
    }

    [Serializable]
    public sealed class BattleProjectile
    {
        public string sourceId;
        public string targetId;
        public Vector2 origin;
        public Vector2 position;
        public float damage;
    }

    [Serializable]
    public sealed class BattleState
    {
        public List<BattleActor> actors = new List<BattleActor>();
        public List<BattleProjectile> projectiles = new List<BattleProjectile>();
        public bool finished;
        public bool victory;
        public bool lootApplied;
        public float elapsed;
    }

    [Serializable]
    public sealed class PrisonerState
    {
        public string characterId;
        public string sourceEvent;
        public bool handedOver;
        public bool executed;
        public bool pardoned;
    }
}
