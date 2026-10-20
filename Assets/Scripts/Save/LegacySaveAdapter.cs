using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class LegacySaveAdapter
    {
        public SaveData Decode(string json, string expectedSpace)
        {
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
                        if (member != null)
                        {
                            member.velocity = Vector2.zero;
                            member.repathRemaining = 0;
                        }

                data.version = 5;
            }

            if (data.version < 6)
            {
                LegacyRoutes old = JsonUtility.FromJson<LegacyRoutes>(json);
                if (data.crew?.members != null)
                    foreach (CrewState member in data.crew.members)
                    {
                        if (member == null)
                            continue;
                        LegacyMember route = old?.crew?.members?.Find(candidate => candidate != null && candidate.id == member.id);
                        if (route?.path != null && route.path.Count > 0)
                        {
                            member.nextWaypoint = route.path[0];
                            member.destination = route.path[route.path.Count - 1];
                        }

                        if (member.activity == CrewActivity.Repairing || member.activity == CrewActivity.Sleeping || member.activity == CrewActivity.Eating)
                        {
                            member.hasWorkPosition = true;
                            member.workPosition = member.position;
                        }
                    }

                data.version = 6;
            }

            if (data.version < 7)
            {
                data.economyEnabled = false;
                data.version = 7;
            }

            if (!data.economyEnabled)
                data.economy = null;
            if (data.version < 8)
            {
                data.journalEnabled = false;
                data.version = 8;
            }

            if (!data.journalEnabled)
            {
                data.journal = null;
                data.sleep = null;
            }

            return data;
        }

        [Serializable]
        private sealed class LegacyRoutes
        {
            public LegacyRoster crew = null;
        }

        [Serializable]
        private sealed class LegacyRoster
        {
            public List<LegacyMember> members = null;
        }

        [Serializable]
        private sealed class LegacyMember
        {
            public string id = null;
            public List<CrewWaypoint> path = null;
        }

        // Role: 현재 단계에서 해석할 수 있는 식별자·버전·유한한 위치와 시간만 수용한다.
        private static void Validate(SaveData data, string expectedSpace)
        {
            if (data == null || data.format != "GreatVoyage.CabinSession" || data.version < 1 || data.version > 8)
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
            if (data.version >= 8 && data.journalEnabled && (data.journal == null || data.sleep == null))
                throw new InvalidDataException("일지 또는 취침 상태가 누락되었습니다.");
            if (!Guid.TryParse(data.playthroughId, out _) || data.playerId != "player" || data.spaceId != expectedSpace)
                throw new InvalidDataException("저장된 인물 또는 공간 식별자를 확인할 수 없습니다.");
            if (double.IsNaN(data.elapsedSeconds) || double.IsInfinity(data.elapsedSeconds) || data.elapsedSeconds < 0 || data.elapsedSeconds > 600d * 365000d || float.IsNaN(data.playerX) || float.IsInfinity(data.playerX) || float.IsNaN(data.playerY) || float.IsInfinity(data.playerY))
                throw new InvalidDataException("저장된 위치 또는 시간이 유효하지 않습니다.");
        }

        public static GameState Convert(SaveData old)
        {
            var state = new GameState
            {
                playthroughId = old.playthroughId,
                elapsedSeconds = old.elapsedSeconds,
                player = new PlayerLocation
                {
                    position = new Vector2(old.playerX, old.playerY),
                    areaId = old.navigation == null || old.navigation.aboard ? "aboard" : "dock"
                },
                ship = old.ship,
                navigation = old.navigation,
                crew = old.crew,
                economy = old.economy,
                journal = old.journal,
                sleep = old.sleep
            };
            state.inventory.Gold = old.economy?.gold ?? 0;
            state.inventory.Cargo = old.economy?.cargo ?? 0;
            state.inventory.Food = old.crew?.food ?? 0;
            state.inventory.RepairTools = old.crew?.repairTools ?? 0;
            if (old.crew != null)
                foreach (var member in old.crew.members)
                    state.Register(member);
            if (old.economy != null)
            {
                foreach (var market in old.economy.markets)
                    state.inventory.Set(state.inventory.cargoProductId, market.stock, "port:" + market.portId + ":merchant");
                foreach (var contract in old.economy.contracts)
                {
                    if (contract.formerCrew != null)
                    {
                        if (state.characters.Exists(member => member.id == contract.formerCrew.id))
                            throw new InvalidDataException("Conflicting legacy character snapshots require review");
                        state.Register(contract.formerCrew);
                        contract.formerCrew = null;
                    }
                }
            }

            return state;
        }
    }
}
