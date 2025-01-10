
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Unconscious.src.Config
{
    public class ModConfig
    { 
        public List<string> EnabledDamageTypes { get; set; }

        //public string UnconsciousDurationDescription = "timer in seconds, while unconscious after it reaches 0 it will kill you";
        public int UnconsciousDuration = 300;

        //public string FinishingTimerDescription = "timer in seconds, after it reaches 0 the finish button will be enabled";
        public int FinishingTimer = 3;

        //public string SuicideTimerDescription = "timer in seconds, after it reaches 0 the suicide button will be enabled";
        public int SuicideTimer = 0;

        //public string RevivePerTickDurationDescription = "this is value per tick while holding the (shift + right click) on player, the lower it is the slower the revive is";
        public float RevivePerTickDuration = 0.2f;

        //public string PickupPerTickDurationDescription = "this is value per tick while holding the (shift + right click) on player, the lower it is the slower the revive is";
        public float PickupPerTickDuration = 1f;

        //public string MaxHealthPercentAfterReviveDescription = "heal from max health that player will get in percent value. 1 = 100%, 0.5 = 50%, 0.15 = 15% etc...";
        public float MaxHealthPercentAfterRevive = 0.15f;

        //public string ChanceOfSelfRevivalDescription = "chance of self revive in percent value. 1 = 100%, 0.5 = 50%, 0.15 = 15% etc...";
        public float ChanceOfSelfRevival = 0.0f;

        //public string EnableCarryMechanicDescription = "enable or disable the carry mechanic of other player";
        public bool EnableCarryMechanic = true;

        //public string EnableSuicideButtonDescription = "enable or disable the displaying of suicide button";
        public bool EnableSuicideButton = true;

        //public string DropWeaponOnUnconsciousDescription = "enable or disable player dropping the item he held in hand while getting in the unconscious status";
        public bool DropWeaponOnUnconscious = false;

        //public string DropWeaponOnUnconsciousDescription = "enable or disable requirement of holding temporal gear when reviving";
        public bool RequireTemporalGearForRevive = false;

        //public string UnconsciousCmdPrivilegeDescription = "needed privilage to use the /unconscious command";
        public string UnconsciousCmdPrivilege = "ban";

        //public string ReviveCmdPrivilegeDescription = "needed privilage to use the /revive command";
        public string ReviveCmdPrivilege = "ban";

        //public string EnabledDamageTypesDescription;
    }
}