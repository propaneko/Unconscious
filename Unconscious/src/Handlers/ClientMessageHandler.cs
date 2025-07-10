using Unconscious.src.Gui;
using Unconscious.src.Packets;
using Vintagestory.API.Client;
using Vintagestory.Server;

namespace Unconscious.src.Handlers
{
    internal class ClientMessageHandler
    {
        private ICoreClientAPI capi = UnconsciousModSystem.getCAPI();
        private BlackScreenOverlay dialogBlackScreen = null;
        private FinishOffOverlay dialogFinishOff = null;

        public void SetMessageHandlers()
        {
            capi.Network.GetChannel("unconscious").SetMessageHandler<ShowUnconciousScreen>(OnClientMessagesReceived);
            capi.Network.GetChannel("unconscious").SetMessageHandler<ShowPlayerFinishOffScreenPacket>(OnClientFinishedOffScreenReceived);
            capi.Network.GetChannel("unconscious").SetMessageHandler<PlayerAnimation>(OnPlayerAnimation);
        }

        private void OnClientMessagesReceived(ShowUnconciousScreen packet)
        {
            if (packet.shouldShow)
            {
                dialogBlackScreen = new BlackScreenOverlay(capi, packet);
                dialogBlackScreen.TryOpen();
                dialogBlackScreen.StartTimer();
                return;
            }

            if (!packet.shouldShow && dialogBlackScreen != null)
            {
                capi.World.Player.Entity.AnimManager.StopAnimation("sleep");
                dialogBlackScreen.StopTimer();
                dialogBlackScreen.TryClose();
                dialogBlackScreen = null;
                return;
            }
        }

        private void OnClientFinishedOffScreenReceived(ShowPlayerFinishOffScreenPacket packet)
        {
            if (packet.shouldShow)
            {
                dialogFinishOff = new FinishOffOverlay(capi, packet);
                dialogFinishOff.TryOpen();
                return;
            }

            if (!packet.shouldShow && dialogFinishOff != null)
            {
                dialogFinishOff.TryClose();
                dialogFinishOff = null;
                return;
            }
        }

        private void OnPlayerAnimation(PlayerAnimation packet)
        {
            if (packet.shouldPlay == true && capi.World.Player != null)
            {
                capi.World.Player.Entity.AnimManager.StartAnimation(packet.animationName);

                if (packet.animationName == "sleep")
                {
                    capi.World.Player.Entity.CollisionBox.Set(-0.3f, 0f, -0.9f, 0.3f, 0.3f, 0.9f);
                }

            }
            else
            {
                capi.World.Player.Entity.AnimManager.StopAnimation(packet.animationName);

                if (packet.animationName == "sleep")
                {
                    capi.World.Player.Entity.CollisionBox.Set(-0.3f, 0f, -0.3f, 0.3f, 1.8f, 0.3f);
                }
            }
        }
    }
}
