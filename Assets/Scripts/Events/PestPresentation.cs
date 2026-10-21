using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class PestPresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("Characters 아래 사전 배치한 유해 생물 부모.")]
        private Transform root;
        [SerializeField, Tooltip("Editor에서 제작한 유해 생물 Prefab.")]
        private SpriteRenderer prefab;
        private GameRuntime game;
        private readonly Dictionary<string, SpriteRenderer> instances = new Dictionary<string, SpriteRenderer>();
        public void Bind(GameRuntime runtime) => game = runtime;
        public void Refresh()
        {
            var pests = game.Pests.Active;
            root.gameObject.SetActive(pests.Length > 0);
            foreach (var pest in pests)
            {
                if (!instances.TryGetValue(pest.id, out var view)) { view = Instantiate(prefab, root); view.name = pest.id; instances.Add(pest.id, view); }
                view.transform.position = pest.position;
                view.transform.localScale = Vector3.one * game.Ship.CellSize * .45f;
                view.enabled = game.Ship.IsFloorVisible(pest.floorId);
                view.color = game.State.events.current.kind == "crabs" ? new Color(.8f,.2f,.12f) : game.State.events.current.kind == "barnacles" ? new Color(.4f,.5f,.55f) : new Color(.35f,.25f,.2f);
            }
            foreach (var id in instances.Keys.Where(id => !pests.Any(value => value.id == id)).ToArray()) { Destroy(instances[id].gameObject); instances.Remove(id); }
        }
    }
}
