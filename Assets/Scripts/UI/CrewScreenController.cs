using System.Collections.Generic;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class CrewScreenController : MonoBehaviour
    {
        [SerializeField, Tooltip("선원 정보·지속 업무 지정 화면.")]
        private ScreenView view;
        [SerializeField, Tooltip("공통 입력/정지 스택.")]
        private ScreenStack stack;
        private GameRuntime game;
        private string selected;
        private CrewDuty duty;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            view.Action("Exit", () => stack.CloseTop());
            view.Action("AssignWork", () =>
            {
                game.Crew.SetDuty(selected, duty);
                game.MarkInteraction();
                game.RequestSave();
                Refresh();
            });
        }

        public void Open(string id)
        {
            selected = id;
            var member = game.Crew.FindMember(id);
            if (member == null)
                return;
            duty = member.primaryDuty;
            stack.Open("CrewDetailsScreen");
            Refresh();
        }

        private void Refresh()
        {
            var member = game.Crew.FindMember(selected);
            if (member == null)
            {
                stack.CloseTop();
                return;
            }

            view.Text("Details/Name", member.displayName);
            view.Text("Details/Career", "항해 경력 " + member.navigationExperience);
            view.Text("Details/Specialty", member.specialty.ToString());
            view.Text("Details/Health", member.health.ToString("0"));
            view.Text("Details/Happiness", member.happiness.ToString("0"));
            view.Text("Details/Strength", member.strength.ToString("0"));
            view.Text("Details/Dexterity", member.dexterity.ToString("0"));
            view.Text("Details/Intelligence", member.intelligence.ToString("0"));
            view.Text("Details/Trust", member.trust.ToString("0"));
            view.Text("Details/History", string.Join("\n", member.career ?? new List<string>()));
            view.Text("Details/Wishes", member.wish == null || member.wish.kind == CrewWishKind.None ? "희망 조건 없음" : member.wish.kind + " · " + member.wish.targetId);
            view.Text("Details/CurrentWork", member.primaryDuty + " · " + member.activity);
            view.Connected(member.promiseBreached ? "약속 위반 · 신뢰가 지속적으로 감소합니다." : member.rank);
            var options = new List<CrewDuty>
            {
                CrewDuty.None
                , CrewDuty.Guard, CrewDuty.Lookout
            };
            if (member.specialty == CrewSpecialty.Maintenance)
                options.Add(CrewDuty.Maintenance);
            if (CrewBrain.QualifiedNavigator(member))
                options.Add(CrewDuty.Navigation);
            var rows = EconomyScreens.ReplaceRows(view.Get<UnityEngine.UI.ScrollRect>("WorkSelection"), options.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                var option = options[i];
                rows[i].Text("Choice", option.ToString());
                rows[i].OnSelect(() =>
                {
                    duty = option;
                    view.Connected("선택한 주업무: " + option + " · 적용 버튼으로 확정");
                });
            }
        }
    }
}
