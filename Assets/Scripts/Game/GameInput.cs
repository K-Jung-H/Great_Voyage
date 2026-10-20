using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreatVoyage
{
    // PC bindings share one action boundary; later input devices feed these same actions.
    public sealed class GameInput : IDisposable
    {
        private readonly InputActionMap actions = new InputActionMap("Game");
        private readonly InputAction move, interact, cancel;

        public Vector2 Move => Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool CancelPressed => cancel.WasPressedThisFrame();

        public GameInput()
        {
            move = actions.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            interact = actions.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            cancel = actions.AddAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
        }

        public void Enable() => actions.Enable();
        public void Disable() => actions.Disable();
        public void Dispose() => actions.Dispose();
    }
}
