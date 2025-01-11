using BloodyStory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace Unconscious.src.Compat
{
    internal static class BSCompat
    {
        public static void ToggleBleeding(EntityPlayer player, bool toggle)
        {
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedProcess = toggle;
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedParticles = toggle;
        }

        public static void AddOnBleedoutEH(EntityPlayer player)
        {
            player.GetBehavior<EntityBehaviorBleed>().OnBleedout += (out bool shouldDie, DamageSource lastHit) =>
            {
                shouldDie = false;
                UnconsciousModSystem.HandlePlayerUnconscious(player);
            };
        }
    }
}
