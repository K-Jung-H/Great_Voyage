using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GreatVoyage
{
    public sealed class JournalSession : MonoBehaviour
    {
        [SerializeField] private GameSession session;
        [SerializeField] private NavigationSession navigation;
        public JournalState State { get; private set; }
        public string Message { get; private set; }
        public int Revision { get; private set; }
        public JournalVoyage Current => State?.voyages.LastOrDefault(v => v.sequence == navigation.VoyageSequence);
        public bool AutopilotRequested => State != null && State.autopilot && State.planIndex < State.plan.Count;

        // Role: 일지 상태를 검증하고 항해 단위 기록을 복원한다.
        public JournalState Initialize(JournalState saved)
        {
            if (session == null || navigation == null) throw new InvalidDataException("일지 참조가 누락되었습니다.");
            State = saved ?? new JournalState();
            if (State.voyages == null || State.plan == null || State.pending == null || State.pendingEvents == null
                || State.planIndex < 0 || State.planIndex > State.plan.Count || State.plan.Count > 512 || State.voyages.Count > 1000)
                throw new InvalidDataException("일지 저장 상태가 유효하지 않습니다.");
            var sequences = new System.Collections.Generic.HashSet<int>();
            foreach (Vector2 point in State.plan) CheckPosition(point);
            foreach (JournalVoyage voyage in State.voyages)
            {
                if (voyage == null || voyage.sequence < 1 || voyage.sequence > navigation.VoyageSequence || !sequences.Add(voyage.sequence)
                    || !navigation.HasPort(voyage.departurePort) || (!string.IsNullOrEmpty(voyage.arrivalPort) && !navigation.HasPort(voyage.arrivalPort))
                    || voyage.plan == null || voyage.recorded == null || voyage.events == null || voyage.notes == null)
                    throw new InvalidDataException("항해 일지의 항해 단위가 유효하지 않습니다.");
                foreach (Vector2 point in voyage.plan) CheckPosition(point);
                foreach (JournalPoint point in voyage.recorded) CheckPoint(point);
                foreach (JournalEvent item in voyage.events) CheckEvent(item);
                foreach (JournalNote note in voyage.notes)
                { if (note == null || note.text == null || note.text.Length > 500) throw new InvalidDataException("메모가 유효하지 않습니다."); CheckPosition(note.position); }
            }
            foreach (JournalPoint point in State.pending) CheckPoint(point);
            foreach (JournalEvent item in State.pendingEvents) CheckEvent(item);
            if (saved == null && !navigation.IsDocked) BeginVoyage();
            if (!navigation.IsDocked && Current == null) throw new InvalidDataException("현재 항해의 일지 단위가 없습니다.");
            return State;
        }

        // Role: 출항 직후 고유 항해 순번과 계획을 일지에 연결한다.
        public void BeginVoyage()
        {
            if (Current != null) return;
            State.voyages.Add(new JournalVoyage { sequence = navigation.VoyageSequence, departurePort = navigation.DeparturePort,
                plan = new System.Collections.Generic.List<Vector2>(State.plan) });
            State.pending.Clear(); State.pendingEvents.Clear(); State.gap = true; State.carryEligible = false; State.hasRecorded = false;
            State.planIndex = 0; Revision++;
        }

        // Role: 입항한 항해를 닫고 취침 이후에도 확정 기록을 보존한다.
        public void Arrive(string port)
        {
            AppendEndpoint();
            if (Current != null) Current.arrivalPort = port;
            State.autopilot = false; Revision++;
        }

        // Role: 실제 이동을 일자·공백·취침 이월 자격과 함께 임시 누적한다.
        public void ObserveMovement(Vector2 from, Vector2 to, int day, bool sleeping)
        {
            if (Current == null || from == to) return;
            if (State.pending.Count == 0) State.pending.Add(new JournalPoint { position = from, day = day, carry = sleeping, breakBefore = State.gap });
            JournalPoint last = State.pending[State.pending.Count - 1];
            if (last.day != day || last.carry != sleeping || Vector2.Distance(last.position, to) >= .35f)
                State.pending.Add(new JournalPoint { position = to, day = day, carry = sleeping });
            State.gap = false;
        }

        // Role: 해당 일자의 미기록 경로와 이월 경로·사건을 확정 기록에 붙인다.
        public void Record()
        {
            if (!session.IsReady || Current == null) { Message = "첫 출항 후 경로를 기록할 수 있습니다."; return; }
            AppendEndpoint();
            int day = (int)(session.ElapsedSeconds / GameSession.SecondsPerDay) + 1;
            bool gap = false;
            foreach (JournalPoint point in State.pending)
            {
                if (point.day != day && !point.carry) { gap = true; continue; }
                point.breakBefore |= gap; gap = false; Current.recorded.Add(point);
            }
            Current.events.AddRange(State.pendingEvents.Where(item => item.day == day));
            State.pending.Clear(); State.pendingEvents.Clear();
            State.hasRecorded = true; State.carryEligible = true; State.gap = gap;
            Message = "현재까지의 기록을 확정했습니다."; Revision++; session.SaveCurrent();
        }

        // Role: 샘플 간격 사이의 마지막 실제 위치도 기록 시점에 포함한다.
        private void AppendEndpoint()
        {
            if (Current == null) return;
            JournalPoint last = State.pending.LastOrDefault();
            if (last != null && Vector2.Distance(last.position, navigation.WorldPosition) < .001f) return;
            State.pending.Add(new JournalPoint { position = navigation.WorldPosition,
                day = last?.day ?? (int)(session.ElapsedSeconds / GameSession.SecondsPerDay) + 1,
                carry = last?.carry ?? false, breakBefore = last == null && State.gap });
        }

        // Role: 기록 이후 상호작용이나 사건이 발생하면 취침 전 경로 이월 자격을 해제한다.
        public void MarkInteraction() { if (State != null) State.carryEligible = false; }

        // Role: 취침 전 임시 사건은 제거하고 조건을 만족한 경로만 다음 기록으로 이월한다.
        public void PrepareSleep(int nextDay, bool returningFromWatch = false)
        {
            State.pendingEvents.Clear();
            if (returningFromWatch || (State.hasRecorded && State.carryEligible))
                foreach (JournalPoint point in State.pending) { point.carry = true; point.day = nextDay; }
            else { State.pending.Clear(); State.gap = true; }
            State.carryEligible = false; Revision++;
        }

        // Role: 실제 사건 시스템에서 전달한 위치·안정 ID만 기록 후보로 등록한다.
        public void ObserveEvent(string id, string title, Vector2 position)
        {
            if (Current == null || string.IsNullOrEmpty(id) || State.pendingEvents.Any(e => e.id == id) || Current.events.Any(e => e.id == id)) return;
            CheckPosition(position); MarkInteraction();
            State.pendingEvents.Add(new JournalEvent { id = id, title = title ?? "사건", position = position,
                day = (int)(session.ElapsedSeconds / GameSession.SecondsPerDay) + 1 }); Revision++;
        }

        // Role: 사용자 계획점을 현재 계획과 해당 항해 기록에 추가한다.
        public void AddPlanPoint(Vector2 point)
        {
            if (!session.IsReady || !navigation.ValidWorldPosition(point) || State.plan.Count >= 512) return;
            if (State.plan.Count > 0 && Vector2.Distance(State.plan.Last(), point) < .5f) return;
            State.plan.Add(point); State.planIndex = 0;
            if (Current != null && !navigation.IsDocked) Current.plan = new System.Collections.Generic.List<Vector2>(State.plan);
            MarkInteraction(); Revision++;
        }

        // Role: 현재 계획만 지우고 과거 완료 항해의 계획은 유지한다.
        public void ClearPlan()
        {
            State.plan.Clear(); State.planIndex = 0; State.autopilot = false;
            if (Current != null && !navigation.IsDocked) Current.plan.Clear();
            MarkInteraction(); Revision++; session.SaveCurrent();
        }

        // Role: 계획 항로의 자동 조타 요청을 전환한다. 출항은 별도 행동이다.
        public void ToggleAutopilot()
        {
            if (!session.IsReady || State.plan.Count == 0) { Message = "계획선을 먼저 작성하세요."; return; }
            State.autopilot = !State.autopilot;
            if (State.autopilot && State.planIndex >= State.plan.Count) State.planIndex = 0;
            MarkInteraction(); Revision++; session.SaveCurrent();
        }

        // Role: 지나온 중간 목적지를 넘기고 다음 자동 조타 방향을 제공한다.
        public bool TryGetHeading(Vector2 position, out float heading)
        {
            while (State.planIndex < State.plan.Count && Vector2.Distance(position, State.plan[State.planIndex]) < .8f) State.planIndex++;
            heading = 0;
            if (!AutopilotRequested) { State.autopilot = false; return false; }
            Vector2 delta = State.plan[State.planIndex] - position;
            heading = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360); return true;
        }

        // Role: 지도 좌표의 사용자 메모를 현재 항해에 저장한다.
        public void AddNote(Vector2 position, string text)
        {
            if (Current == null || string.IsNullOrWhiteSpace(text) || text.Length > 500) return;
            if (!navigation.ValidWorldPosition(position)) { Message = "해역 안의 위치를 선택하세요."; return; }
            CheckPosition(position); Current.notes.Add(new JournalNote { position = position, text = text.Trim() });
            MarkInteraction(); Revision++; session.SaveCurrent();
        }

        // Role: 확정된 사건 아이콘만 제거한다. 실제 사건 결과는 변경하지 않는다.
        public void RemoveEvent(JournalVoyage voyage, string id)
        {
            if (voyage == null || !State.voyages.Contains(voyage)) return;
            voyage.events.RemoveAll(item => item.id == id); MarkInteraction(); Revision++; session.SaveCurrent();
        }

        // Role: 로드한 좌표가 실제 고정 해역 안인지 확인한다.
        private void CheckPosition(Vector2 point) { if (!navigation.ValidWorldPosition(point)) throw new InvalidDataException("일지 위치가 해역 밖입니다."); }
        // Role: 로드한 경로 지점의 날짜와 좌표를 확인한다.
        private void CheckPoint(JournalPoint point) { if (point == null || point.day < 1) throw new InvalidDataException("일지 경로가 유효하지 않습니다."); CheckPosition(point.position); }
        // Role: 로드한 사건의 안정 식별자·날짜와 좌표를 확인한다.
        private void CheckEvent(JournalEvent item) { if (item == null || string.IsNullOrEmpty(item.id) || item.day < 1 || string.IsNullOrEmpty(item.title)) throw new InvalidDataException("일지 사건이 유효하지 않습니다."); CheckPosition(item.position); }
    }
}
