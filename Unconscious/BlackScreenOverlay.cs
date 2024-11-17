using System;
using Vintagestory.API.Client;

namespace Unconscious
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
            this.remainingTime = durationInSeconds;
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
                "\"Your vision fades to black, the world spinning into a distant hum. You feel the cold embrace of the earth beneath you and the weight of your own weakness. Time slips away, and you are left adrift in the void—helpless, yet not entirely lost. Will you rise again, or remain a shadow in this unforgiving land?\"",
                "\"Darkness wraps around you like a heavy shroud. The sounds of the world grow distant, muffled as if submerged in deep waters. Your body feels heavy, unresponsive, yet faint whispers of life linger within. Is this the end... or merely a fleeting pause in your journey?\"",
                "\"A numb stillness overtakes you, as though the world itself has turned its back. Shadows dance at the edge of your awareness, teasing you with glimpses of light that feel just out of reach. The faint memory of warmth lingers, a bittersweet echo of life slipping further from your grasp.\"",
                "\"The weight of the earth presses against you, pinning you to the cold, unyielding ground. Time drifts, disjointed, as you struggle to hold onto fleeting moments of clarity. The world feels impossibly distant, its sounds muffled by the thick fog of unconsciousness.\"",
                "\"A void envelopes you, vast and unending, swallowing every trace of your senses. You strain to grasp onto anything familiar, but all that remains is a deep, aching silence that threatens to pull you deeper into its embrace.\"",
                "\"Your limbs are heavy, as if bound by invisible chains. The air is thick, each breath a struggle against the encroaching darkness. Yet somewhere in the abyss, a faint light flickers—a fragile beacon of hope.\"",
                "\"The ground beneath you feels both solid and unreal, as though you are caught between two worlds. Distant echoes of life reach your ears, blurred and distorted, as the darkness teases you with its quiet, suffocating allure.\"",
                "\"The once-familiar rhythm of your heartbeat grows faint, each beat a whisper in the silence. Shadows close in, their cold fingers brushing against your skin as your thoughts scatter like leaves in the wind.\""
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
              .AddButton("Commit sudoku", CommitSudoku, buttonBound)
             .Compose();


            timerTextElement = SingleComposer.GetStaticText("timerText");
        }

        public void StartTimer()
        {
            isMovementDisabled = true;
            updateTimerId = capi.Event.RegisterGameTickListener(UpdateTimer, 1000); // Tick every second
            disablePlayerMovementId = capi.Event.RegisterGameTickListener(DisablePlayerMovement, 20); // Enforce movement disable every tick
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
            return $"You're going to bleed out in: {minutes:D2}:{seconds:D2}";
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

                // Completely halt motion
                player.Entity.Pos.Motion.Set(0, 0, 0);
                player.Entity.ServerPos.Motion.Set(0, 0, 0);

            }
        }


        public override bool TryClose()
        {
            // Prevent closing with Esc while unconscious
            if (remainingTime > 0) return false;

            return base.TryClose();
        }
    }
}
