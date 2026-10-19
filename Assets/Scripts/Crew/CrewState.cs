using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    public enum CrewSpecialty { None, Maintenance, Navigation }
    public enum CrewDuty { None, Maintenance, Navigation }
    public enum CrewAvailability { Ready, Dead, Imprisoned, Combat }
    public enum CrewActivity { Idle, GoingToRepair, Repairing, GoingToBed, Sleeping, Eating, Wandering, GoingToHelm, Helming }

    [Serializable]
    public sealed class CrewWaypoint
    {
        public string floorId;
        public Vector2 position;
    }

    [Serializable]
    public sealed class CrewState
    {
        public string id;
        public string displayName;
        public CrewSpecialty specialty;
        public CrewDuty primaryDuty;
        public int navigationExperience;
        public CrewAvailability availability;
        public string floorId;
        public Vector2 position;
        public Vector2 velocity;
        public float repathRemaining;
        public float health = 100;
        public float happiness = 100;
        public float hunger;
        public float energy = 100;
        public CrewActivity activity;
        public string targetId;
        public float remainingSeconds;
        public CrewWaypoint destination;
        public CrewWaypoint nextWaypoint;
        public bool hasWorkPosition;
        public Vector2 workPosition;
        public int wanderSequence;
        [NonSerialized] public List<CrewWaypoint> path = new List<CrewWaypoint>();
        [NonSerialized] public bool resumeWaypoint;
    }

    [Serializable]
    public sealed class CrewRosterState
    {
        public string settingsId;
        public int food;
        public int repairTools;
        public double remainder;
        public List<CrewState> members = new List<CrewState>();
    }
}
