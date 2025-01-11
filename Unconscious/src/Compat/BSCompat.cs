using BloodyStory;
using Vintagestory.API.Common;

namespace Unconscious.src.Compat
{
    internal static class BSCompat
    {
        public const string ReviveCallbackAttr = "reviveCallbackID";
        public static void HandleUnconscious(EntityPlayer player)
        {
            if (player.Attributes.GetLong(ReviveCallbackAttr, -1) >= 0)
            {
                player.Api.Event.UnregisterCallback(player.Attributes.GetLong(ReviveCallbackAttr));
            }
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedProcess = true;
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedParticles = true;
        }
        public static void HandleRevive(EntityPlayer player)
        {
            player.Attributes.SetLong(ReviveCallbackAttr,
            player.Api.Event.RegisterCallback((float obj) =>
            {
                RestartBleeding(player);
            }, UnconsciousModSystem.getConfig().GracePeriod));
        }
        public static void RestartBleeding(EntityPlayer player)
        {
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedProcess = false;
            player.GetBehavior<EntityBehaviorBleed>().pauseBleedParticles = false;
            player.Attributes.SetLong(ReviveCallbackAttr, -1);
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
