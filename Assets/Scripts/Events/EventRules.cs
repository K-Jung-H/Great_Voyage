using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class EventOption
    {
        public string id, label;
    }

    public sealed class EventRules
    {
        private readonly GameRuntime game;
        private readonly EventSettings settings;
        private IDisposable announcementPause;
        private static readonly string[] Kinds = { "storm", "pirates", "rats", "watch", "island", "mutiny", "kraken", "fish", "crabs", "barnacles" };
        public EventState State => game.State.events;
        public bool AwaitingChoice => State.current != null && State.current.phase == EventPhase.Announced;
        public bool Running => State.current != null && State.current.phase == EventPhase.Running;
        public string Title => TitleFor(State.current?.kind);
        public static string TitleFor(string kind) => kind switch { "storm"=>"폭풍우", "pirates"=>"해적 습격", "rats"=>"쥐 발생", "watch"=>"불침번", "island"=>"무인도 발견", "mutiny"=>"선상 반란", "kraken"=>"크라켄", "fish"=>"물고기떼", "crabs"=>"게 발생", "barnacles"=>"따개비 발생", _=>"사건" };
        public string Description => State.current.kind switch
        {
            "storm"=>"갑판 선원과 펼친 돛이 피해를 입고 방향타가 흔들립니다.",
            "pirates" or "kraken" or "mutiny"=>"선원은 자동으로 무장하여 전투에 참여합니다. 선장은 전투를 관전합니다.",
            "island"=>"파견할 선원을 선택하거나 섬을 지나갈 수 있습니다. 파견된 선원은 결과가 나올 때까지 업무를 수행하지 않습니다.",
            "watch"=>"순찰 거리를 채우고 침대로 돌아가세요. 미충족 시 다른 사건 가능성이 증가합니다.",
            "fish"=>"물고기떼를 지나갑니다. 선내에 떨어진 생선은 식량을 대신하고 하선할 때 재고로 전환됩니다.",
            _=>"유해 생물이 시설에 접근해 피해를 줍니다. 선원 경비 업무로 제거할 수 있습니다."
        };
        public EventRules(GameRuntime game, EventSettings settings)
        {
            this.game=game; this.settings=settings;
            Validate(State,game.State.playthroughId);
            if(!settings || settings.minimumInterval<=0 || settings.maximumInterval<settings.minimumInterval || settings.stormDuration<=0 || settings.healthDamagePerSecond<0 || settings.sailDamagePerSecond<0 || settings.headingVariation<0 || game.Ship.GetFloorDefinition(settings.exposedFloor)==null || settings.weights==null || settings.weights.Length==0 || settings.weights.Any(value=>value==null||!Kinds.Contains(value.kind)||value.weight<0||float.IsNaN(value.weight)||float.IsInfinity(value.weight)) || settings.weights.Sum(value=>value.weight)<=0 || settings.pestDuration<=0 || settings.dispatchDuration<=0 || settings.watchDuration<=0 || settings.patrolDistance<0 || settings.islandReward<0 || settings.fishYield<0)
                throw new InvalidDataException("Invalid event definition");
            if(AwaitingChoice)announcementPause=game.Pauses.Acquire("EventAnnouncement");
        }
        public static void Validate(EventState state,string playthrough)
        {
            if(state==null)return;
            if(state.sequence<0 || double.IsNaN(state.nextAt)||double.IsInfinity(state.nextAt)||state.nextAt< -1 || float.IsNaN(state.watchFailureBonus)||float.IsInfinity(state.watchFailureBonus)||state.watchFailureBonus<0)throw new InvalidDataException("Invalid event schedule");
            var current=state.current;if(current==null)return;
            if(current.id!=playthrough+":event:"+state.sequence || !Kinds.Contains(current.kind) || !Enum.IsDefined(typeof(EventPhase),current.phase) || current.initialSeed==0 || current.randomState==0 || current.startedAt<0 || double.IsNaN(current.startedAt)||double.IsInfinity(current.startedAt)||current.progressedSeconds<0||double.IsNaN(current.progressedSeconds)||double.IsInfinity(current.progressedSeconds)||(current.phase!=EventPhase.Resolved&&string.IsNullOrEmpty(current.checkpoint))||current.effectsApplied!=(current.phase==EventPhase.Resolved))throw new InvalidDataException("Invalid event identity or checkpoint");
            if(current.phase!=EventPhase.Resolved)
            {
                var checkpoint=SaveCoordinator.DecodeSnapshot<FoundationSaveData>(current.checkpoint);
                if(checkpoint==null||checkpoint.format!="GreatVoyage.Game"||checkpoint.version!=10||checkpoint.playthroughId!=playthrough||checkpoint.events!=null||checkpoint.elapsedSeconds!=current.startedAt)throw new InvalidDataException("Event checkpoint does not belong to this session");
                SaveCoordinator.ValidateCheckpoint(checkpoint);
            }
        }
        private bool MutinyDue
        {
            get {var members=game.State.crew.members.Where(value=>value.health>0&&value.availability==CrewAvailability.Ready).ToArray();bool condition=members.Length>0&&members.Count(value=>value.trust<=10)*2>members.Length;if(!condition)State.mutinyLatched=false;return condition&&!State.mutinyLatched;}
        }
        public void PrepareBoundary()
        {
            if(AwaitingChoice||Running||game.Navigation.IsDocked||(State.current?.phase==EventPhase.Resolved&&!State.current.resultAcknowledged)||game.Prisoners.Pending.Length>0)return;
            if(MutinyDue){State.nextKind="mutiny";State.nextAt=game.ElapsedSeconds;}
            if(State.nextAt<0 || State.nextSeed==0)
            {
                uint seed=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0)|1u;
                State.nextAt=game.ElapsedSeconds+Mathf.Lerp(settings.minimumInterval,settings.maximumInterval,Next(ref seed))/(1+State.watchFailureBonus*.25f);
                State.nextKind=Pick(ref seed);
                State.nextSeed=seed;
                float radians=game.Navigation.Heading*Mathf.Deg2Rad;
                State.nextPosition=game.Navigation.WorldPosition+new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*(float)(State.nextAt-game.ElapsedSeconds)*Mathf.Max(1,game.State.navigation.speed);
                if(!game.Navigation.ValidWorldPosition(State.nextPosition))State.nextPosition=game.Navigation.WorldPosition;
                game.RequestSave();
            }
            if(MutinyDue){State.nextKind="mutiny";State.nextAt=game.ElapsedSeconds;}
            game.Loop.ScheduleEventBoundary(Math.Max(State.nextAt,game.ElapsedSeconds));
        }
        private string Pick(ref uint seed)
        {
            var available=settings.weights.Where(value=>value.kind!="watch"&&value.kind!="mutiny"&&value.weight>0).ToArray();
            if(available.Length==0)return "storm";
            float roll=Next(ref seed)*available.Sum(value=>value.weight);
            foreach(var value in available){roll-=value.weight;if(roll<=0)return value.kind;}
            return available[available.Length-1].kind;
        }
        public void BeginScheduled()
        {
            if(game.Navigation.IsDocked||AwaitingChoice||Running)return;
            if(State.sequence==int.MaxValue)throw new InvalidDataException("Event identity exhausted");
            uint seed=State.nextSeed==0?(BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0)|1u):State.nextSeed;
            string kind=MutinyDue?"mutiny":State.nextKind??Pick(ref seed);
            if(game.IsSleeping&&kind!="mutiny"&&Next(ref seed)<settings.watchProbability)kind="watch";
            if(kind=="watch")game.Rest.BeginWatch(settings.watchDuration,settings.patrolDistance);else game.Rest.Wake();
            game.Crew.CapturePositions();var checkpoint=SaveCoordinator.Capture(game.State);checkpoint.events=null;
            State.sequence++;
            State.current=new VoyageEventState{id=game.State.playthroughId+":event:"+State.sequence,kind=kind,initialSeed=seed,randomState=seed,startedAt=game.ElapsedSeconds,checkpoint=SaveCoordinator.EncodeSnapshot(checkpoint)};
            if(kind=="mutiny")State.mutinyLatched=true;
            State.nextAt=-1;State.nextKind=null;State.nextSeed=0;
            announcementPause=game.Pauses.Acquire("EventAnnouncement");game.RequestSave();game.NotifyChanged();
        }
        public EventOption[] Choices()
        {
            if(State.current.kind!="island")return new[]{new EventOption{id="continue",label="계속"}};
            return game.State.crew.members.Where(value=>value.availability==CrewAvailability.Ready&&value.health>0).Select(value=>new EventOption{id="dispatch:"+value.id,label=value.displayName+" 파견"}).Concat(new[]{new EventOption{id="leave",label="지나가기"}}).ToArray();
        }
        public void ContinueStorm()=>Choose("continue");
        public void Choose(string choice)
        {
            if(!AwaitingChoice||!Choices().Any(value=>value.id==choice))return;
            var current=State.current;current.choice=choice;current.phase=EventPhase.Running;
            announcementPause?.Dispose();announcementPause=null;
            if(current.kind=="pirates"||current.kind=="mutiny"||current.kind=="kraken")game.Battle.Begin();
            else if(current.kind=="rats"||current.kind=="crabs"||current.kind=="barnacles")game.Pests.Begin();
            else if(current.kind=="island")
            {
                if(choice=="leave"){Resolve("무인도를 지나갔습니다.");return;}
                current.dispatchedCrewId=choice.Substring(9);game.Crew.SetAvailability(current.dispatchedCrewId,CrewAvailability.Away);
            }
            game.RequestSave();game.NotifyChanged();
        }
        public void Advance(float seconds)
        {
            if(!Running)return;
            var current=State.current;
            if(current.kind=="pirates"||current.kind=="mutiny"||current.kind=="kraken"){game.Battle.Advance(seconds);return;}
            if(current.kind=="storm"){AdvanceStorm(seconds);return;}
            current.progressedSeconds+=seconds;
            if(current.kind=="watch")
            {
                if(game.Rest.State.mode!=PlayerRest.Watch || (game.Rest.State.watchRemaining<=0&&game.Rest.State.patrolRemaining>0))
                {
                    bool success=game.Rest.State.patrolRemaining<=0&&!game.Rest.State.missedWatch;
                    if(!success)State.watchFailureBonus=Mathf.Min(4,State.watchFailureBonus+1);
                    Resolve(success?"불침번 순찰을 마쳤습니다.":"순찰을 마치지 못해 다른 사건 가능성이 증가했습니다.");
                }
            }
            else if(current.kind=="island"&&current.progressedSeconds>=settings.dispatchDuration)
            {
                var crew=game.State.characters.Find(value=>value.id==current.dispatchedCrewId);if(crew!=null){if(game.Crew.FindMember(crew.id)!=null)game.Crew.SetAvailability(crew.id,CrewAvailability.Ready);else crew.availability=CrewAvailability.Ready;}
                bool success=Next(ref current.randomState)<.75f;
                int reward=success?Math.Min(settings.islandReward,int.MaxValue-game.State.inventory.Gold):0;game.State.inventory.Gold+=reward;
                Resolve(success?"파견 선원이 돌아와 "+reward+" 금화를 얻었습니다.":"파견 선원이 소득 없이 돌아왔습니다.");
            }
            else if(current.kind=="fish"&&current.progressedSeconds>=3)
            {
                int count=game.State.inventory.Get("fresh-fish");game.State.inventory.Set("fresh-fish",count+Math.Min(settings.fishYield,int.MaxValue-count));Resolve("선내에 생선이 떨어졌습니다. 식량을 대신합니다.");
            }
            else if(current.pests!=null)
            {
                game.Pests.Advance(seconds);
                if(game.Pests.Active.Length==0||current.progressedSeconds>=settings.pestDuration){current.pests.Clear();Resolve("유해 생물 사건이 끝났습니다.");}
            }
        }
        private void AdvanceStorm(float seconds)
        {
            var current=State.current;float consumed=Mathf.Min(seconds,(float)Math.Max(0,settings.stormDuration-current.progressedSeconds));
            foreach(var crew in game.State.crew.members)if(crew.floorId==settings.exposedFloor&&crew.availability==CrewAvailability.Ready)crew.health=Mathf.Max(0,crew.health-consumed*settings.healthDamagePerSecond);
            foreach(var facility in game.Ship.Facilities)if(facility.sailDeployed&&game.Ship.GetFacilityDefinition(facility.definitionId).role==FacilityRole.Sail)facility.durability=Mathf.Max(0,facility.durability-consumed*settings.sailDamagePerSecond);
            int previous=(int)current.progressedSeconds;current.progressedSeconds+=consumed;
            for(int tick=previous;tick<(int)current.progressedSeconds;tick++)game.Navigation.RotateByStorm(Mathf.Lerp(-settings.headingVariation,settings.headingVariation,Next(ref current.randomState)));
            if(current.progressedSeconds+.00001>=settings.stormDuration)Resolve("폭풍이 지나갔습니다.");
        }
        public void Resolve(string result)
        {
            var current=State.current;if(current==null||current.phase==EventPhase.Resolved)return;
            current.result=result;current.effectsApplied=true;current.phase=EventPhase.Resolved;current.checkpoint=null;
            game.Crew.StopPestWork();
            game.Journal.ObserveEvent(current.id,Title,game.Navigation.WorldPosition);game.RequestSave();game.NotifyChanged();
        }
        public static float Next(ref uint seed){seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;return(seed&0xffffffu)/16777216f;}
    }
}
