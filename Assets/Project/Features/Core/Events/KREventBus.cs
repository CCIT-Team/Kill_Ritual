    using System;
using System.Collections.Generic;

namespace KillRitual.Core.Events
{
    public sealed class KREventBus
    {
        // 이벤트 타입을 키로, 구독 콜백 리스트를 값으로 가지며 Publish 시점에 제네릭으로 캐스팅합니다.
        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();

        public void Subscribe<T>(Action<T> callback) where T : struct
        {
            if (callback == null)
            {
                return;
            }

            Type eventType = typeof(T);

            if (!_subscribers.TryGetValue(eventType, out List<object> handlers))
            {
                handlers = new List<object>();
                _subscribers[eventType] = handlers;
            }

            handlers.Add(callback);
        }

        public void Unsubscribe<T>(Action<T> callback) where T : struct
        {
            if (callback == null)
            {
                return;
            }

            Type eventType = typeof(T);

            if (_subscribers.TryGetValue(eventType, out List<object> handlers))
            {
                handlers.Remove(callback);
            }
        }

        public void Publish<T>(T eventData) where T : struct
        {
            Type eventType = typeof(T);

            if (!_subscribers.TryGetValue(eventType, out List<object> handlers) || handlers.Count == 0)
            {
                return;
            }

            // 순회 중 컬렉션 변경 예외 방지를 위한 스냅샷(얕은 복사) 배열
            object[] snapshot = handlers.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is Action<T> typedCallback)
                {
                    typedCallback.Invoke(eventData);
                }
            }
        }

        public void ClearAll()
        {
            _subscribers.Clear();
        }
    }
}
