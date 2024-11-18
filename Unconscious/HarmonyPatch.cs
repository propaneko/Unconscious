using HarmonyLib;
using NoticeBoard.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Unconscious
{
    public class PlayerPatch
    {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Entity), "ReceiveDamage")]
        public static bool ReceiveDamage(Entity __instance, ref DamageSource damageSource, ref float damage)
        {
            if (__instance.Api is ICoreServerAPI sapi)
            {
                if (__instance is EntityPlayer player)
                {
                    IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;
                    var health = player.WatchedAttributes.GetTreeAttribute("health");
                    var unconscious = player.WatchedAttributes.GetBool("unconscious");

                    float currentHealth = health.GetFloat("currenthealth");
                    float resultingHealth = currentHealth - damage;
                   
                    ShowPlayerFinishOffScreenPacket closeWindowPacket = new()
                    {
                        attackerPlayerUUID = serverPlayer.PlayerUID,
                        victimPlayerUUID = serverPlayer.PlayerUID,
                        damageType = damageSource.Type,
                        shouldShow = false
                    };
                    sapi.Network.GetChannel("unconscious").SendPacket(closeWindowPacket, serverPlayer);

                    if (resultingHealth <= 1 && !unconscious && serverPlayer.Entity.Alive)
                    {
                        if (damageSource.Type == EnumDamageType.BluntAttack ||
                            damageSource.Type == EnumDamageType.PiercingAttack ||
                            damageSource.Type == EnumDamageType.Suffocation ||
                            damageSource.Type == EnumDamageType.SlashingAttack ||
                            damageSource.Type == EnumDamageType.Crushing)
                        {
                            ShowUnconciousScreen responsePacket = new()
                            {
                                shouldShow = true,
                                unconsciousTime = UnconsciousModSystem.getConfig().UnconsciousDuration,
                            };

                            sapi.Network.GetChannel("unconscious").SendPacket(responsePacket, serverPlayer);

                            player.PlayEntitySound("hurt", null, randomizePitch: true, 24f);
                            player.WatchedAttributes.SetBool("unconscious", true);
                            health.SetFloat("currenthealth", 1);
                            player.WatchedAttributes.MarkPathDirty("unconscious");

                            serverPlayer.Entity.TryStopHandAction(forceStop: true, EnumItemUseCancelReason.Death);
                            serverPlayer.Entity.AnimManager.StartAnimation("sleep");
                            //serverPlayer.Entity.AnimManager.StartAnimation("sitflooridle");
                            return false;
                        }
                    }

                    if (resultingHealth <= 1 && unconscious && serverPlayer.Entity.Alive) { 
                        if  (
                            damageSource.Type == EnumDamageType.BluntAttack ||
                            damageSource.Type == EnumDamageType.PiercingAttack ||
                            damageSource.Type == EnumDamageType.SlashingAttack
                            )
                        {
                            if (damageSource.SourceEntity is EntityPlayer attackingPlayer)
                            {
                                serverPlayer.Entity.AnimManager.StartAnimation("sleep");
                                IServerPlayer attackingServerPlayer = sapi.World.PlayerByUid(attackingPlayer.PlayerUID) as IServerPlayer;

                                ShowPlayerFinishOffScreenPacket responsePacket = new()
                                {
                                    attackerPlayerUUID = attackingServerPlayer.PlayerUID,
                                    victimPlayerUUID = serverPlayer.PlayerUID,
                                    damageType = damageSource.Type,
                                    shouldShow = true
                                };
                                sapi.Network.GetChannel("unconscious").SendPacket(responsePacket, attackingServerPlayer);

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
