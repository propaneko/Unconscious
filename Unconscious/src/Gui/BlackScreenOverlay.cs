using Cairo;
using System;
using Unconscious.src.Packets;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace Unconscious.src.Gui
{
    public class BlackScreenOverlay : GuiDialog
    {
        private int totalTime;
        private int remainingTime;
        private float percentChanceOfRevival;
        private bool enableSuicideButton;
        private bool isMovementDisabled = false;
        private long updateTimerId;
        private long disablePlayerMovementId;
        private long soundTimerId;

        private long suicideId;
        private int suicideCountdown;


        public BlackScreenOverlay(ICoreClientAPI capi, ShowUnconciousScreen packet) : base(capi)
        {
            remainingTime = packet.unconsciousTime;
            totalTime = packet.unconsciousTime;
            percentChanceOfRevival = packet.chanceOfRevival;
            enableSuicideButton = packet.enableSuicideButton;
            suicideCountdown = packet.countdownSuicideButton;
            Compose();
        }

        public override string ToggleKeyCombinationCode => null; // Not toggleable manually

        static string GetRandomQuote(string[] quotes)
        {
            Random random = new Random();
            int index = random.Next(quotes.Length);
            return quotes[index];
        }

        private ElementBounds CalculateTextBounds(Context ctx, string text, double x, double y)
        {
            // Measure text dimensions
            TextExtents te = ctx.TextExtents(text);

            // Calculate the bounds dynamically based on text size
            double width = te.Width;
            double height = te.Height;

            // Return centered bounds
            return ElementBounds
                .Fixed(x, y, width, height) // Dynamically set width and height
                .WithAlignment(EnumDialogArea.CenterMiddle); // Center alignment
        }
        private void Compose()
        {
            var rectH = capi.Render.FrameHeight;
            var rectW = capi.Render.FrameWidth;

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

            using (ImageSurface surface = new ImageSurface(Format.ARGB32, 1, 1))
            using (Context ctx = new Context(surface))
            {
                ctx.SetFontSize(24);
                ctx.SelectFontFace("Sans", FontSlant.Normal, FontWeight.Normal);

                ElementBounds textBounds = CalculateTextBounds(ctx, $"{Lang.Get("unconscious:unconscious-bleed-out")}:", 0, 0);
                ElementBounds quoteTextBounds = ElementBounds.Fixed(EnumDialogArea.CenterMiddle, 0, -100, 600, 50);
                ElementBounds timerBounds = CalculateTextBounds(ctx, GetFormattedTime(), 0, 30);
                ElementBounds buttonBound = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, 0, 400, 50);
                ElementBounds bounds = ElementBounds.Fill.WithFixedPadding(0);

                SingleComposer = capi.Gui
                 .CreateCompo("blackscreen", bounds)
                 .AddStaticCustomDraw(bounds, DrawBlackRectangle)
                 .AddStaticText($"\"{randomQuote}\"", CairoFont.WhiteSmallText().WithFontSize(14).WithWeight(Cairo.FontWeight.Bold), quoteTextBounds)
                 .AddStaticText($"{Lang.Get("unconscious:unconscious-bleed-out")}:", CairoFont.WhiteSmallText().WithFontSize(20).WithWeight(Cairo.FontWeight.Bold), textBounds)
                 .AddStaticText(GetFormattedTime(), CairoFont.WhiteSmallText().WithFontSize(20).WithWeight(Cairo.FontWeight.Bold), timerBounds, "timerText");
            }

            if (enableSuicideButton)
            {
                ElementBounds buttonBound = ElementBounds.Fixed(EnumDialogArea.LeftTop, 0, 0, 400, 50);
                SingleComposer.AddButton(suicideCountdown > 0 ? suicideCountdown.ToString() : Lang.Get("unconscious:unconscious-button-suicide"), CommitSudoku, buttonBound,  EnumButtonStyle.Normal, "suicideButton").Compose();
            } else
            {
                SingleComposer.Compose();
            }


        }

        private void DrawBlackRectangle(Context context, ImageSurface surface, ElementBounds bounds)
        {
            context.SetSourceRGBA(0, 0, 0, 0.92);
            context.Rectangle(bounds.drawX, bounds.drawY, bounds.OuterWidth, bounds.OuterHeight);
            context.Fill();

            DrawBloodVignette(context, bounds);
        }
        private void DrawBloodVignette(Context ctx, ElementBounds bounds)
        {
            double width = bounds.OuterWidth;
            double height = bounds.OuterHeight;
            double maxDistance = Math.Max(width, height) / 2 + 150;

            double startRadius = maxDistance;
            double stopRadius = -100;

            double timeFactor = (double)remainingTime / totalTime; 

            double centerX = bounds.drawX + width / 2;
            double centerY = bounds.drawY + height / 2;

            double vignetteRadius = stopRadius + (startRadius - stopRadius) * timeFactor;

            using (var gradient = new RadialGradient(centerX, centerY, vignetteRadius, centerX, centerY, startRadius))
            {
                gradient.AddColorStop(0, new Color(0.1, 0, 0, 0.1));
                gradient.AddColorStop(0.5, new Color(0.15, 0, 0, 0.3));
                gradient.AddColorStop(1, new Color(0.3, 0, 0, 0.6));

                ctx.SetSource(gradient);
                ctx.Rectangle(bounds.drawX, bounds.drawY, width, height);
                ctx.Fill();
            }
        }



        public void StartTimer()
        {
            isMovementDisabled = true;
            updateTimerId = capi.Event.RegisterGameTickListener(UpdateTimer, 1000); // Tick every second
            disablePlayerMovementId = capi.Event.RegisterGameTickListener(DisablePlayerMovement, 1); // Enforce movement disable every tick
            soundTimerId = capi.Event.RegisterGameTickListener(SoundTimer, 750);
        }

        public bool CommitSudoku()
        {
            remainingTime = -1;
            StopTimer();
            return true;
        }

        public void StopTimer()
        {
            var player = capi.World.Player;

            isMovementDisabled = false;
            capi.Event.UnregisterGameTickListener(updateTimerId);
            capi.Event.UnregisterGameTickListener(disablePlayerMovementId);
            capi.Event.UnregisterGameTickListener(soundTimerId);

            PacketMethods.SendUnconsciousPacket(false);

            if (remainingTime == 0)
            {
                if (player.Entity.IsUnconscious())
                {
                    if (TryRecovery())
                    {
                        PacketMethods.SendPlayerRevivePacket();
                    }
                    else
                    {
                        PacketMethods.SendPlayerDeathPacket();
                    }
                }
            }

            if (remainingTime == -1)
            {
                if (player.Entity.IsUnconscious())
                {
                    PacketMethods.SendPlayerDeathPacket();
                }
            }

            remainingTime = 0;
            TryClose();
        }

        private bool TryRecovery()
        {
            percentChanceOfRevival = percentChanceOfRevival > 1.0f ? 1.0f : percentChanceOfRevival;
            Random rand = new Random();
            return rand.NextDouble() <= percentChanceOfRevival;
        }


        private int GetNextInterval(int remainingTime)
        {
            int minInterval = 750;
            int maxInterval = 3000;
            int range = maxInterval - minInterval;

            if (remainingTime < 0 || remainingTime > totalTime)
            {
                return minInterval;
            }

            double normalizedTime = (double)remainingTime / totalTime;

            int interval = minInterval + (int)(range * (1.0 - normalizedTime));
            return interval;
        }

        private void SoundTimer(float dt)
        {
            Random random = new Random();
            int randomNumber = random.Next(1, 12); // 1 is inclusive, 12 is exclusive
            capi.World.Player.Entity.World.PlaySoundAt(new AssetLocation($"unconscious:sounds/unconscious{randomNumber}"), capi.World.Player.Entity, null, false, 2, 0.2f);

            int nextInterval = GetNextInterval(remainingTime);
            capi.Event.UnregisterGameTickListener((long)soundTimerId);
            soundTimerId = capi.Event.RegisterGameTickListener(SoundTimer, nextInterval);
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
            return $"{minutes:D2}:{seconds:D2}";
        }

        public override void OnRenderGUI(float deltaTime)
        {
            base.OnRenderGUI(deltaTime);
        }

        private void DisablePlayerMovement(float dt)
        {
            if (!isMovementDisabled) return;

            var player = capi.World.Player;
            ClientPlayer cplayer = (player.Entity as EntityPlayer).Player as ClientPlayer;


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
                cplayer.CameraYaw = 0;
                //player.Entity.LocalEyePos.Set(0, 0.5, 0);
                player.Entity.AnimManager.StartAnimation("sleep");
            }
        }

        public override bool TryOpen()
        {
            base.TryOpen();
            if (enableSuicideButton)
            {
                SingleComposer.GetButton("suicideButton").Enabled = false;
                SingleComposer.ReCompose();
                suicideId = capi.Event.RegisterGameTickListener((dt) =>
                {
                    suicideCountdown--;
                    SingleComposer.GetButton("suicideButton").Text = suicideCountdown.ToString();
                    SingleComposer.ReCompose();
                    if (suicideCountdown <= 0)
                    {
                        SingleComposer.GetButton("suicideButton").Enabled = true;
                        SingleComposer.GetButton("suicideButton").Text = Lang.Get("unconscious:unconscious-button-suicide");
                        SingleComposer.ReCompose();
                        capi.World.UnregisterGameTickListener(suicideId);
                    }
                }, 1000);
                return true;
            }
            return true;
        }

        public override bool TryClose()
        {
            if (remainingTime > 0) return false;

            return base.TryClose();
        }
    }
}
