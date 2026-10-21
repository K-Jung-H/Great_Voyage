using UnityEngine;
using UnityEngine.UI;

namespace GreatVoyage
{
    public sealed class BattleActorView : MonoBehaviour
    {
        [SerializeField, Tooltip("적 표현은 공통 캐릭터 파츠를 재사용한다. 아군은 기존 월드 인물을 표시한다.")]
        private Transform enemyVisual;
        [SerializeField, Tooltip("월드 Canvas의 사전 제작 체력 바.")]
        private RectTransform healthFill;
        [SerializeField, Tooltip("월드 Canvas의 사전 제작 공격 대기 바.")]
        private RectTransform cooldownFill;

        public void Display(BattleActor actor, CrewState character, BattleRules rules)
        {
            transform.position = character.position;
            enemyVisual.gameObject.SetActive(actor.enemy && !rules.IsCrew(actor.characterId) && character.health > 0);
            enemyVisual.localScale = Vector3.one * (character.species == "kraken" ? 2 : 1);
            gameObject.SetActive(character.health > 0);
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(character.health / (character.species == "kraken" ? rules.Settings.krakenHealth : actor.enemy && !rules.IsCrew(actor.characterId) ? rules.Settings.pirateHealth : 100f)), 1);
            float cooldown = rules.Cooldown(character.armed ? character.weapon : WeaponKind.Unarmed);
            cooldownFill.anchorMax = new Vector2(1 - Mathf.Clamp01(actor.cooldown / cooldown), 1);
        }
    }
}
