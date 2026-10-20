using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GreatVoyage
{
    public sealed class HudPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("지갑 표시.")]
        private TMP_Text gold;
        [SerializeField, Tooltip("게임 날짜 표시.")]
        private TMP_Text date;
        [SerializeField, Tooltip("항해 중에만 표시하는 날짜.")]
        private TMP_Text voyageDay;
        [SerializeField, Tooltip("항해 날짜 루트.")]
        private GameObject voyageRegion;
        [SerializeField, Tooltip("시간 구간 이미지들.")]
        private Image[] timeBands;
        private GameRuntime game;
        public void Bind(GameRuntime runtime)
        {
            game = runtime;
        }

        public void Refresh()
        {
            if (game == null)
                return;
            gold.text = "금화 " + game.State.inventory.Gold;
            date.text = $"{(int)(game.ElapsedSeconds / 600) + 1}일 · {game.ElapsedSeconds % 600 / 600 * 24:00.0}시";
            voyageRegion.SetActive(!game.Navigation.IsDocked);
            double started = game.Navigation.State.voyageStartedAt;
            voyageDay.text = started < 0 ? "항해일자 —" : "항해 " + ((int)((game.ElapsedSeconds - started) / 600) + 1) + "일";
            float time = (float)(game.ElapsedSeconds % 600 / 600);
            for (int i = 0; i < timeBands.Length; i++)
                timeBands[i].color = new Color(timeBands[i].color.r, timeBands[i].color.g, timeBands[i].color.b, i == Mathf.Min(4, (int)(time * 5)) ? 1 : .3f);
        }
    }
}
