using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GreatVoyage
{
    public sealed class ShipEditPointer : MonoBehaviour, IPointerClickHandler
    {
        public event Action<Vector2> Selected;
        public void OnPointerClick(PointerEventData data) => Selected?.Invoke(data.position);
    }
}
