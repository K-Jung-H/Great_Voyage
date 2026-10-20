using UnityEngine;

namespace GreatVoyage
{
    public interface IInteractable
    {
        Transform InteractionRoot { get; }
        Vector2 InteractionPosition { get; }
        string Prompt { get; }
        bool Available { get; }
        void Interact();
    }
}
