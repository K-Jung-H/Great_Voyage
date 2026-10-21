using UnityEngine;
using UnityEngine.UI;
namespace GreatVoyage
{
    public sealed class EventScreenController:MonoBehaviour
    {
        [SerializeField,Tooltip("사전 제작한 이벤트 대화 화면.")]private ScreenView view;
        [SerializeField,Tooltip("공통 모달·정지 수명 관리자.")]private ScreenStack stack;
        private GameRuntime game;
        private string displayed;
        public void Bind(GameRuntime runtime){game=runtime;game.Changed+=Refresh;Refresh(false);}
        private void Refresh(bool arrived)
        {
            var current=game.State.events.current;
            bool announcement=game.Events.AwaitingChoice;
            bool result=current!=null&&current.phase==EventPhase.Resolved&&!current.resultAcknowledged;
            if(!announcement&&!result)return;
            string key=current.id+":"+current.phase;if(displayed==key)return;displayed=key;
            view.Text("Title",game.Events.Title);
            view.Text("Description",announcement?game.Events.Description:current.result);
            view.Text("Result",announcement?"사건 중 종료하면 같은 시작 상태와 판정 조건으로 재개합니다.":"결과와 항해일지 사건 기록이 반영되었습니다.");
            var choices=announcement?game.Events.Choices():new[]{new EventOption{id="ack",label="확인"}};
            var rows=EconomyScreens.ReplaceRows(view.Get<ScrollRect>("ChoiceList"),choices.Length);
            for(int i=0;i<rows.Count;i++){string id=choices[i].id;rows[i].Text("Choice",choices[i].label);rows[i].OnSelect(()=>Choose(id));}
            view.Connected(announcement?"사건 시작":"사건 종료");stack.Open(view.name,true);
        }
        private void Choose(string id)
        {
            bool announcement=game.Events.AwaitingChoice;
            stack.UnlockAndClose(view.name);
            if(announcement)game.Events.Choose(id);
            else if(game.State.events.current?.phase==EventPhase.Resolved){game.State.events.current.resultAcknowledged=true;game.RequestSave();game.NotifyChanged();}
        }
        private void OnDestroy(){if(game!=null)game.Changed-=Refresh;}
    }
}
