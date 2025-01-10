using Cairo;
using System;
using Unconscious.src.Packets;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Unconscious.src.Gui
{
    public class FinishOffOverlay : GuiDialog
    {
        private readonly ShowPlayerFinishOffScreenPacket packet;
        private bool isButtonEnabled = false;
        private long callbackId;
        private long countdownId;
        private int killcountdown;
        private GuiComposer composer;
        public FinishOffOverlay(ICoreClientAPI capi, ShowPlayerFinishOffScreenPacket packet) : base(capi)
        {
            this.packet = packet;
            this.killcountdown = packet.finishTimer;
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
                 Lang.Get("unconscious:finishing-quote-1"),
                 Lang.Get("unconscious:finishing-quote-2"),
                 Lang.Get("unconscious:finishing-quote-3"),
                 Lang.Get("unconscious:finishing-quote-4"),
                 Lang.Get("unconscious:finishing-quote-5"),
            };

            string randomQuote = GetRandomQuote(finishingQuotes);

            composer = SingleComposer = capi.Gui
             .CreateCompo("blackscreen", bounds)
             .AddStaticCustomDraw(bounds, DrawBlackRectangle)

             .AddStaticText(
                $"\"{randomQuote}\"",
                CairoFont.WhiteSmallText().WithFontSize(14).WithWeight(Cairo.FontWeight.Bold),
                quoteTextBounds,
                "quoteText"
            )
              .AddButton(Lang.Get("unconscious:finishing-button-spare"), SpareHim, buttonSpareBound)
              .AddButton(killcountdown > 0 ? packet.finishTimer.ToString() : Lang.Get("unconscious:finishing-button-kill"), KillHim, buttonKillBound, EnumButtonStyle.Normal, "killButton")
              .Compose();
        }

        private void DrawBlackRectangle(Context context, ImageSurface surface, ElementBounds bounds)
        {
            context.SetSourceRGBA(0, 0, 0, 0.40);
            context.Rectangle(bounds.drawX, bounds.drawY, bounds.OuterWidth, bounds.OuterHeight);
            context.Fill();
        }

        public bool KillHim()
        {
            PlayerKill playerKillPaacket = new()
            {
                attackerPlayerUUID = packet.attackerPlayerUUID,
                victimPlayerUUID = packet.victimPlayerUUID,
                damageType = packet.damageType
            };
            UnconsciousModSystem.getCAPI().Network.GetChannel("unconscious").SendPacket(playerKillPaacket);
            capi.World.Player.Entity.AnimManager.StartAnimation("finishingblow");
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
                countdownId = capi.Event.RegisterGameTickListener((dt) =>
                {
                    killcountdown--;
                    composer.GetButton("killButton").Text = killcountdown.ToString();
                    composer.ReCompose();
                    if (killcountdown <= 0)
                    {
                        composer.GetButton("killButton").Enabled = true;
                        composer.GetButton("killButton").Text = Lang.Get("unconscious:finishing-button-kill");
                        composer.ReCompose();
                        capi.World.UnregisterGameTickListener(countdownId);
                    }
                }, 1000);
                return true;
            }
            return false;
        }
    }
}
