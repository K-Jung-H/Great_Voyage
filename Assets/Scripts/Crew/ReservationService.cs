using System.Collections.Generic;

namespace GreatVoyage
{
    public sealed class ReservationService
    {
        private readonly Dictionary<string, string> owners = new Dictionary<string, string>();
        public string this[string id] { get => owners[id]; set => owners[id] = value; }

        public bool ContainsKey(string id) => owners.ContainsKey(id);
        public bool TryGetValue(string id, out string owner) => owners.TryGetValue(id, out owner);
        public void Add(string id, string owner) => owners.Add(id, owner);
        public bool Remove(string id) => owners.Remove(id);
    }

    public interface ICrewBody
    {
        bool IsSimulated { get; }

        void SynchronizeVisibility(bool show);
        void CaptureState();
        void TeleportToState();
        void ApplyVelocity(UnityEngine.Vector2 velocity);
        void DriveVelocity(UnityEngine.Vector2 velocity, float acceleration, float mass);
        float ObstacleDistance(UnityEngine.Vector2 origin, UnityEngine.Vector2 direction, float radius, float lookAhead);
    }

    internal sealed class LogicalCrewBody : ICrewBody
    {
        public bool IsSimulated => false;

        public void SynchronizeVisibility(bool show)
        {
        }

        public void CaptureState()
        {
        }

        public void TeleportToState()
        {
        }

        public void ApplyVelocity(UnityEngine.Vector2 velocity)
        {
        }

        public void DriveVelocity(UnityEngine.Vector2 velocity, float acceleration, float mass)
        {
        }

        public float ObstacleDistance(UnityEngine.Vector2 origin, UnityEngine.Vector2 direction, float radius, float distance) => distance;
    }
}
