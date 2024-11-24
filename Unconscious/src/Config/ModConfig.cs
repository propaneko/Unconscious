
using System.Collections.Generic;

namespace Unconscious.src.Config
{
    public class ModConfig
    {
        public int UnconsciousDuration = 300;
        public int FinishingTimer = 3;


        public float RevivePerTickDuration = 0.2f;
        public float PickupPerTickDuration = 1f;

        public float MaxHealthPercentAfterRevive = 0.15f;
        public float ChanceOfSelfRevival = 0.0f;

        public bool EnableCarryMechanic = true;
        public bool EnableSuicideButton = true;

        public bool DropWeaponOnUnconscious = true;

        public string UnconsciousCmdPrivilege = "ban";
        public string ReviveCmdPrivilege = "ban";
    }
}