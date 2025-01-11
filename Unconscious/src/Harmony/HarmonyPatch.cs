using HarmonyLib;
using System;
using Unconscious.src.Compat;
using Unconscious.src.Packets;
using Unconscious.src.Player;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious.src.Harmony
{
    public class PlayerPatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EntityPlayer), "OnHurt")]
        public static bool OnHurt(Entity __instance, ref DamageSource damageSource, ref float damage)
        {
            if (__instance.Api is ICoreServerAPI sapi)
            {
                if (__instance is EntityPlayer player)
                {

                    IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

                    if (player.Attributes.GetLong(BSCompat.ReviveCallbackAttr, -1) >= 0)
                    {
                        player.Api.Event.UnregisterCallback(player.Attributes.GetLong(BSCompat.ReviveCallbackAttr));
                    }

                    if (serverPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
                    {
                        return true;
                    }
                    var health = player.WatchedAttributes.GetTreeAttribute("health");
                    var unconscious = player.WatchedAttributes.GetBool("unconscious");

                    float currentHealth = health.GetFloat("currenthealth");

                    //sapi.Logger.Debug($"[unconscious] damage: {damage}, resultingHealth: {currentHealth}");
                   
                    if (sapi != null) 
                    {
                        PacketMethods.SendShowFinishingOffPacket(serverPlayer.PlayerUID, serverPlayer.PlayerUID, damageSource.Type, false, serverPlayer);
                    }

                    player.WatchedAttributes.SetBool("carryingUnconciousPlayer", false);
                    player.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");

                    //sapi.Logger.Debug($"unconscious: {unconscious}, enityAlive?: {serverPlayer.Entity.Alive}");

                    if (currentHealth <= 1 && !unconscious && serverPlayer.Entity.Alive)
                    {
                        Enum.TryParse(damageSource.Type.ToString(), out EnumDamageType damageType);
                        var checkEnabledDamageTypes =  UnconsciousModSystem.getConfig().EnabledDamageTypes.Contains(damageType.ToString());
  
                        if (checkEnabledDamageTypes)
                        {
                            UnconsciousModSystem.HandlePlayerUnconscious(player);
                            return false;
                        }
                    }

                    if (unconscious && serverPlayer.Entity.Alive)
                    {
                        health.SetFloat("currenthealth", 1);
                        if (
                            damageSource.Type == EnumDamageType.BluntAttack ||
                            damageSource.Type == EnumDamageType.PiercingAttack ||
                            damageSource.Type == EnumDamageType.SlashingAttack
                            )
                        {
                            serverPlayer.Entity.AnimManager.StartAnimation("sleep");
                            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);

                            if (damageSource.SourceEntity is EntityPlayer attackingPlayer)
                            {
                                IServerPlayer attackingServerPlayer = sapi.World.PlayerByUid(attackingPlayer.PlayerUID) as IServerPlayer;
                                PacketMethods.SendShowFinishingOffPacket(attackingServerPlayer.PlayerUID, serverPlayer.PlayerUID, damageSource.Type, true, attackingServerPlayer);
                                return false;
                            }
                            return false;
                        }
                        return false;
                    }
                }
                return true;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AiTaskBaseTargetable), "IsTargetableEntity")]
        public static bool ShouldCancelTarget(Entity e, ref bool __result)
        {
            if (e != null && e is EntityPlayer && e.Code != null && e.Code.Path != null && e.Code.Path.StartsWith("player"))
            {
                EntityPlayer entityPlayer = (EntityPlayer)e;
                if (entityPlayer != null)
                {
                    IServerPlayer serverPlayer = entityPlayer.Player as IServerPlayer;
                    if (serverPlayer != null)
                    {
                            
                        if (serverPlayer.Entity.IsUnconscious())
                        {
                            __result = false;
                            return false;
                        }
                        return true;
                    }
                }
            }
            return true;
        }
    }
}
