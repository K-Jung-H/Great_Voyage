using System;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable] public sealed class JournalPoint
    {
        public Vector2 position;
        public int day;
        public bool carry;
        public bool breakBefore;
    }
    [Serializable] public sealed class JournalEvent
    {
        public string id;
        public string title;
        public Vector2 position;
        public int day;
    }
    [Serializable] public sealed class JournalNote
    {
        public Vector2 position;
        public string text;
    }
    [Serializable] public sealed class JournalVoyage
    {
        public int sequence;
        public string departurePort;
        public string arrivalPort;
        public List<Vector2> plan = new List<Vector2>();
        public List<JournalPoint> recorded = new List<JournalPoint>();
        public List<JournalEvent> events = new List<JournalEvent>();
        public List<JournalNote> notes = new List<JournalNote>();
    }
    [Serializable] public sealed class JournalState
    {
        public List<JournalVoyage> voyages = new List<JournalVoyage>();
        public List<Vector2> plan = new List<Vector2>();
        public List<JournalPoint> pending = new List<JournalPoint>();
        public List<JournalEvent> pendingEvents = new List<JournalEvent>();
        public bool carryEligible;
        public bool hasRecorded;
        public bool gap = true;
        public bool autopilot;
        public int planIndex;
    }
    public enum PlayerRest { Awake, Sleeping, Watch }
    [Serializable] public sealed class PlayerSleepState
    {
        public PlayerRest mode;
        public string bedId;
        public double wakeAt;
        public int recordDay;
        public float watchRemaining;
        public float patrolRemaining;
        public bool missedWatch;
    }
}
