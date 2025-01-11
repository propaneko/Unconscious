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
        public static void HandleUnconscious(EntityPlayer player)
        {
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedProcess = true;
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedParticles = true;
        }
        public static void HandleRevive(EntityPlayer player)
        {
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedProcess = false;
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedParticles = false;
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
