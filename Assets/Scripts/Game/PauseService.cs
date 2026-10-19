using System;
using System.Collections.Generic;

namespace GreatVoyage
{
    public sealed class PauseService
    {
        private readonly Dictionary<int, string> reasons = new Dictionary<int, string>();
        private int nextId;
        public bool IsPaused => reasons.Count > 0;
        public int Count => reasons.Count;
        public event Action Changed;

        // Role: 독립적인 정지 사유를 등록하고 해당 사유만 해제하는 핸들을 반환한다.
        public IDisposable Acquire(string reason)
        {
            int id = ++nextId;
            reasons.Add(id, reason);
            Changed?.Invoke();
            return new PauseHandle(this, id);
        }

        // Role: 지정된 사유만 제거하고 다른 정지 사유는 유지한다.
        private void Release(int id)
        {
            if (reasons.Remove(id)) Changed?.Invoke();
        }

        private sealed class PauseHandle : IDisposable
        {
            private PauseService owner;
            private readonly int id;

            // Role: 해제할 정지 사유의 소유권을 보관한다.
            public PauseHandle(PauseService owner, int id)
            {
                this.owner = owner;
                this.id = id;
            }

            // Role: 같은 핸들을 여러 번 해제해도 한 번만 사유를 제거한다.
            public void Dispose()
            {
                PauseService current = owner;
                owner = null;
                current?.Release(id);
            }
        }
    }
}
