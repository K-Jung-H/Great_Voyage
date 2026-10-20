using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    [Serializable]
    public sealed class ItemField
    {
        public string key;
        public Component component;
    }

    public sealed class ListItemView : MonoBehaviour
    {
        [SerializeField, Tooltip("항목 Prefab의 명시적인 필드 참조.")]
        private ItemField[] fields;
        public void Text(string key, string value)
        {
            var field = fields.FirstOrDefault(item => item.key == key && item.component is TMP_Text);
            if (field != null)
                ((TMP_Text)field.component).text = value;
        }

        public T Get<T>(string key)
            where T : Component => fields.Where(item => item.key == key).Select(item => item.component).OfType<T>().FirstOrDefault();
        public void OnSelect(UnityEngine.Events.UnityAction action)
        {
            var b = GetComponent<Button>();
            if (b)
            {
                b.interactable = true;
                b.onClick.AddListener(action);
            }
        }
    }
}
