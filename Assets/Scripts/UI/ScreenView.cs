using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    [Serializable]
    public sealed class UIReference
    {
        public string key;
        public Component component;
    }

    public sealed class ScreenView : MonoBehaviour
    {
        [SerializeField, Tooltip("Editor에서 지정한 컴포넌트 참조. 런타임 경로 탐색/화면 생성 없음.")]
        private UIReference[] references;
        public T Get<T>(string key)
            where T : Component
        {
            var reference = references.FirstOrDefault(value => value.key == key && value.component is T);
            if (reference == null)
                throw new InvalidOperationException(name + ": missing UI reference " + key + " (" + typeof(T).Name + ")");
            return (T)reference.component;
        }

        public void Text(string key, string value) => Get<TMP_Text>(key).text = value;
        public void Action(string key, UnityEngine.Events.UnityAction action)
        {
            var button = Get<Button>(key);
            button.interactable = true;
            button.onClick.AddListener(action);
        }

        public void Connected(string message)
        {
            var status = references.FirstOrDefault(value => value.key == "Availability" && value.component is TMP_Text);
            if (status != null)
                ((TMP_Text)status.component).text = message;
        }
    }
}
