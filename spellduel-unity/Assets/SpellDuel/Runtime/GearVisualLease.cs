using System;
using UnityEngine;
namespace SpellDuel
{
    // Suspend appearance while a parent is inactive; release only when the registered lease is removed.
    public sealed class GearVisualLease:MonoBehaviour
    {
        internal Action Restore,Apply;
        internal void Suspend(){Restore?.Invoke();}
        internal void Release(){var action=Restore;Restore=null;Apply=null;action?.Invoke();}
        void OnEnable(){Apply?.Invoke();}
        void OnDisable(){if(gameObject.activeSelf)Suspend();else Release();}
        void OnDestroy(){Release();}
    }
}
