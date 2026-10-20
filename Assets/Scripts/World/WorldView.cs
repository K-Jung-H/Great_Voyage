using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    [Serializable]
    public sealed class AreaView
    {
        public string id;
        public GameObject root;
        public Transform entry;
        public Rect bounds;
    }

    [Serializable]
    public sealed class FacilityViewBinding
    {
        public string id;
        public GameObject root;
        public SpriteRenderer[] renderers;
    }

    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField, Tooltip("지역 ID·표시·진입점. 상태는 GameState가 소유한다.")]
        private AreaView[] areas;
        [SerializeField, Tooltip("시설 ID별 내구도 표시.")]
        private FacilityViewBinding[] facilities;
        [SerializeField, Tooltip("동적 인물의 공통 부모.")]
        private Transform characters;
        [SerializeField, Tooltip("물리 모터를 가진 공유 선원 Prefab.")]
        private CrewMovement crewPrefab;
        [SerializeField, Tooltip("공통 플레이어 물리 모터.")]
        private Rigidbody2D player;
        private GameRuntime game;
        private readonly Dictionary<string, CrewMovement> crew = new Dictionary<string, CrewMovement>();
        private string visible;
        public event Action<string> FloorChanged;
        public event Action<string> CrewSelected;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            foreach (var tile in GetComponentsInChildren<SpecialTileView>(true))
                tile.Bind(game.Ship);
            foreach (var area in areas)
                area.root.transform.localScale = Vector3.one * game.Ship.CellSize;
            player.transform.localScale = Vector3.one * game.Ship.CellSize;
            ApplySpace(true);
            SyncCrew();
        }

        private string CurrentArea => game.Navigation.IsAboard ? game.Ship.ActiveFloor : game.State.player.areaId;

        public void CapturePlayer()
        {
            if (game == null)
                return;
            game.State.player.position = player.position;
            game.State.player.linearVelocity = player.linearVelocity;
        }

        public void ApplySpace(bool teleport = false)
        {
            string current = CurrentArea;
            if (!areas.Any(area => area.id == current))
                throw new InvalidOperationException("Unknown area: " + current);
            if (current != visible)
            {
                bool floorChanged = visible != null && visible.StartsWith("deck-") && current.StartsWith("deck-");
                CaptureBodies();
                foreach (var area in areas)
                    area.root.SetActive(area.id == current);
                visible = current;
                teleport = true;
                if (floorChanged)
                    FloorChanged?.Invoke(current);
            }

            if (teleport)
            {
                player.position = game.State.player.position;
                player.transform.position = new Vector3(player.position.x, player.position.y, 0);
                player.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }

            foreach (var member in crew.Values)
                member.SynchronizeVisibility(game.Crew.IsVisible(member.State));
        }

        public bool MoveArea(string target, Vector2 destination)
        {
            destination *= game.Ship.CellSize;
            var area = areas.FirstOrDefault(value => value.id == target);
            if (area == null || !area.bounds.Contains(destination / game.Ship.CellSize))
                return false;
            CapturePlayer();
            var location = game.State.player;
            string previousKey = location.areaId == "aboard" ? game.State.shipInstanceId + ":" + game.Ship.ActiveFloor : game.Navigation.DockedPort + ":" + location.areaId;
            var previous = location.visits.Find(visit => visit.id == previousKey);
            if (previous == null)
            {
                previous = new AreaVisit
                {
                    id = previousKey
                };
                location.visits.Add(previous);
            }

            previous.position = location.position;
            var remembered = location.visits.Find(visit => visit.id == game.Navigation.DockedPort + ":" + target);
            if (remembered != null)
            {
                if (!area.bounds.Contains(remembered.position / game.Ship.CellSize))
                    throw new InvalidOperationException("Stored area position invalid");
                destination = remembered.position;
            }

            location.areaId = target;
            location.position = destination;
            location.linearVelocity = Vector2.zero;
            ApplySpace(true);
            game.RequestSave();
            return true;
        }

        public void SyncCrew()
        {
            foreach (var member in game.State.crew.members)
            {
                if (crew.ContainsKey(member.id))
                    continue;
                var view = Instantiate(crewPrefab, characters);
                view.name = "Crew_" + member.id;
                view.Bind(member, game.Crew, game.Ship.CellSize);
                view.Selected += id => CrewSelected?.Invoke(id);
                crew.Add(member.id, view);
                game.Crew.BindBody(member.id, view);
            }

            foreach (var id in crew.Keys.Where(id => !game.State.crew.members.Any(member => member.id == id)).ToArray())
            {
                crew[id].SynchronizeVisibility(false);
                Destroy(crew[id].gameObject);
                crew.Remove(id);
            }
        }

        public void Refresh()
        {
            SyncCrew();
            ApplySpace();
            foreach (var binding in facilities)
            {
                var facility = game.Ship.GetFacility(binding.id);
                binding.root.SetActive(facility != null);
                if (facility == null)
                    continue;
                float value = facility.durability / game.Ship.GetFacilityDefinition(facility.definitionId).maxDurability;
                foreach (var renderer in binding.renderers)
                    renderer.color = Color.Lerp(new Color(.4f, .12f, .1f), new Color(.7f, .55f, .32f), value);
            }
        }

        public void CaptureBodies()
        {
            foreach (var view in crew.Values)
                view.CaptureState();
        }
    }
}
