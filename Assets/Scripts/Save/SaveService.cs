using System;
using System.IO;
using System.Collections.Generic;
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
            string json = File.ReadAllText(FilePath, Encoding.UTF8);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            Validate(data, expectedSpace);
            if (data.version == 1)
            {
                // v1에는 함선이 없다. 역직렬화된 빈 객체 대신 초기 함선 생성 경로를 사용한다.
                data.ship = null;
            }
            if (data.version < 3)
            {
                data.navigation = null;
            }
            if (data.version < 4)
            {
                data.crew = null;
            }
            if (data.version < 5)
            {
                if (data.crew?.members != null)
                    foreach (CrewState member in data.crew.members)
                        if (member != null) { member.velocity = Vector2.zero; member.repathRemaining = 0; }
                data.version = 5;
            }
            if (data.version < 6)
            {
                LegacyRoutes old = JsonUtility.FromJson<LegacyRoutes>(json);
                if (data.crew?.members != null)
                    foreach (CrewState member in data.crew.members)
                    {
                        if (member == null) continue;
                        LegacyMember route = old?.crew?.members?.Find(candidate => candidate != null && candidate.id == member.id);
                        if (route?.path != null && route.path.Count > 0)
                        {
                            member.nextWaypoint = route.path[0];
                            member.destination = route.path[route.path.Count - 1];
                        }
                        if (member.activity == CrewActivity.Repairing || member.activity == CrewActivity.Sleeping || member.activity == CrewActivity.Eating)
                        { member.hasWorkPosition = true; member.workPosition = member.position; }
                    }
                data.version = 6;
            }
            if (data.version < 7) { data.economyEnabled = false; data.version = 7; }
            if (!data.economyEnabled) data.economy = null;
            return data;
        }

        [Serializable] private sealed class LegacyRoutes { public LegacyRoster crew = null; }
        [Serializable] private sealed class LegacyRoster { public List<LegacyMember> members = null; }
        [Serializable] private sealed class LegacyMember { public string id = null; public List<CrewWaypoint> path = null; }

        // Role: 임시 파일을 디스크에 기록한 뒤 마지막 저장을 원자적으로 교체한다.
        public void Save(SaveData data, string expectedSpace)
        {
            Validate(data, expectedSpace);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
            if (bytes.Length > 1024 * 1024) throw new InvalidDataException("저장 데이터가 읽기 가능한 크기 한도를 초과했습니다. 기존 파일은 보존됩니다.");
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
            if (data == null || data.format != "GreatVoyage.CabinSession" || data.version < 1 || data.version > 7)
                throw new InvalidDataException("손상되었거나 지원하지 않는 저장 형식입니다.");
            if (data.version >= 2 && data.ship == null)
                throw new InvalidDataException("함선 상태가 누락된 저장 파일입니다.");
            if (data.version >= 3 && data.navigation == null)
                throw new InvalidDataException("항해 상태가 누락된 저장 파일입니다.");
            if (data.version >= 4 && data.crew == null)
                throw new InvalidDataException("선원 상태가 누락된 저장 파일입니다.");
            if (data.version >= 7 && data.economyEnabled && (data.economy == null || string.IsNullOrEmpty(data.economy.settingsId)))
                throw new InvalidDataException("경제 상태가 누락된 저장 파일입니다.");
            if (data.version >= 7 && !data.economyEnabled && data.economy != null && !string.IsNullOrEmpty(data.economy.settingsId))
                throw new InvalidDataException("경제 사용 여부와 저장 상태가 일치하지 않습니다.");
            if (!Guid.TryParse(data.playthroughId, out _) || data.playerId != "player" || data.spaceId != expectedSpace)
                throw new InvalidDataException("저장된 인물 또는 공간 식별자를 확인할 수 없습니다.");
            if (double.IsNaN(data.elapsedSeconds) || double.IsInfinity(data.elapsedSeconds) || data.elapsedSeconds < 0 || data.elapsedSeconds > 600d * 365000d
                || float.IsNaN(data.playerX) || float.IsInfinity(data.playerX) || float.IsNaN(data.playerY) || float.IsInfinity(data.playerY))
                throw new InvalidDataException("저장된 위치 또는 시간이 유효하지 않습니다.");
        }
    }
}
