using Vintagestory.API.Server;
using Unconscious.src.Player;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using System.Linq;
using Vintagestory.Server;
using Unconscious.src.Packets;

namespace Unconscious.src.Commands
{
    internal class Commands
    {
        private ICoreServerAPI sapi = UnconsciousModSystem.getSAPI();
        public void SetCommands()
        {
            sapi.ChatCommands.GetOrCreate("unconscious")
                 .WithArgs(new StringArgParser("player", true))
                 .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                 .HandleWith((TextCommandCallingArgs args) =>
                 {
                     var targetPlayer = sapi.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());
                     if (targetPlayer != null)
                     {
                         if (targetPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
                         {
                             return new TextCommandResult
                             {
                                 Status = EnumCommandStatus.Error,
                                 StatusMessage = Lang.Get($"Player is not in the Survival mode!"),
                             };
                         }

                         if (targetPlayer.Entity.IsUnconscious())
                         {
                             return new TextCommandResult
                             {
                                 Status = EnumCommandStatus.Error,
                                 StatusMessage = Lang.Get($"Player is already unconscious!"),
                             };
                         }

                         UnconsciousModSystem.PlayerDropActiveItemOnUnconscious(targetPlayer);
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
                 });

            sapi.ChatCommands.GetOrCreate("revive")
               .WithArgs(new StringArgParser("player", true))
               .RequiresPrivilege(UnconsciousModSystem.getConfig().ReviveCmdPrivilege)
               .HandleWith((TextCommandCallingArgs args) =>
               {
                   var targetPlayer = sapi.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());

                   if (targetPlayer.WorldData.CurrentGameMode != EnumGameMode.Survival)
                   {
                       return new TextCommandResult
                       {
                           Status = EnumCommandStatus.Error,
                           StatusMessage = Lang.Get($"Player is not in the Survival mode!"),
                       };
                   }

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
                           UnconsciousModSystem.HandlePlayerPickup(targetPlayer.Entity);
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
               });
            var parsers = sapi.ChatCommands.Parsers;
            sapi.ChatCommands.GetOrCreate("playanimation")
                .WithArgs(parsers.Word("animation"))
                .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                .HandleWith((TextCommandCallingArgs args) =>
                {
                    IPlayer callerPlayer = args.Caller.Player;
                    IServerPlayer serverPlayer = sapi.World.PlayerByUid(callerPlayer.PlayerUID) as IServerPlayer;

                    string animation = args[0].ToString();

                    if (serverPlayer != null)
                    {
                        serverPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode.Clear();
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
                });

            sapi.ChatCommands.GetOrCreate("stopanimation")
                .WithArgs(parsers.Word("animation"))
                .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
                .HandleWith((TextCommandCallingArgs args) =>
                {
                    //var targetPlayer = sapi.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());
                    //IServerPlayer targetPlayer = (IServerPlayer)args[0];
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
                });
        }
       

    }
}
