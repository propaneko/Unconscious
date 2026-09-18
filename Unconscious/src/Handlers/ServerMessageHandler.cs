using Vintagestory.API.Server;
using Unconscious.Packets;
using Unconscious.Player;
using Vintagestory.API.Common;

namespace Unconscious.Handlers
{
    internal class ServerMessageHandler
    {
        private readonly ICoreServerAPI sapi = UnconsciousModSystem.getSAPI();

        public void SetMessageHandlers()
        {
            sapi.Network.GetChannel("unconscious").SetMessageHandler<SendUnconsciousPacket>(OnUnconsciousPacket);
            sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerDeath>(OnPlayerDeathPacket);
            sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerKill>(OnPlayerKill);
        }

        private void OnUnconsciousPacket(IServerPlayer player, SendUnconsciousPacket packet)
        {
            player.Entity.SetUnconscious(packet.isUnconscious);
            player.Entity.WatchedAttributes.MarkPathDirty("unconscious");
        }

        private void OnPlayerDeathPacket(IServerPlayer player, PlayerDeath packet)
        {
            player.Entity.SetUnconscious(false);
            player.Entity.WatchedAttributes.MarkPathDirty("unconscious");

            UnconsciousModSystem.unconsciousTimers.RemoveAll(timer => timer.PlayerUID == player.PlayerUID);

            player.Entity.Die(EnumDespawnReason.Death, new DamageSource
            {
                Type = EnumDamageType.Injury, // Set damage type
                Source = EnumDamageSource.Suicide
            });

            PacketMethods.SendAnimationPacketToClient(false, "sleep", player);
        }

        private void OnPlayerKill(IServerPlayer player, PlayerKill packet)
        {
            IServerPlayer attackingServerPlayer = sapi.World.PlayerByUid(packet.attackerPlayerUUID) as IServerPlayer;
            IServerPlayer victimServerPlayer = sapi.World.PlayerByUid(packet.victimPlayerUUID) as IServerPlayer;

            if (victimServerPlayer.Entity.IsUnconscious())
            {
                victimServerPlayer.Entity.SetUnconscious(false);
                victimServerPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");

                attackingServerPlayer.Entity.StartAnimation("finishingblow");
                victimServerPlayer.Entity.World.PlaySoundAt(new AssetLocation($"unconscious:sounds/finishingblow"), victimServerPlayer.Entity, null, false, 8, 1f);

                UnconsciousModSystem.unconsciousTimers.RemoveAll(timer => timer.PlayerUID == victimServerPlayer.PlayerUID);

                ShowUnconciousScreen responsePacket = new()
                {
                    shouldShow = false,
                    unconsciousTime = 0
                };

                sapi.Network.GetChannel("unconscious").SendPacket(responsePacket, victimServerPlayer);

                victimServerPlayer.Entity.Die(EnumDespawnReason.Death, new DamageSource
                {
                    Type = packet.damageType, // Set damage type
                    Source = EnumDamageSource.Entity,
                    SourceEntity = attackingServerPlayer.Entity,
                    CauseEntity = attackingServerPlayer.Entity,
                    
                });
                PacketMethods.SendAnimationPacketToClient(false, "sleep", victimServerPlayer);
            }
        }

    }
}
