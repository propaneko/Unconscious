using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious
{
    public static class PlayerExtensions
    {
        public static void SetUnconscious(this EntityPlayer player, bool isUnconscious)
        {
            // Add or update the "unconscious" attribute
            player.WatchedAttributes.SetBool("unconscious", isUnconscious);
        }

        public static bool IsUnconscious(this EntityPlayer player)
        {
            // Retrieve the "unconscious" attribute
            return player.WatchedAttributes.GetBool("unconscious", false);
        }

        public static void PickUpPlayer(this EntityPlayer player)
        {
            // Retrieve the "unconscious" attribute
           
            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");

            var health = player.WatchedAttributes.GetTreeAttribute("health");
            var maxHealth = health.GetFloat("maxHealth");
            health.SetFloat("currenthealth", 5);

            PacketMethods.SendShowUnconciousScreenPacket(false, player as IServerPlayer);
        }
    }
}
