using Vintagestory.API.Server;
using Unconscious.src.Player;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System.Linq;
using Unconscious.src.Packets;

namespace Unconscious.src.Commands
{
    internal class Commands
    {
        private ICoreServerAPI sapi = UnconsciousModSystem.getSAPI();
        private const string ConfigName = "unconscious.json";
        public void SetCommands()
        {
            sapi.ChatCommands.GetOrCreate("unconscious")
                .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                .BeginSubCommand("make")
                .WithArgs(new StringArgParser("player", true))
                .HandleWith(OnUnconsciousCommand)
                .EndSubCommand()
                
                .BeginSubCommand("revive")
                .WithArgs(new StringArgParser("player", true))
                .RequiresPrivilege(UnconsciousModSystem.getConfig().ReviveCmdPrivilege)
                .HandleWith(OnReviveCommand)
                .EndSubCommand()

                .BeginSubCommand("config")
                    .BeginSubCommand("duration")
                    .WithDescription("set duration for how long player will be unconscious")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new IntArgParser("time",0, 9000, 300, false))
                    .HandleWith(OnDurationChange)
                    .EndSubCommand()

                    .BeginSubCommand("finishTimer")
                    .WithDescription("set duration for how long the finisher button will be disabled")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new IntArgParser("time", 0, 9000, 300, false))
                    .HandleWith(OnFinishTimerChange)
                    .EndSubCommand()

                    .BeginSubCommand("suicideTimer")
                    .WithDescription("set duration for how long the suicide button will be disabled")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new IntArgParser("time", 0, 9000, 300, false))
                    .HandleWith(OnSuicideTimerChange)
                    .EndSubCommand()

                    .BeginSubCommand("reviveTick")
                    .WithDescription("set how much value will increase per tick")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new FloatArgParser("value", 0.1f, 10f, false))
                    .HandleWith(OnReviveTickChange)
                    .EndSubCommand()

                    .BeginSubCommand("pickupTick")
                    .WithDescription("set how much value will increase per tick")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new FloatArgParser("value", 0.1f, 10f, false))
                    .HandleWith(OnPickupTickChange)
                    .EndSubCommand()

                    .BeginSubCommand("maxHealthAfteRrevive")
                    .WithDescription("set how much health will be regenerated after revive (0.0f is 0%)")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new FloatArgParser("value", 0.0f, 1.0f, false))
                    .HandleWith(OnMaxHealthAfteRreviveChange)
                    .EndSubCommand()

                    .BeginSubCommand("chanceOfSelfRevival")
                    .WithDescription("set chance of self revival after timer runs out (0.0f is 0% %)")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new FloatArgParser("value", 0.0f, 1.0f, false))
                    .HandleWith(OnChanceOfSelfRevivalChange)
                    .EndSubCommand()

                    .BeginSubCommand("enableCarryMechanic")
                    .WithDescription("enable or disable the carry mechanic")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new BoolArgParser("enable", "enable", false))
                    .HandleWith(OneEnableCarryMechanicChange)
                    .EndSubCommand()

                    .BeginSubCommand("enableSuicideButton")
                    .WithDescription("enable or disable the suicide button")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new BoolArgParser("enable", "enable", false))
                    .HandleWith(OnEnableSuicideButtonChange)
                    .EndSubCommand()

                    .BeginSubCommand("dropWeaponOnUnconscious")
                    .WithDescription("enable or disable dropping holding item on unconscious")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new BoolArgParser("enable", "enable", false))
                    .HandleWith(OnDropWeaponOnUnconsciousChange)
                    .EndSubCommand()

                    .BeginSubCommand("requireSmellingSaltsForRevive")
                    .WithDescription("enable or disable the need of smelling salts for reviving")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new BoolArgParser("enable", "enable", false))
                    .HandleWith(OnRequireSmellingSaltsForReviveChange)
                    .EndSubCommand()

                    .BeginSubCommand("gracePeriodBS")
                    .WithDescription("when using Bloody Story set the grace period in which after reviving the player will resume bleeding")
                    .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                    .WithArgs(new IntArgParser("time", 0, 100000, 10000, false))
                    .HandleWith(OnGracePeriodBShange)
                    .EndSubCommand()

                .EndSubCommand();

            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.GetOrCreate("playanimation")
                .WithArgs(parsers.Word("animation"))
                .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                .HandleWith(OnAnimationPlay);

            sapi.ChatCommands.GetOrCreate("stopanimation")
                .WithArgs(parsers.Word("animation"))
                .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                .HandleWith(OnAnimationStop);
        }

        private TextCommandResult OnGracePeriodBShange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Grace Period time in ms: {UnconsciousModSystem.getConfig().GracePeriod}");
            }

            UnconsciousModSystem.getConfig().GracePeriod = (int)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Grace Period time in ms: {UnconsciousModSystem.getConfig().GracePeriod}");
        }

        private TextCommandResult OnRequireSmellingSaltsForReviveChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Require smelling salts: {UnconsciousModSystem.getConfig().RequireSmellingSaltsForRevive}");
            }

            UnconsciousModSystem.getConfig().RequireSmellingSaltsForRevive = (bool)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Require smelling salts: {UnconsciousModSystem.getConfig().RequireSmellingSaltsForRevive}");
        }

        private TextCommandResult OnDropWeaponOnUnconsciousChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Drop weapon enabled: {UnconsciousModSystem.getConfig().DropWeaponOnUnconscious}");
            }

            UnconsciousModSystem.getConfig().DropWeaponOnUnconscious = (bool)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Drop weapon enabled: {UnconsciousModSystem.getConfig().DropWeaponOnUnconscious}");
        }
        private TextCommandResult OnEnableSuicideButtonChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Suicide button enabled: {UnconsciousModSystem.getConfig().EnableSuicideButton}");
            }

            UnconsciousModSystem.getConfig().EnableSuicideButton = (bool)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Suicide button enabled: {UnconsciousModSystem.getConfig().EnableSuicideButton}");
        }

        private TextCommandResult OneEnableCarryMechanicChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Carry mechanic enabled: {UnconsciousModSystem.getConfig().EnableCarryMechanic}");
            }

            UnconsciousModSystem.getConfig().EnableCarryMechanic = (bool)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Carry mechanic enabled: {UnconsciousModSystem.getConfig().EnableCarryMechanic}");
        }

        private TextCommandResult OnChanceOfSelfRevivalChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Chance of self revival: {UnconsciousModSystem.getConfig().ChanceOfSelfRevival}");
            }

            UnconsciousModSystem.getConfig().ChanceOfSelfRevival = (float)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Chance of self revival: {UnconsciousModSystem.getConfig().ChanceOfSelfRevival}");
        }

        private TextCommandResult OnMaxHealthAfteRreviveChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Health after revive: {UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive}");
            }

            UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive = (float)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Health after revive: {UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive}");
        }

        private TextCommandResult OnPickupTickChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Pickup tick value: {UnconsciousModSystem.getConfig().PickupPerTickDuration}");
            }

            UnconsciousModSystem.getConfig().PickupPerTickDuration = (float)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Suicide timer duration: {UnconsciousModSystem.getConfig().PickupPerTickDuration}");
        }


        private TextCommandResult OnReviveTickChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Revive tick value: {UnconsciousModSystem.getConfig().RevivePerTickDuration}");
            }

            UnconsciousModSystem.getConfig().RevivePerTickDuration = (float)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Revive tick value: {UnconsciousModSystem.getConfig().RevivePerTickDuration}");
        }

        private TextCommandResult OnSuicideTimerChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Suicide timer duration: {UnconsciousModSystem.getConfig().SuicideTimer}");
            }

            UnconsciousModSystem.getConfig().SuicideTimer = (int)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Suicide timer duration: {UnconsciousModSystem.getConfig().SuicideTimer}");
        }

        private TextCommandResult OnFinishTimerChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Finishing timer duration: {UnconsciousModSystem.getConfig().FinishingTimer}");
            }

            UnconsciousModSystem.getConfig().FinishingTimer = (int)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Finishing timer duration: {UnconsciousModSystem.getConfig().FinishingTimer}");
        }

        private TextCommandResult OnDurationChange(TextCommandCallingArgs args)
        {
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success($"Unconscious duration: {UnconsciousModSystem.getConfig().UnconsciousDuration}");
            }

            UnconsciousModSystem.getConfig().UnconsciousDuration = (int)args[0];
            sapi.StoreModConfig(UnconsciousModSystem.getConfig(), ConfigName);
            return TextCommandResult.Success($"Unconscious duration: {UnconsciousModSystem.getConfig().UnconsciousDuration}");
        }

        private TextCommandResult OnReviveCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = sapi.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());

            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get($"Cant find player!"),
                };
            }

            //if (targetPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
            //{
            //    return new TextCommandResult
            //    {
            //        Status = EnumCommandStatus.Error,
            //        StatusMessage = Lang.Get($"Player is not in the Survival mode!"),
            //    };
            //}

            if (targetPlayer.PlayerUID == args.Caller.Player.PlayerUID && !targetPlayer.HasPrivilege("ban"))
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get($"You cant revive yourself!."),
                };
            }

            if (targetPlayer != null)
            {
                var health = targetPlayer.Entity.WatchedAttributes.GetTreeAttribute("health");

                if (targetPlayer.Entity.IsUnconscious())
                {
                    UnconsciousModSystem.HandlePlayerPickup(targetPlayer.Entity, 1.0f);
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Success,
                        StatusMessage = Lang.Get($"Player picked up!"),
                    };
                }

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get($"{targetPlayer.PlayerName} is not unconscious."),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get($"Something went wrong. Maybe player doesnt exist?"),
            };
        }

        private TextCommandResult OnUnconsciousCommand(TextCommandCallingArgs args)
        {
            var targetPlayer = sapi.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());

            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get($"Cant find player!"),
                };
            }

            if (targetPlayer != null)
            {
                //if (targetPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
                //{
                //    return new TextCommandResult
                //    {
                //        Status = EnumCommandStatus.Error,
                //        StatusMessage = Lang.Get($"Player is not in the Survival mode!"),
                //    };
                //}

                if (targetPlayer.Entity.IsUnconscious())
                {
                    return new TextCommandResult
                    {
                        Status = EnumCommandStatus.Error,
                        StatusMessage = Lang.Get($"Player is already unconscious!"),
                    };
                }

                UnconsciousModSystem.HandlePlayerUnconscious(targetPlayer.Entity);

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get($"{targetPlayer.PlayerName} is now unconscious."),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get($"Something went wrong. Maybe player doesnt exist?"),
            };
        }

        private TextCommandResult OnAnimationStop(TextCommandCallingArgs args)
        {
            IPlayer callerPlayer = args.Caller.Player;
            IServerPlayer serverPlayer = sapi.World.PlayerByUid(callerPlayer.PlayerUID) as IServerPlayer;

            string animation = args[0].ToString();

            if (serverPlayer != null)
            {
                serverPlayer.Entity.AnimManager.StopAnimation(animation);
                PacketMethods.SendAnimationPacketToClient(false, animation, serverPlayer);

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get($"{serverPlayer.PlayerName} is playing animation. {animation}"),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get($"Something went wrong. Maybe player doesnt exist?"),
            };
        }

        private TextCommandResult OnAnimationPlay(TextCommandCallingArgs args)
        {
            IPlayer callerPlayer = args.Caller.Player;
            IServerPlayer serverPlayer = sapi.World.PlayerByUid(callerPlayer.PlayerUID) as IServerPlayer;

            string animation = args[0].ToString();

            if (serverPlayer != null)
            {
                serverPlayer.Entity.AnimManager.StartAnimation(animation);
                PacketMethods.SendAnimationPacketToClient(true, animation, serverPlayer);

                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get($"{serverPlayer.PlayerName} is playing animation. {animation}"),
                };
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get($"Something went wrong. Maybe player doesnt exist?"),
            };
        }

      


    }
}
