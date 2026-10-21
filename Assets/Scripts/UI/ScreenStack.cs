using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GreatVoyage
{
    public sealed class ScreenStack : MonoBehaviour
    {
        [SerializeField, Tooltip("시작부터 배치된 화면 인스턴스들.")]
        private ScreenView[] screens;
        [SerializeField, Tooltip("공유 UI 포커스 소유자.")]
        private EventSystem eventSystem;
        private readonly List<Entry> opened = new List<Entry>();
        private GameRuntime game;
        public event Action<ScreenView> Closed;
        private sealed class Entry
        {
            public ScreenView view;
            public IDisposable pause;
            public bool locked;
            public GameObject selection;
        }

        public bool IsOpen => opened.Count > 0;
        public ScreenView Top => IsOpen ? opened[opened.Count - 1].view : null;

        public ScreenView View(string name) => Array.Find(screens, view => view.name == name);
        public void Bind(GameRuntime runtime) => game = runtime;
        public void Open(string name, bool locked = false)
        {
            var view = View(name);
            if (!view)
                throw new InvalidOperationException("Unknown screen: " + name);
            var existing = opened.Find(entry => entry.view == view);
            if (existing != null)
            {
                existing.locked |= locked;
                return;
            }

            if (IsOpen && opened[opened.Count - 1].locked && name != "SessionModals")
                return;
            view.transform.SetAsLastSibling();
            var entry = new Entry
            {
                view = view,
                locked = locked,
                pause = game.Pauses.Acquire(name),
                selection = eventSystem.currentSelectedGameObject
            };
            opened.Add(entry);
            view.gameObject.SetActive(true);
            eventSystem.SetSelectedGameObject(null);
        }

        public bool CloseTop()
        {
            if (!IsOpen)
                return true;
            var entry = opened[opened.Count - 1];
            if (entry.locked)
                return false;
            opened.RemoveAt(opened.Count - 1);
            entry.view.gameObject.SetActive(false);
            entry.pause.Dispose();
            eventSystem.SetSelectedGameObject(entry.selection && entry.selection.activeInHierarchy ? entry.selection : null);
            Closed?.Invoke(entry.view);
            game.RequestSave();
            return true;
        }

        public void CloseAll()
        {
            while (IsOpen)
            {
                opened[opened.Count - 1].locked = false;
                CloseTop();
            }
        }

        public void UnlockAndClose(string name)
        {
            if (IsOpen && Top.name == name)
            {
                opened[opened.Count - 1].locked = false;
                CloseTop();
            }
        }

        private void OnDestroy()
        {
            foreach (var entry in opened)
                entry.pause?.Dispose();
            opened.Clear();
        }
    }
}
