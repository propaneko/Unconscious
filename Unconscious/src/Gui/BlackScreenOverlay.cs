using System;
using Unconscious.src.Packets;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Unconscious.src.Gui
{
    public class BlackScreenOverlay : GuiDialog
    {
        private int remainingTime;
        private GuiElementStaticText timerTextElement;
        private bool isMovementDisabled = false;
        private long updateTimerId;
        private long disablePlayerMovementId;

        public BlackScreenOverlay(ICoreClientAPI capi, int durationInSeconds) : base(capi)
        {
            remainingTime = durationInSeconds;
            Compose();
        }

        public override string ToggleKeyCombinationCode => null; // Not toggleable manually

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
            ElementBounds textBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, 0, 400, 50);
            ElementBounds quoteTextBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, -100, 600, 50);

            ElementBounds buttonBound = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, 0, 400, 50);

            ElementBounds bounds = ElementBounds.Fill.WithFixedPadding(0);

            string[] quotes = new string[]
            {
                Lang.Get("unconscious:unconscious-quote-1"),
                Lang.Get("unconscious:unconscious-quote-2"),
                Lang.Get("unconscious:unconscious-quote-3"),
                Lang.Get("unconscious:unconscious-quote-4"),
                Lang.Get("unconscious:unconscious-quote-5"),
                Lang.Get("unconscious:unconscious-quote-6"),
                Lang.Get("unconscious:unconscious-quote-7"),
                Lang.Get("unconscious:unconscious-quote-8"),
            };

            string randomQuote = GetRandomQuote(quotes);


            SingleComposer = capi.Gui
             .CreateCompo("blackscreen", bounds)
             .AddStaticText(
                $"\"{randomQuote}\"",
                CairoFont.WhiteSmallText().WithFontSize(14).WithWeight(Cairo.FontWeight.Bold),
                quoteTextBounds,
                "quoteText"
            )
              .AddStaticText(
                GetFormattedTime(),
                CairoFont.WhiteSmallText().WithFontSize(20).WithWeight(Cairo.FontWeight.Bold),
                textBounds,
                "timerText"
            )
              .AddButton(Lang.Get("unconscious:unconscious-button-suicide"), CommitSudoku, buttonBound)
             .Compose();


            timerTextElement = SingleComposer.GetStaticText("timerText");
        }

        public void StartTimer()
        {
            isMovementDisabled = true;
            updateTimerId = capi.Event.RegisterGameTickListener(UpdateTimer, 1000); // Tick every second
            disablePlayerMovementId = capi.Event.RegisterGameTickListener(DisablePlayerMovement, 1); // Enforce movement disable every tick
        }

        public bool CommitSudoku()
        {
            remainingTime = 0;
            StopTimer();
            return true;
        }

        public void StopTimer()
        {
            var player = capi.World.Player;

            isMovementDisabled = false;
            capi.Event.UnregisterGameTickListener(updateTimerId);
            capi.Event.UnregisterGameTickListener(disablePlayerMovementId);

            PacketMethods.SendUnconsciousPacket(false);

            if (remainingTime == 0)
            {
                if (player.Entity.IsUnconscious())
                {
                    PacketMethods.SendPlayerDeathPacket();
                }
            }

            remainingTime = 0;
            TryClose();
        }

        private void UpdateTimer(float dt)
        {
            if (remainingTime > 0)
            {
                remainingTime--;
                SingleComposer.GetStaticText("timerText")?.SetValue(GetFormattedTime());
                SingleComposer.ReCompose();
            }
            else
            {
                StopTimer();
                TryClose(); // Close the GUI when the timer ends
            }
        }

        private string GetFormattedTime()
        {
            int minutes = remainingTime / 60;
            int seconds = remainingTime % 60;
            return $"{Lang.Get("unconscious:unconscious-bleed-out")}: {minutes:D2}:{seconds:D2}";
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
        }

        private void DisablePlayerMovement(float dt)
        {
            if (!isMovementDisabled) return;

            var player = capi.World.Player;

            if (player?.Entity != null)
            {
                // Stop all input controls
                var controls = player.Entity.Controls;
                controls.Forward = false;
                controls.Backward = false;
                controls.Left = false;
                controls.Right = false;
                controls.Jump = false;
                controls.Sneak = false;
                controls.Up = false;
                controls.Down = false;


                // Completely halt motion
                player.Entity.Pos.Motion.Set(0, 0, 0);
                player.Entity.ServerPos.Motion.Set(0, 0, 0);
                player.Entity.LocalEyePos.Set(0, 0.5, 0);
                player.Entity.StopAnimation("walk");
                player.Entity.StopAnimation("walkright");
                player.Entity.StopAnimation("walkleft");
                player.Entity.StopAnimation("walk");
                player.Entity.StopAnimation("sneakwalk");
            }
        }


        public override bool TryClose()
        {
            if (remainingTime > 0) return false;

            return base.TryClose();
        }
    }
}
