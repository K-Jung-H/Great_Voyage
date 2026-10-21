using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    public enum CrewSpecialty { None, Maintenance, Navigation, Lookout, Guard }
    public enum CrewDuty { None, Maintenance, Navigation, Lookout, Guard }
    public enum CrewWishKind { None, Duty, Destination, Facility }
    [Serializable]
    public sealed class CrewWish
    {
        public CrewWishKind kind;
        public CrewDuty duty;
        public string targetId;
    }
    public enum CrewAvailability { Ready, Dead, Imprisoned, Combat, Away }
    public enum CrewActivity { Idle, GoingToRepair, Repairing, GoingToBed, Sleeping, Eating, Wandering, GoingToHelm, Helming, EncounterMoving, EncounterReady, GoingToPest, FightingPest, GoingToLookout, Lookingout }
    public enum WeaponKind { Unarmed, Sword, Gun }

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
        public float strength = 10;
        public float dexterity = 10;
        public float intelligence = 10;
        public float trust = 100;
        public WeaponKind weapon;
        public bool armed;
        public string rank = "선원";
        public List<string> career = new List<string>();
        public CrewWish wish = new CrewWish();
        public CrewWish acceptedPromise;
        public bool promiseBreached;
        public bool promiseChecked;
        public string species;
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
