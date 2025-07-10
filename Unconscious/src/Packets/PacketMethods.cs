using CompactExifLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Server;
using static Unconscious.UnconsciousModSystem;

namespace Unconscious.src.Packets
{
    public static class PacketMethods
    {
        public static void SendShowUnconciousScreenPacket(bool shouldShow, IServerPlayer targetPlayer, int unconsciousDuration = 0)
        {
            ShowUnconciousScreen responsePacket = new()
            {
                shouldShow = shouldShow,
                unconsciousTime = unconsciousDuration != 0 ? unconsciousDuration : UnconsciousModSystem.getConfig().UnconsciousDuration,
                chanceOfRevival = UnconsciousModSystem.getConfig().ChanceOfSelfRevival,
                enableSuicideButton = UnconsciousModSystem.getConfig().EnableSuicideButton,
                countdownSuicideButton = UnconsciousModSystem.getConfig().SuicideTimer,
            };

            UnconsciousModSystem.getSAPI().Network.GetChannel("unconscious").SendPacket(responsePacket, targetPlayer);
        }
        public static void SendShowFinishingOffPacket(string attackerPlayerUUID, string victimPlayerUUID, EnumDamageType damageType, bool shouldShow, IServerPlayer targetPlayer)
        {
            ShowPlayerFinishOffScreenPacket responsePacket = new()
            {
                attackerPlayerUUID = attackerPlayerUUID,
                victimPlayerUUID = victimPlayerUUID,
                damageType = damageType,
                shouldShow = shouldShow,
                finishTimer = UnconsciousModSystem.getConfig().FinishingTimer
            };

            UnconsciousModSystem.getSAPI().Network.GetChannel("unconscious").SendPacket(responsePacket, targetPlayer);
        }

        public static void SendAnimationPacketToClient(bool shouldPlay, string animationName, IServerPlayer targetPlayer)
        {
            PlayerAnimation responsePacket = new()
            {
                shouldPlay = shouldPlay,
                animationName = animationName,
            };

            UnconsciousModSystem.getSAPI().Network.GetChannel("unconscious").SendPacket(responsePacket, targetPlayer);
        }

        public static void SendUnconsciousPacket(bool isUnconscious)
        {
            SendUnconsciousPacket responsePacket = new()
            {
                isUnconscious = isUnconscious,
            };
            UnconsciousModSystem.getCAPI().Network.GetChannel("unconscious").SendPacket(responsePacket);
        }

        public static void SendPlayerDeathPacket()
        {
            PlayerDeath playerDeathPaacket = new() { };
            UnconsciousModSystem.getCAPI().Network.GetChannel("unconscious").SendPacket(playerDeathPaacket);
        }

        public static void SendPlayerRevivePacket()
        {

            PlayerRevive playerRevivePaacket = new() { };
            UnconsciousModSystem.getCAPI().Network.GetChannel("unconscious").SendPacket(playerRevivePaacket);
        }
    }
}
