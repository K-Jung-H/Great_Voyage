using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class QuestScreens : MonoBehaviour
    {
        [SerializeField, Tooltip("사전 제작한 시청 퀘스트/포로 Frame.")]
        private ScreenView view;
        private GameRuntime game;
        private string selected;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
            view.Action("QuestsTab", Refresh);
            view.Action("Quests/Details/Accept", () => { game.Market.Quest(selected); Refresh(); });
        }

        private void Refresh()
        {
            view.Get<RectTransform>("Quests").gameObject.SetActive(true);
            view.Get<RectTransform>("PrisonerHandover").gameObject.SetActive(false);
            var quests = game.Market.Current.quests.Where(value => !value.failed && !value.completed).ToArray();
            if (!quests.Any(value => value.id == selected)) selected = quests.FirstOrDefault()?.id;
            var rows = EconomyScreens.ReplaceRows(view.Get<ScrollRect>("Quests/QuestList"), quests.Length);
            for (int i = 0; i < quests.Length; i++)
            {
                var quest = quests[i];
                rows[i].Text("Quest", game.Market.ProductName(quest.productId) + " 납품");
                rows[i].Text("Progress", quest.accepted ? game.State.inventory.Get(quest.productId) + "/" + quest.quantity : "수주 가능");
                rows[i].Text("Reward", quest.reward + " 금화");
                rows[i].OnSelect(() => { selected = quest.id; Refresh(); });
            }
            var current = quests.FirstOrDefault(value => value.id == selected);
            view.Text("Quests/Details/Quest", current == null ? "퀘스트 없음" : game.Market.ProductName(current.productId) + " 납품");
            view.Text("Quests/Details/Description", current == null ? "다음 날 새 요청을 확인하세요." : "시청에서 수주하고 제한 시간 안에 물품을 전달하세요.");
            view.Text("Quests/Details/Requirements", current == null ? "" : current.quantity + " " + game.Market.ProductName(current.productId) + (current.accepted ? " · 남은 " + Mathf.CeilToInt((float)(current.expiresAt - game.ElapsedSeconds)) + "초" : ""));
            view.Text("Quests/Details/Reward", current?.reward + " 금화 · 현지 평판");
            view.Text("Quests/Details/RewardResult", game.Market.Message ?? "");
            view.Text("Quests/Details/Accept/Label", current?.accepted == true ? "납품" : "수주");
            view.Get<Button>("Quests/Details/Accept").interactable = current != null;
            view.Connected("현지 평판 " + game.Market.Current.localStanding + " · 전역 명성과 별도로 관리");
        }
    }
}
