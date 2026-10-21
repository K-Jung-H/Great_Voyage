using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GreatVoyage
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JournalChart : MaskableGraphic, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        private JournalRules journal;
        private NavigationRules navigation;
        private JournalScreenController panel;
        public void Bind(JournalRules journal, NavigationRules navigation, JournalScreenController panel)
        {
            this.journal = journal;
            this.navigation = navigation;
            this.panel = panel;
            center = navigation.Ports.Aggregate(Vector2.zero, (sum, port) => sum + port.position) / navigation.Ports.Length;
            span = Mathf.Max(60, navigation.Ports.Max(port => Vector2.Distance(port.position, center)) * 2.6f);
            raycastTarget = true;
        }

        public void Zoom(int direction)
        {
            span = Mathf.Clamp(span * Mathf.Pow(.85f, direction), 12, MaximumSpan);
            SetVerticesDirty();
        }

        private Vector2 center = new Vector2(15, 0);
        private float span = 60;
        private float MaximumSpan => navigation == null ? 250 : Mathf.Max(navigation.WorldBounds.width, navigation.WorldBounds.height) * 1.25f;
        public readonly HashSet<int> Layers = new HashSet<int>();
        // Role: 지도·기록의 현재 상태를 표시 갱신에 반영한다.
        private void Update()
        {
            if (journal != null && journal.State != null)
                SetVerticesDirty();
        }

        // Role: 지도 좌표를 화면 내부 좌표로 변환한다.
        private Vector2 Local(Vector2 world) => (world - center) * (rectTransform.rect.width / span);
        // Role: 마우스 좌표를 해역 좌표로 변환한다.
        private Vector2 World(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, data.position, data.pressEventCamera, out Vector2 local);
            return local * (span / rectTransform.rect.width) + center;
        }

        // Role: 실제 고정 항구·계획·확정 기록·사건·현재 배를 지도에 그린다.
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (navigation == null || journal == null || journal.State == null)
                return;
            Color grid = new Color(.55f, .75f, .75f, .12f);
            float step = Mathf.Pow(10, Mathf.Floor(Mathf.Log10(span / 8)));
            for (float x = Mathf.Floor((center.x - span * .6f) / step) * step; x <= center.x + span * .6f; x += step)
            {
                Line(mesh, new Vector2(x, center.y - span), new Vector2(x, center.y + span), grid, .7f);
            }
            for (float y = Mathf.Floor((center.y - span * .6f) / step) * step; y <= center.y + span * .6f; y += step)
                Line(mesh, new Vector2(center.x - span, y), new Vector2(center.x + span, y), grid, .7f);

            foreach (JournalVoyage voyage in journal.State.voyages)
            {
                if (voyage != journal.Current && !Layers.Contains(voyage.sequence))
                    continue;
                float alpha = voyage == journal.Current ? 1 : .45f;
                for (int i = 1; i < voyage.recorded.Count; i++)
                    if (!voyage.recorded[i].breakBefore)
                        Line(mesh, voyage.recorded[i - 1].position, voyage.recorded[i].position, new Color(.95f, .85f, .55f, alpha), 2.5f);
                if (voyage != journal.Current)
                    for (int i = 1; i < voyage.plan.Count; i++)
                        Line(mesh, voyage.plan[i - 1], voyage.plan[i], new Color(.4f, .7f, .95f, alpha), 1.5f);
                foreach (JournalEvent item in voyage.events)
                    Marker(mesh, item.position, new Color(.95f, .35f, .3f, alpha), 7);
                foreach (JournalNote item in voyage.notes)
                    Marker(mesh, item.position, new Color(.65f, .9f, .5f, alpha), 4);
            }

            for (int i = 1; i < journal.State.plan.Count; i++)
                Line(mesh, journal.State.plan[i - 1], journal.State.plan[i], new Color(.4f, .8f, 1), 2);
            foreach (Vector2 point in journal.State.plan)
                Marker(mesh, point, new Color(.4f, .8f, 1), 3);
            foreach (PortDefinition port in navigation.Ports)
                Marker(mesh, port.position, new Color(.95f, .7f, .25f), 9);
            Vector2 position = Local(navigation.WorldPosition);
            float angle = navigation.Heading * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Triangle(mesh, position + forward * 12, position - forward * 7 + new Vector2(-forward.y, forward.x) * 7, position - forward * 7 - new Vector2(-forward.y, forward.x) * 7, Color.white);
        }

        // Role: 두 해역 지점을 일정 화면 두께의 선으로 잇는다.
        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, Color tint, float width)
        {
            Vector2 a = Local(from), b = Local(to), normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Triangle(mesh, a - normal, a + normal, b + normal, tint);
            Triangle(mesh, a - normal, b + normal, b - normal, tint);
        }

        // Role: 항구·사건·메모를 구분하는 마름모 표식을 그린다.
        private void Marker(VertexHelper mesh, Vector2 world, Color tint, float radius)
        {
            Vector2 p = Local(world);
            Triangle(mesh, p + Vector2.up * radius, p + Vector2.left * radius, p + Vector2.down * radius, tint);
            Triangle(mesh, p + Vector2.up * radius, p + Vector2.down * radius, p + Vector2.right * radius, tint);
        }

        // Role: UI 메시 삼각형 하나를 추가한다.
        private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
        }

        // Role: 선택 도구 또는 펜 도구의 지도 클릭을 처리한다.
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left)
                return;
            Vector2 point = World(data);
            if (panel.Tool == 1)
                journal.AddPlanPoint(point);
            else
                panel.SelectMapPoint(point, span * .025f);
        }

        // Role: 왼쪽 펜 드래그로 계획을 그리고 오른쪽 드래그로 지도를 이동한다.
        public void OnDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right || (data.button == PointerEventData.InputButton.Left && panel.Tool == 0))
            {
                center -= data.delta * (span / rectTransform.rect.width) / transform.lossyScale.x;
                SetVerticesDirty();
            }
            else if (data.button == PointerEventData.InputButton.Left && panel.Tool == 1)
                journal.AddPlanPoint(World(data));
        }

        // Role: 지도 중심을 기준으로 확대·축소한다.
        public void OnScroll(PointerEventData data)
        {
            span = Mathf.Clamp(span * Mathf.Pow(.85f, data.scrollDelta.y), 12, MaximumSpan);
            SetVerticesDirty();
        }
    }
}
