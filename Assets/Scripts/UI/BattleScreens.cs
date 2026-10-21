using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class BattleScreens : MonoBehaviour
    {
        [SerializeField, Tooltip("사전 제작한 관전 UI.")]
        private ScreenView hud;
        [SerializeField, Tooltip("생존자 처분 화면.")]
        private ScreenView disposition;
        [SerializeField, Tooltip("선원별 사전 무장 화면.")]
        private ScreenView armory;
        [SerializeField, Tooltip("시청 퀘스트/포로 인계 Frame.")]
        private ScreenView townHall;
        [SerializeField, Tooltip("공통 화면/정지 수명.")]
        private ScreenStack stack;
        private GameRuntime game;
        private string selected;
        private string battleId;
        private System.Collections.Generic.List<ListItemView> healthRows;

        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            disposition.Action("Capture", () => Decide("capture"));
            disposition.Action("Execute", () => Decide("execute"));
            disposition.Action("Pardon", () => Decide("pardon"));
            armory.Action("Exit", () => stack.CloseTop());
            armory.Action("EquipAll", () =>
            {
                foreach (var member in game.State.crew.members.Where(value => value.availability == CrewAvailability.Ready))
                    if (!member.armed && !game.Battle.Equip(member.id, WeaponKind.Sword))
                        game.Battle.Equip(member.id, WeaponKind.Gun);
                RefreshArmory();
            });
            armory.Action("UnequipAll", () =>
            {
                foreach (var member in game.State.crew.members)
                    game.Battle.Equip(member.id, WeaponKind.Unarmed);
                RefreshArmory();
            });
            townHall.Action("Exit", () => stack.CloseTop());
            townHall.Action("PrisonersTab", RefreshHandover);
            townHall.Action("PrisonerHandover/Handover", () =>
            {
                game.Prisoners.Handover(selected);
                RefreshHandover();
            });
            townHall.Get<Button>("QuestsTab").interactable = false;
            game.Changed += Changed;
            Changed(false);
        }

        private void Changed(bool arrived)
        {
            bool show = game.Battle.Running;
            hud.gameObject.SetActive(show);
            if (show)
            {
                var actors = game.Battle.State.actors;
                string id = game.State.events.current.id;
                if (battleId != id)
                {
                    battleId = id;
                    healthRows = EconomyScreens.ReplaceRows(hud.Get<ScrollRect>("CrewHealthScroll"), actors.Count);
                    EconomyScreens.ReplaceRows(hud.Get<ScrollRect>("BattleLog"), 0);
                    hud.Connected("관전 전투 · 전투 명령 없음");
                }
                hud.Text("AliveCrew", "아군 " + actors.Count(value => !value.enemy && game.Battle.Character(value.characterId).health > 0));
                hud.Text("AliveEnemies", "적 " + actors.Count(value => value.enemy && !value.surrendered && game.Battle.Character(value.characterId).health > 0));
                for (int i = 0; i < actors.Count; i++)
                {
                    var member = game.Battle.Character(actors[i].characterId);
                    healthRows[i].Text("Name", member.displayName);
                    healthRows[i].Text("Specialty", member.armed ? member.weapon.ToString() : "맨손");
                    healthRows[i].Text("Health", member.health.ToString("0") + " · " + actors[i].cooldown.ToString("0.0") + "초");
                    var button = healthRows[i].GetComponent<Button>();
                    if (button) button.interactable = false;
                }
            }
            else if (game.State.events.current?.resultAcknowledged == true && game.Prisoners.Pending.Length > 0 && stack.Top != disposition)
                RefreshDisposition();
        }

        private void RefreshDisposition()
        {
            var targets = game.Prisoners.Pending;
            if (targets.Length == 0)
            {
                stack.UnlockAndClose(disposition.name);
                return;
            }
            if (!targets.Any(value => value.characterId == selected))
                selected = targets[0].characterId;
            var rows = EconomyScreens.ReplaceRows(disposition.Get<ScrollRect>("SurvivingTargets"), targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                string id = targets[i].characterId;
                var member = game.Battle.Character(id);
                rows[i].Text("Name", (id == selected ? "▶ " : "") + member.displayName);
                rows[i].Text("Health", member.health.ToString("0"));
                rows[i].OnSelect(() => { selected = id; RefreshDisposition(); });
            }
            disposition.Text("PrisonerCapacity", "포로 " + game.Prisoners.Held.Length + " / " + game.Prisoners.Capacity);
            disposition.Get<Button>("Capture").interactable = game.Prisoners.Held.Length < game.Prisoners.Capacity;
            disposition.Connected("생존자만 처분 · 생포에는 감옥 공간 필요");
            stack.Open(disposition.name, true);
        }

        private void Decide(string choice)
        {
            if (game.Prisoners.DisposeTarget(selected, choice))
            {
                disposition.Text("DispositionResult", choice == "capture" ? "생포했습니다." : choice == "execute" ? "처형했습니다." : "용서했습니다.");
                RefreshDisposition();
            }
        }

        public void OpenArmory()
        {
            if (game.Battle.Running)
                return;
            selected = game.State.crew.members.FirstOrDefault()?.id;
            RefreshArmory();
            stack.Open(armory.name);
        }

        private void RefreshArmory()
        {
            var members = game.State.crew.members.Where(value => value.availability == CrewAvailability.Ready).ToArray();
            var rows = EconomyScreens.ReplaceRows(armory.Get<ScrollRect>("CrewScroll"), members.Length);
            for (int i = 0; i < members.Length; i++)
            {
                string id = members[i].id;
                rows[i].Text("Name", members[i].displayName);
                rows[i].Text("Specialty", members[i].armed ? members[i].weapon.ToString() : "맨손");
                rows[i].Text("Health", members[i].health.ToString("0"));
                rows[i].OnSelect(() => { selected = id; RefreshArmory(); });
            }
            var member = game.Crew.FindMember(selected);
            armory.Text("SelectedCrew/Name", member?.displayName ?? "선원 없음");
            armory.Text("SelectedCrew/CurrentWeapon", member?.armed == true ? member.weapon.ToString() : "맨손");
            armory.Text("SelectedCrew/PersonalSetting", "무기 항목 선택으로 장비 변경");
            var choices = EconomyScreens.ReplaceRows(armory.Get<ScrollRect>("SelectedCrew/WeaponChoices"), 3);
            for (int i = 0; i < choices.Count; i++)
            {
                var weapon = (WeaponKind)i;
                choices[i].Text("Item", i == 0 ? "맨손" : i == 1 ? "칼" : "총");
                choices[i].Text("Stock", i == 0 ? "-" : game.State.inventory.Get(i == 1 ? "sword" : "gun").ToString());
                choices[i].Text("UnitPrice", "");
                var quantity = choices[i].Get<TMPro.TMP_InputField>("Quantity");
                if (quantity) quantity.gameObject.SetActive(false);
                choices[i].OnSelect(() => { game.Battle.Equip(selected, weapon); RefreshArmory(); });
            }
            armory.Connected("사전 무장은 전투 시작 시 유지됩니다.");
        }

        public void OpenTownHall()
        {
            RefreshHandover();
            stack.Open(townHall.name);
        }

        private void RefreshHandover()
        {
            townHall.Get<RectTransform>("Quests").gameObject.SetActive(false);
            townHall.Get<RectTransform>("PrisonerHandover").gameObject.SetActive(true);
            var held = game.Prisoners.Held;
            if (!held.Any(value => value.characterId == selected))
                selected = held.FirstOrDefault()?.characterId;
            var rows = EconomyScreens.ReplaceRows(townHall.Get<ScrollRect>("PrisonerHandover/PrisonerList"), held.Length);
            for (int i = 0; i < held.Length; i++)
            {
                string id = held[i].characterId;
                rows[i].Text("Name", (id == selected ? "▶ " : "") + game.Battle.Character(id).displayName);
                rows[i].Text("Health", game.Battle.Character(id).health.ToString("0"));
                rows[i].OnSelect(() => { selected = id; RefreshHandover(); });
            }
            townHall.Text("PrisonerHandover/Reward", "1명 인계 보상 " + game.Battle.Settings.handoverReward);
            townHall.Get<Button>("PrisonerHandover/Handover").interactable = selected != null;
            townHall.Connected("포로 인계 · 퀘스트 연결은 10단계 구현 중");
        }

        private void OnDestroy()
        {
            if (game != null) game.Changed -= Changed;
        }
    }
}
