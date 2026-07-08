// Assets/Project/Features/CombatZones/ZoneEntryRelay.cs
using UnityEngine;

namespace KillRitual.CombatZones
{
    [RequireComponent(typeof(Collider))]
    public class ZoneEntryRelay : MonoBehaviour
    {
        [SerializeField] private WaveCombatZone _zone;

        private void Reset()
        {
            _zone = GetComponentInParent<WaveCombatZone>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_zone == null)
            {
                Debug.LogWarning($"[ZoneEntryRelay] {name}: 연결된 WaveCombatZone이 없습니다.");
                return;
            }

            _zone.NotifyEntryTriggered(other);
        }
    }
}