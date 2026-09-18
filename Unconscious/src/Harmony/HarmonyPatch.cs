using HarmonyLib;
using System;
using Unconscious.Compat;
using Unconscious.Packets;
using Unconscious.Player;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious.HarmonyPatches
{
    public class PlayerPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityPlayer), "OnHurt")]
        public static bool OnHurt(EntityPlayer __instance, ref DamageSource damageSource, ref float damage)
        {
            if (__instance.Api is not ICoreServerAPI sapi) return true;
            if (__instance.Player is not IServerPlayer serverPlayer) return true;

            if (__instance.Attributes.GetLong(BSCompat.ReviveCallbackAttr, -1) >= 0)
            {
                __instance.Api.Event.UnregisterCallback(__instance.Attributes.GetLong(BSCompat.ReviveCallbackAttr));
                if (__instance.Api.ModLoader.GetMod("bloodystory") != null)
                {
                    BSCompat.RestartBleeding(__instance);
                }
                else
                {
                    __instance.Attributes.SetLong(BSCompat.ReviveCallbackAttr, -1);
                }
            }

            if (serverPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival) return true;

            var healthTree = __instance.WatchedAttributes.GetTreeAttribute("health");
            var isUnconscious = __instance.WatchedAttributes.GetBool("unconscious");
            float currentHealth = healthTree.GetFloat("currenthealth");

            PacketMethods.SendShowFinishingOffPacket(serverPlayer.PlayerUID, serverPlayer.PlayerUID, damageSource.Type, false, serverPlayer);

            __instance.WatchedAttributes.SetBool("carryingUnconciousPlayer", false);
            __instance.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");

            if ((currentHealth - damage) <= 0 && !isUnconscious && __instance.Alive)
            {
                bool isEnabledDamageType = UnconsciousModSystem.getConfig().EnabledDamageTypes.Contains(damageSource.Type.ToString());

                if (isEnabledDamageType)
                {
                    long draggerId = damageSource.SourceEntity?.EntityId ?? 0;
                    __instance.WatchedAttributes.SetLong("draggerEntityId", draggerId);
                    __instance.WatchedAttributes.MarkPathDirty("draggerEntityId");

                    UnconsciousModSystem.HandlePlayerUnconscious(__instance, damageSource);
                    return false;
                }
            }

            if (isUnconscious && __instance.Alive)
            {
                healthTree.SetFloat("currenthealth", 1);

                if (damageSource.Type == EnumDamageType.BluntAttack ||
                    damageSource.Type == EnumDamageType.PiercingAttack ||
                    damageSource.Type == EnumDamageType.SlashingAttack)
                {
                    __instance.AnimManager.StartAnimation("sleep");
                    PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);

                    if (damageSource.SourceEntity is EntityPlayer attackingPlayer &&
                        sapi.World.PlayerByUid(attackingPlayer.PlayerUID) is IServerPlayer attackingServerPlayer)
                    {
                        PacketMethods.SendShowFinishingOffPacket(attackingServerPlayer.PlayerUID, serverPlayer.PlayerUID, damageSource.Type, true, attackingServerPlayer);
                    }
                }
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AiTaskBaseTargetable), "IsTargetableEntity")]
        public static bool ShouldCancelTarget(Entity e, ref bool __result)
        {
            if (e is EntityPlayer player && player.IsUnconscious())
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AiTaskBaseTargetable), "CanSensePlayer")]
        public static bool CanSensePlayer(EntityPlayer eplr, ref bool __result)
        {
            if (eplr != null && eplr.IsUnconscious())
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityBehaviorPlayerRevivable), "AttemptRevive")]
        public static bool AttemptRevive(Entity __instance)
        {
            return false;
        }
    }
}