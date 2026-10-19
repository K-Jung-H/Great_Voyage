using UnityEngine;

namespace GreatVoyage
{
    public sealed class BedInteraction : MonoBehaviour
    {
        private PlayerSleep sleep;
        private ShipLayout ship;
        private string id;
        public bool Available => sleep != null && sleep.CanUseBed(id);
        public Vector2 InteractionPosition => ship.TryGetAccessPosition(id, out Vector2 point) ? point : (Vector2)transform.position;
        public string Prompt => sleep.State != null && sleep.State.mode == PlayerRest.Watch ? "[E] 불침번 종료 · 다시 취침" : "[E] 취침";

        // Role: 생성된 침대 시설의 안정 ID를 플레이어 취침에 연결한다.
        public void Bind(PlayerSleep owner, ShipLayout layout, string facilityId) { sleep = owner; ship = layout; id = facilityId; }
        // Role: 해당 침대에서 취침 또는 불침번 종료를 요청한다.
        public void Interact() { if (Available) sleep.UseBed(id); }
    }
}
