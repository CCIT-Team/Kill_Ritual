using UnityEngine;
using KillRitual.Core.Events;
using KillRitual.Core.SaveData;

namespace KillRitual.Core.Managers
{
    public sealed partial class KRManagers : MonoBehaviour
    {
        public static KREventBus Event { get; private set; }

        public static KRFileManager File { get; private set; }

        public static KRCombatRegistry Combat { get; private set; }

        private void InitCore()
        {
            Event  = new KREventBus();
            File   = new KRFileManager();
            Combat = new KRCombatRegistry();
        }
    }
}
