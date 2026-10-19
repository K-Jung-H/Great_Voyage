using System;
using System.IO;
using UnityEngine;

namespace GreatVoyage
{
    [DefaultExecutionOrder(-200)]
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField, Tooltip("저장·복원할 플레이어의 물리 위치.")]
        private Rigidbody2D player;
        [SerializeField, Tooltip("현재 공간의 안정적인 저장 식별자.")]
        private string spaceId = "captains-cabin";
        [SerializeField, Tooltip("층과 시설 상태를 복원할 함선.")]
        private ShipLayout ship;

        public const double SecondsPerDay = 600d;
        public PauseService Pauses { get; } = new PauseService();
        public bool IsPaused => Pauses.IsPaused;
        public string ErrorMessage { get; private set; }
        public bool IsReady { get; private set; }
        public double ElapsedSeconds => state?.elapsedSeconds ?? 0d;
        public string SavePath => saves?.FilePath;

        private SaveData state;
        private SaveService saves;
        private IDisposable errorPause;
        private bool savedForQuit;

        // Role: 세션을 읽고 위치를 복원하며 읽기 실패 시 기존 데이터를 보존한다.
        private void Awake()
        {
            Pauses.Changed += StopPlayerWhenPaused;
            string folder = Application.isEditor ? "Editor" : "Player";
            saves = new SaveService(Path.Combine(Application.persistentDataPath, "Saves", folder, "cabin-session.json"));
            try
            {
                if (player == null) throw new InvalidOperationException("플레이어 참조가 연결되지 않았습니다.");
                if (ship == null) throw new InvalidOperationException("함선 참조가 연결되지 않았습니다.");
                state = saves.Load(spaceId) ?? new SaveData
                {
                    playthroughId = Guid.NewGuid().ToString("D"), spaceId = spaceId,
                    playerX = player.position.x * ship.CellSize, playerY = player.position.y * ship.CellSize
                };
                state.ship = ship.Initialize(state.ship);
                if (!ship.ValidPosition(state.ship.activeFloor, new Vector2(state.playerX, state.playerY)))
                    throw new InvalidDataException("저장 위치가 현재 층의 경계 밖에 있습니다.");
                player.position = new Vector2(state.playerX, state.playerY);
                player.linearVelocity = Vector2.zero;
                IsReady = true;
            }
            catch (Exception error)
            {
                ErrorMessage = "저장을 불러오지 못했습니다. 기존 파일은 보존됩니다.\n" + error.Message;
                errorPause = Pauses.Acquire("LoadError");
                Debug.LogError(ErrorMessage, this);
            }
        }

        // Role: 정상 종료 전에 저장할 수 있도록 종료 요청을 구독한다.
        private void OnEnable() => Application.wantsToQuit += PrepareQuit;

        // Role: 정지 사유가 없을 때만 게임 시간을 진행한다.
        private void Update()
        {
            if (IsReady && !IsPaused) state.elapsedSeconds += Time.unscaledDeltaTime;
        }

        // Role: 정지 등록 즉시 현재 물리 이동을 멈춘다.
        private void StopPlayerWhenPaused()
        {
            if (IsPaused && player != null) player.linearVelocity = Vector2.zero;
        }

        // Role: 현재 위치·시간을 저장하고 실패하면 진행을 멈춰 오류를 표시한다.
        public bool SaveCurrent()
        {
            if (!IsReady || player == null) return false;
            try
            {
                state.playerX = player.position.x;
                state.playerY = player.position.y;
                ship.RememberPosition();
                saves.Save(state, spaceId);
                ErrorMessage = null;
                errorPause?.Dispose();
                errorPause = null;
                return true;
            }
            catch (Exception error)
            {
                ErrorMessage = "저장하지 못했습니다. 종료를 중단했습니다.\n" + error.Message;
                if (errorPause == null) errorPause = Pauses.Acquire("SaveError");
                Debug.LogError(ErrorMessage, this);
                return false;
            }
        }

        // Role: 저장 성공 후에만 플레이를 종료한다.
        public void SaveAndQuit()
        {
            if (!PrepareQuit()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Role: 운영체제 종료 요청도 저장 성공을 확인한 뒤 허용한다.
        private bool PrepareQuit()
        {
            if (savedForQuit) return true;
            if (!IsReady) return true;
            savedForQuit = SaveCurrent();
            return savedForQuit;
        }

        // Role: Editor의 정지 버튼처럼 종료 요청을 우회하는 종료에서도 마지막 상태를 저장한다.
        private void OnApplicationQuit()
        {
            if (!savedForQuit && IsReady) SaveCurrent();
        }

        // Role: 세션 비활성화 시 종료 콜백을 해제한다.
        private void OnDisable() => Application.wantsToQuit -= PrepareQuit;

        // Role: 정지 알림 구독을 정리한다.
        private void OnDestroy() => Pauses.Changed -= StopPlayerWhenPaused;
    }
}
