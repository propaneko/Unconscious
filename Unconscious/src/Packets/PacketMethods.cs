using Vintagestory.API.Server;

namespace Unconscious.src.Packets
{
    public static class PacketMethods
    {
        public static void SendShowUnconciousScreenPacket(bool shouldShow, int unconsciousTime, IServerPlayer targetPlayer)
        {
            ShowUnconciousScreen responsePacket = new()
            {
                shouldShow = shouldShow,
                unconsciousTime = unconsciousTime
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
    }
}
