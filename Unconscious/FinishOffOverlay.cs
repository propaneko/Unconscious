using NoticeBoard.Packets;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

namespace Unconscious
{
    public class FinishOffOverlay : GuiDialog
    {
        private readonly ShowPlayerFinishOffScreenPacket packet;
        private bool isButtonEnabled = false;
        private long callbackId;
        private GuiComposer composer;
        public FinishOffOverlay(ICoreClientAPI capi, ShowPlayerFinishOffScreenPacket packet) : base(capi)
        {
            this.packet = packet;
            Compose();
        }

        public override string ToggleKeyCombinationCode => null;

        static string GetRandomQuote(string[] quotes)
        {
            Random random = new Random();
            int index = random.Next(quotes.Length);
            return quotes[index];
        }
        private void Compose()
        {

            var rectH = capi.Render.FrameHeight;
            var rectW = capi.Render.FrameWidth;
            ElementBounds quoteTextBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, -100, 600, 50);

            ElementBounds buttonKillBound = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 150, 0, 200, 50);
            ElementBounds buttonSpareBound = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, -150, 0, 200, 50);


            ElementBounds bounds = ElementBounds.Fill.WithFixedPadding(0);

            string[] finishingQuotes = new string[]
            {
                "Your opponent lies before you, battered and broken. Their breaths come in shallow gasps, eyes flickering with the last embers of defiance. The choice is yours: deliver the final blow, or let the fates decide their end.",
                "You tower over your foe, their strength spent and their will shattered. The weight of your next action presses heavily on your soul. Mercy or finality—your decision will carve itself into the annals of this world.",
                "The clash of battle has left your opponent at your mercy. Their blood stains the ground, their body trembling in defeat. In this fleeting moment, you hold the power of life and death in your hands.",
                "Your adversary kneels before you, the fire in their eyes now a dying ember. Time seems to slow as the final blow hangs in the balance. Will you strike true, or leave them to their fate?",
                "The battlefield grows silent, save for the labored breathing of the one who fell before you. The weight of the moment bears down upon you—this is the turning point where paths diverge, and destinies are written in blood."
            };

            string randomQuote = GetRandomQuote(finishingQuotes);

            composer = SingleComposer = capi.Gui
             .CreateCompo("blackscreen", bounds)
             .AddStaticText(
                $"\"{randomQuote}\"",
                CairoFont.WhiteSmallText().WithFontSize(14).WithWeight(Cairo.FontWeight.Bold),
                quoteTextBounds,
                "quoteText"
            )
              .AddButton("Spare him", SpareHim, buttonSpareBound)
              .AddButton("Finish it", KillHim, buttonKillBound, EnumButtonStyle.Normal, "killButton")
              .Compose();
        }

        public bool KillHim()
        {
            PlayerKill playerKillPaacket = new() {
                attackerPlayerUUID = packet.attackerPlayerUUID,
                victimPlayerUUID = packet.victimPlayerUUID,
                damageType = packet.damageType
            };
            UnconsciousModSystem.getCAPI().Network.GetChannel("unconscious").SendPacket(playerKillPaacket);
            TryClose();
            return true;
        }

        public bool SpareHim()
        {
            TryClose();
            return true;
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
        }


        public override bool TryClose()
        {
            return base.TryClose();
        }

        public override bool TryOpen()
        {
            if (base.TryOpen())
            {
                isButtonEnabled = false;
                composer.GetButton("killButton").Enabled = false;
                composer.ReCompose();
                // Start a timer to enable the button after 3 seconds
                callbackId = capi.World.RegisterCallback((dt) =>
                {
                    isButtonEnabled = true;
                    composer.GetButton("killButton").Enabled = true;
                    composer.ReCompose();

                    capi.World.UnregisterCallback(callbackId);
                }, 3000);

                return true;
            }
            return false;
        }
    }
}
