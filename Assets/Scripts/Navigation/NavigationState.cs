using System;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class NavigationState
    {
        public string definitionId;
        public Vector2 position;
        public float heading;
        public float speed;
        public string dockedPort;
        public string departurePort;
        public bool departureCleared;
        public bool aboard = true;
        public Vector2 boardingPosition;
        public int voyageSequence;
        public int completedSequence;
        public double voyageStartedAt = -1;
    }
}
