using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class SaveService
    {
        public string FilePath { get; }

        // Role: 한 플레이의 마지막 상태를 보관할 경로를 지정한다.
        public SaveService(string filePath) => FilePath = filePath;

        // Role: 마지막 저장을 읽고 손상·미지원 버전은 새 게임으로 덮어쓰지 않는다.
        public SaveData Load(string expectedSpace)
        {
            if (!File.Exists(FilePath))
            {
                if (File.Exists(FilePath + ".tmp") || File.Exists(FilePath + ".bak"))
                    throw new InvalidDataException("저장 완료 여부를 확인할 수 없습니다. 저장 파일을 보존했습니다.");
                return null;
            }
            if (new FileInfo(FilePath).Length > 1024 * 1024)
                throw new InvalidDataException("저장 파일 크기가 현재 형식의 범위를 초과합니다.");
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath, Encoding.UTF8));
            Validate(data, expectedSpace);
            return data;
        }

        // Role: 임시 파일을 디스크에 기록한 뒤 마지막 저장을 원자적으로 교체한다.
        public void Save(SaveData data, string expectedSpace)
        {
            Validate(data, expectedSpace);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
            string temporary = FilePath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
            else File.Move(temporary, FilePath);
        }

        // Role: 현재 단계에서 해석할 수 있는 식별자·버전·유한한 위치와 시간만 수용한다.
        private static void Validate(SaveData data, string expectedSpace)
        {
            if (data == null || data.format != "GreatVoyage.CabinSession" || data.version != 1)
                throw new InvalidDataException("손상되었거나 지원하지 않는 저장 형식입니다.");
            if (!Guid.TryParse(data.playthroughId, out _) || data.playerId != "player" || data.spaceId != expectedSpace)
                throw new InvalidDataException("저장된 인물 또는 공간 식별자를 확인할 수 없습니다.");
            if (double.IsNaN(data.elapsedSeconds) || double.IsInfinity(data.elapsedSeconds) || data.elapsedSeconds < 0 || data.elapsedSeconds > 600d * 365000d
                || float.IsNaN(data.playerX) || float.IsInfinity(data.playerX) || float.IsNaN(data.playerY) || float.IsInfinity(data.playerY))
                throw new InvalidDataException("저장된 위치 또는 시간이 유효하지 않습니다.");
        }
    }
}
