using UnityEngine;

namespace GreatVoyage
{
    public sealed class PortServicePoint : MonoBehaviour
    {
        [SerializeField] private EconomySession economy;
        [SerializeField] private PortService service;
        public Vector2 InteractionPosition => transform.TransformPoint(Vector3.down * .6f);
        public string Prompt => service == PortService.Merchant ? "[E] 직물 상인" : service == PortService.Broker ? "[E] 판매 대행인" : "[E] 주점 · 고용/계약";
        public bool Available => economy != null && economy.CanAct;

        // Role: 연결된 항구 서비스 화면을 요청한다.
        public void Interact() => economy.Open(service);
    }
}
