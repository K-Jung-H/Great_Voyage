using System;

namespace GreatVoyage
{
    public enum EventPhase { Announced, Running, Resolved }

    [Serializable]
    public sealed class VoyageEventState
    {
        public string id;
        public string kind = "storm";
        public EventPhase phase;
        public uint initialSeed;
        public uint randomState;
        public double startedAt;
        public double progressedSeconds;
        public string choice;
        public string checkpoint;
        public string result;
        public bool effectsApplied;
        public bool resultAcknowledged;
        public BattleState battle;
        public string dispatchedCrewId;
        public System.Collections.Generic.List<PestState> pests;
    }

    [Serializable]
    public sealed class EventState
    {
        public int sequence;
        public double nextAt = -1;
        public VoyageEventState current;
        public string nextKind;
        public uint nextSeed;
        public UnityEngine.Vector2 nextPosition;
        public float watchFailureBonus;
        public bool mutinyLatched;
    }

    [Serializable]
    public sealed class PestState
    {
        public string id, floorId, targetId;
        public UnityEngine.Vector2 position;
        public float health = 20;
    }
}
