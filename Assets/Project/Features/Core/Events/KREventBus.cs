    using System;
using System.Collections.Generic;

namespace KillRitual.Core.Events
{
    /// <summary>
    /// 타입 세이프(Type-Safe)한 Pub/Sub 이벤트 브로커입니다.
    /// UI는 Player/Weapon을 직접 참조하지 않고 오직 이 버스를 통해서만 데이터를 구독해야 합니다.
    /// (No Direct References in UI 규칙) 이를 통해 Developer A(인프라/무기)와
    /// Developer B(플레이어 무브먼트) 사이의 Git 충돌을 최소화합니다.
    /// </summary>
    public sealed class KREventBus
    {
        // 이벤트 타입(Type)을 키로, 해당 타입을 구독하는 콜백 리스트를 값으로 가지는 딕셔너리입니다.
        // 콜백은 object로 박싱되어 저장되며, Publish 시점에 제네릭으로 안전하게 캐스팅됩니다.
        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();

        /// <summary>
        /// 특정 이벤트 타입(T)을 구독합니다. T는 GC 스파이크 방지를 위해 항상 struct여야 합니다.
        /// </summary>
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

        /// <summary>
        /// 구독을 해제합니다. MonoBehaviour를 구독자로 사용할 경우
        /// 반드시 OnDisable/OnDestroy에서 호출하여 메모리 누수를 방지해야 합니다.
        /// </summary>
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

        /// <summary>
        /// 이벤트를 발행(브로드캐스트)합니다.
        /// 구독자의 콜백 내부에서 다시 Subscribe/Unsubscribe가 호출되어도
        /// "Collection was modified" 예외가 발생하지 않도록, 순회 직전에 리스트를
        /// 배열로 스냅샷 복사한 뒤 그 복사본을 순회합니다.
        /// </summary>
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

        /// <summary>
        /// 씬 전환, 테스트 초기화 등에서 모든 구독 정보를 일괄 해제할 때 사용합니다.
        /// </summary>
        public void ClearAll()
        {
            _subscribers.Clear();
        }
    }
}
