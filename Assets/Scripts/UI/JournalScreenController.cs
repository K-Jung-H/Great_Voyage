using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    public sealed class JournalScreenController : MonoBehaviour
    {
        [SerializeField, Tooltip("사전 구성한 일지 화면의 직렬화 참조.")]
        private ScreenView view;
        [SerializeField, Tooltip("공통 모달/정지 소유자.")]
        private ScreenStack stack;
        [SerializeField, Tooltip("Editor에 배치한 지도 Graphic.")]
        private JournalChart map;
        private GameRuntime game;
        private Vector2 selectedPosition;
        private string selectedEvent;
        private JournalVoyage selectedVoyage;
        public int Tool { get; private set; }

        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            map.Bind(game.Journal, game.Navigation, this);
            view.Action("Close", Close);
            view.Action("Left/PenTools/RecordPen", () => Tool = 0);
            view.Action("Left/PenTools/PlanPen", () => Tool = 1);
            view.Action("Center/MapControls/Record", () =>
            {
                game.Journal.Record();
                Refresh();
            });
            view.Action("Center/MapControls/EditPlan", () => Tool = 1);
            view.Action("Center/MapControls/ClearPlan", () =>
            {
                game.Journal.ClearPlan();
                Refresh();
            });
            view.Action("Center/MapControls/DeleteEvent", () =>
            {
                if (selectedVoyage != null && selectedEvent != null)
                    game.Journal.RemoveEvent(selectedVoyage, selectedEvent);
                Refresh();
            });
            view.Action("Center/MapControls/ZoomOut", () => map.Zoom(-1));
            view.Action("Center/MapControls/ZoomIn", () => map.Zoom(1));
            var note = view.Get<TMP_InputField>("Right/NoteEditor");
            note.interactable = true;
            note.characterLimit = 500;
            note.onEndEdit.AddListener(text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                    return;
                game.Journal.AddNote(selectedPosition, text);
                Refresh();
            });
        }

        public void Open()
        {
            stack.Open("Journal");
            Refresh();
            SelectMapPoint(game.Navigation.WorldPosition, 1);
        }

        public void Close()
        {
            stack.CloseTop();
            game.RequestSave();
        }

        public void Refresh()
        {
            view.Connected(game.Journal.Message ?? "기록 · 계획 · 메모 · 과거 항해 레이어");
            var history = game.Journal.State.voyages.AsEnumerable().Reverse().ToArray();
            var rows = EconomyScreens.ReplaceRows(view.Get<ScrollRect>("Left/HistoryScroll"), history.Length);
            for (int i = 0; i < history.Length; i++)
            {
                var voyage = history[i];
                rows[i].Text("Voyage", "항해 " + voyage.sequence);
                rows[i].Text("Date", voyage.departurePort + " → " + (voyage.arrivalPort ?? "항해 중"));
                rows[i].Text("Summary", voyage.recorded.Count + " 기록점");
                rows[i].OnSelect(() =>
                {
                    selectedVoyage = voyage;
                    view.Text("Right/Description", voyage.departurePort + " → " + voyage.arrivalPort);
                });
                var button = rows[i].Get<Button>("LayerToggle");
                button.interactable = true;
                button.onClick.AddListener(() =>
                {
                    if (!map.Layers.Add(voyage.sequence))
                        map.Layers.Remove(voyage.sequence);
                    map.SetVerticesDirty();
                });
            }
        }

        public void SelectMapPoint(Vector2 point, float radius)
        {
            selectedPosition = point;
            selectedEvent = null;
            selectedVoyage = null;
            Tool = 0;
            string details = $"해역 ({point.x:0.0}, {point.y:0.0})";
            foreach (var port in game.Navigation.Ports)
                if (Vector2.Distance(point, port.position) <= radius)
                {
                    selectedPosition = port.position;
                    details = port.displayName + " · 항구";
                }

            foreach (var voyage in game.Journal.State.voyages.Where(v => v == game.Journal.Current || map.Layers.Contains(v.sequence)))
            {
                foreach (var item in voyage.events)
                    if (Vector2.Distance(point, item.position) <= radius)
                    {
                        selectedPosition = item.position;
                        selectedEvent = item.id;
                        selectedVoyage = voyage;
                        details = item.day + "일 · " + item.title;
                    }

                foreach (var note in voyage.notes)
                    if (Vector2.Distance(point, note.position) <= radius)
                        details += "\n" + note.text;
            }

            view.Text("Right/Description", details);
            view.Get<TMP_InputField>("Right/NoteEditor").SetTextWithoutNotify("");
        }
    }
}
