using Vintagestory.API.Common;

namespace Unconscious.Player
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
    }
}
