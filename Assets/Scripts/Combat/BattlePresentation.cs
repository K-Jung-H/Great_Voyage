using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class BattlePresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("캐릭터 공통 부모 아래의 사건 전용 인물 표시 루트.")]
        private Transform actorsRoot;
        [SerializeField, Tooltip("사전 제작한 전투 인물/체력/대기 바 Prefab.")]
        private BattleActorView actorPrefab;
        [SerializeField, Tooltip("사전 제작한 총 투사체 Prefab.")]
        private Transform projectilePrefab;
        [SerializeField, Tooltip("사전 배치한 투사체 부모.")]
        private Transform projectilesRoot;
        [SerializeField, Tooltip("공통 카메라. 전투 동안 관전 목표로 전환한다.")]
        private CameraFollow cameraFollow;
        [SerializeField, Tooltip("Cameras 아래 사전 배치한 관전 목표.")]
        private Transform spectatorTarget;
        [SerializeField, Tooltip("전투 후 복귀할 플레이어.")]
        private Rigidbody2D player;
        private SpriteRenderer[] playerSprites;
        private GameRuntime game;
        private bool observing;
        private readonly Dictionary<string, BattleActorView> views = new Dictionary<string, BattleActorView>();
        private readonly List<Transform> projectiles = new List<Transform>();

        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        }

        public void Refresh()
        {
            bool active = game.Battle.Running;
            if (active != observing)
            {
                observing = active;
                player.simulated = !active;
                foreach (var sprite in playerSprites) sprite.enabled = !active;
                if (active)
                {
                    var floor = game.Ship.GetFloorDefinition(game.Battle.Settings.floorId);
                    spectatorTarget.position = (floor.origin + (Vector2)floor.size * .5f) * game.Ship.CellSize;
                }
                cameraFollow.BindTarget(active ? spectatorTarget : player.transform);
            }
            actorsRoot.gameObject.SetActive(active);
            projectilesRoot.gameObject.SetActive(active);
            if (!active)
            {
                foreach (var view in views.Values) Destroy(view.gameObject);
                views.Clear();
                foreach (var projectile in projectiles) Destroy(projectile.gameObject);
                projectiles.Clear();
                return;
            }
            foreach (var actor in game.Battle.State.actors)
            {
                if (!views.TryGetValue(actor.characterId, out var view))
                {
                    view = Instantiate(actorPrefab, actorsRoot);
                    view.name = "Battle_" + actor.characterId;
                    views.Add(actor.characterId, view);
                }
                view.Display(actor, game.Battle.Character(actor.characterId), game.Battle);
            }
            var state = game.Battle.State.projectiles;
            while (projectiles.Count < state.Count)
                projectiles.Add(Instantiate(projectilePrefab, projectilesRoot));
            for (int i = 0; i < projectiles.Count; i++)
            {
                projectiles[i].gameObject.SetActive(i < state.Count);
                if (i < state.Count) projectiles[i].position = state[i].position;
            }
        }
    }
}
