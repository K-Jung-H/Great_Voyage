using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GreatVoyage.EditorTools
{
    public sealed class SaveResetWindow : EditorWindow
    {
        private GameStartProfile profile;
        private string result;

        [MenuItem("Great Voyage/Development/Save Data")]
        private static void Open()
        {
            var window = GetWindow<SaveResetWindow>("Save Data");
            window.minSize = new Vector2(540, 340);
            window.profile = AssetDatabase.LoadAssetAtPath<GameStartProfile>("Assets/Data/Game/ReconstructionStart.asset");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("개발용 Save / Load 데이터", EditorStyles.boldLabel);
            profile = (GameStartProfile)EditorGUILayout.ObjectField("시작 프로필", profile, typeof(GameStartProfile), false);
            EditorGUILayout.HelpBox("Play Mode를 종료한 뒤 초기화하세요. 선택한 Editor 슬롯의 json / tmp / bak만 별도 백업 후 제거합니다. 다음 Game 실행은 초기 상태로 시작합니다. 기존 프로토타입 저장·Player 저장·가져오기 원본은 보존합니다.", MessageType.Info);
            if (!profile) return;
            try
            {
                string path = new SaveCoordinator(profile.saveSlot).FilePath;
                EditorGUILayout.LabelField("초기화 대상", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(path, EditorStyles.textArea, GUILayout.Height(46));
                string[] files = SlotFiles(path).Where(File.Exists).ToArray();
                foreach (string file in files)
                    EditorGUILayout.LabelField(Path.GetFileName(file), new FileInfo(file).Length + " bytes");
                if (files.Length == 0) EditorGUILayout.LabelField("저장 파일 없음 — 이미 초기 상태");
                if (GUILayout.Button("저장 폴더 열기"))
                {
                    string directory = Path.GetDirectoryName(path);
                    if (Directory.Exists(directory)) EditorUtility.RevealInFinder(directory);
                    else result = "아직 저장 폴더가 없습니다.";
                }
                bool importing = !string.IsNullOrWhiteSpace(profile.legacySourceCopy);
                if (importing)
                    EditorGUILayout.HelpBox("프로필의 Legacy Source Copy가 지정되어 있습니다. 초기화 후 같은 저장을 다시 가져오므로, 빈 게임을 테스트하려면 Inspector에서 이 값을 먼저 비우세요.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || files.Length == 0 || importing))
                {
                    if (GUILayout.Button("선택 슬롯 백업 후 초기화", GUILayout.Height(30)) &&
                        EditorUtility.DisplayDialog("개발 저장 초기화", string.Join("\n", files) + "\n\n별도 백업 후 이 파일만 제거합니다. 다음 Game 실행은 새 게임입니다. 진행할까요?", "백업 후 초기화", "취소"))
                        ResetSlot(path);
                }
                if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
        }

        private static string[] SlotFiles(string path) => new[] { path, path + ".tmp", path + ".bak" };

        // Role: 검증된 Editor 슬롯만 별도 보관하고 제거한다. 게임 상태/파일 로더는 실행하지 않는다.
        private void ResetSlot(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Play Mode/컴파일 종료 후 초기화하세요.");
            string expectedRoot = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "Saves", "Editor"));
            path = Path.GetFullPath(path);
            if (!string.Equals(Path.GetDirectoryName(path), expectedRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(path).StartsWith("reconstruction-", StringComparison.Ordinal))
                throw new InvalidOperationException("개발용 Editor 슬롯만 초기화할 수 있습니다.");
            string[] files = SlotFiles(path).Where(File.Exists).ToArray();
            if (files.Length == 0) { result = "초기화할 파일이 없습니다."; return; }
            foreach (string file in files) RejectLinks(file);
            string backup = Path.Combine(Application.persistentDataPath, "SaveResetBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            RejectLinks(backup);
            var manifest = new StringBuilder("Great Voyage Editor save reset\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
            foreach (string file in files)
            {
                string destination = Path.Combine(backup, Path.GetFileName(file));
                File.Copy(file, destination, false);
                string hash = Hash(file);
                if (hash != Hash(destination)) throw new IOException("백업 검증 실패. 원본은 제거하지 않았습니다.");
                manifest.AppendLine(file + " | SHA256 " + hash);
            }
            File.WriteAllText(Path.Combine(backup, "manifest.txt"), manifest.ToString(), Encoding.UTF8);
            foreach (string file in files)
            {
                if (Hash(file) != Hash(Path.Combine(backup, Path.GetFileName(file))))
                    throw new IOException("백업 중 원본 변경 감지. 제거를 중단했습니다.");
            }
            foreach (string file in files) File.Delete(file);
            result = "초기화 완료. 백업: " + backup;
            Debug.Log(result);
        }

        private static void RejectLinks(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("연결 경로는 초기화하지 않습니다: " + current);
        }

        private static string Hash(string file)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(file))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }
    }
}
