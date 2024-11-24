using HarmonyLib;
using Unconscious.src.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

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

                    if (serverPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
                    {
                        return true;
                    }
                    var health = player.WatchedAttributes.GetTreeAttribute("health");
                    var unconscious = player.WatchedAttributes.GetBool("unconscious");

                    float currentHealth = health.GetFloat("currenthealth");
                    float resultingHealth = currentHealth - damage;

                    //sapi.Logger.Debug($"[unconscious] currentHealth: {currentHealth}, damage: {damage}, resultingHealth: {resultingHealth}");
                   
                    if (sapi != null)
                    {
                        PacketMethods.SendShowFinishingOffPacket(serverPlayer.PlayerUID, serverPlayer.PlayerUID, damageSource.Type, false, serverPlayer);
                    }

                    player.WatchedAttributes.SetBool("carryingUnconciousPlayer", false);
                    player.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");
                    player.AnimManager.ActiveAnimationsByAnimCode.Clear();

                    //sapi.Logger.Debug($"unconscious: {unconscious}, enityAlive?: {serverPlayer.Entity.Alive}");

                    if (resultingHealth <= 1 && !unconscious && serverPlayer.Entity.Alive)
                    {
                        if (damageSource.Type == EnumDamageType.BluntAttack ||
                            damageSource.Type == EnumDamageType.PiercingAttack ||
                            damageSource.Type == EnumDamageType.Suffocation ||
                            damageSource.Type == EnumDamageType.SlashingAttack ||
                            damageSource.Type == EnumDamageType.Crushing)
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
    }
}
