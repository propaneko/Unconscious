using CompactExifLib;
using HarmonyLib;
using NoticeBoard.Packets;
using System.Linq;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace Unconscious
{
    public class UnconsciousModSystem : ModSystem
    {
        public static UnconsciousModSystem modInstance;
        public Harmony harmony;
        static ICoreServerAPI sapi;
        static ICoreClientAPI capi;
        private BlackScreenOverlay dialog = null;
        private FinishOffOverlay dialogFinishOff = null;


        public UnconsciousModSystem()
        {
            modInstance = this;
        }

        public static ICoreServerAPI getSAPI()
        {
            return sapi;
        }

        public static ICoreClientAPI getCAPI()
        {
            return capi;
        }

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony("unconscious");
            var original = AccessTools.Method(typeof(Entity), nameof(Entity.ReceiveDamage));
            var postfix = AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.ReceiveDamage));
            harmony.Patch(original, new HarmonyMethod(postfix));

            base.Start(api);

            api.Network.RegisterChannel("unconscious")
           .RegisterMessageType(typeof(SendUnconsciousPacket))
           .RegisterMessageType(typeof(PlayerDeath))
           .RegisterMessageType(typeof(PlayerKill))
           .RegisterMessageType(typeof(ShowPlayerFinishOffScreenPacket))
           .RegisterMessageType(typeof(ShowUnconciousScreen));

            api.RegisterEntityBehaviorClass("reviveBehavior", typeof(PlayerBehavior));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            sapi.Network.GetChannel("unconscious").SetMessageHandler<SendUnconsciousPacket>(OnServerMessagesReceived);
            sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerDeath>(OnPlayerDeathPacket);
            sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerKill>(OnPlayerKill);


            sapi.Event.PlayerRespawn += (entity) =>
            {
                if (entity.Entity is EntityPlayer player)
                {
                    if (!player.HasBehavior("reviveBehavior"))
                    {
                        UnconsciousModSystem.getSAPI().Logger.Event("player got revive behavior");
                        player.AddBehavior(new PlayerBehavior(player));
                    }
                }
            };

            sapi.Event.PlayerJoin += (entity) =>
            {
                if (entity.Entity is EntityPlayer player)
                {
                    if (!player.HasBehavior("reviveBehavior"))
                    {
                        UnconsciousModSystem.getSAPI().Logger.Event("player got revive behavior");
                        player.AddBehavior(new PlayerBehavior(player));
                    }
                }
            };

            sapi.ChatCommands.GetOrCreate("unconscious")
               .RequiresPrivilege("ban")
               .WithDescription(Lang.Get("edenvalrpessentials:countdown-command-description"))
               .WithArgs(new StringArgParser("player", true))
               .RequiresPrivilege(Privilege.chat)
               .HandleWith((TextCommandCallingArgs args) =>
               {
                   var targetPlayer = api.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());
                   if (targetPlayer != null)
                   {
                       if (targetPlayer.Entity.IsUnconscious())
                       {
                           PacketMethods.SendShowUnconciousScreenPacket(true, targetPlayer as IServerPlayer);
                           return new TextCommandResult
                           {
                               Status = EnumCommandStatus.Error,
                               StatusMessage = Lang.Get($"Player is already unconscious!"),
                           };
                       }

                       targetPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode.Clear();
                       targetPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode.Foreach((code => targetPlayer.Entity.AnimManager.StopAnimation(code.Value.ToString())));
                       targetPlayer.Entity.AnimManager.StartAnimation("die");

                       targetPlayer.Entity.WatchedAttributes.SetBool("unconscious", true);
                       targetPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");
                       var health = targetPlayer.Entity.WatchedAttributes.GetTreeAttribute("health");
                       health.SetFloat("currenthealth", 1);
                       targetPlayer.Entity.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

                       PacketMethods.SendShowUnconciousScreenPacket(true, targetPlayer as IServerPlayer);

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
               .WithDescription(Lang.Get("edenvalrpessentials:countdown-command-description"))
               .WithArgs(new StringArgParser("player", true))
               .RequiresPrivilege(Privilege.chat)
               .HandleWith((TextCommandCallingArgs args) =>
               {
                   var targetPlayer = api.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());

                   if( targetPlayer.PlayerUID == args.Caller.Player.PlayerUID && !targetPlayer.HasPrivilege("ban"))
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
                           targetPlayer.Entity.Revive();
                           targetPlayer.Entity.WatchedAttributes.SetBool("unconscious", false);
                           targetPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");
                           health.SetFloat("currenthealth", 5);

                           PacketMethods.SendShowUnconciousScreenPacket(false, targetPlayer as IServerPlayer);

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
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            capi.Network.GetChannel("unconscious").SetMessageHandler<ShowUnconciousScreen>(OnClientMessagesReceived);
            capi.Network.GetChannel("unconscious").SetMessageHandler<ShowPlayerFinishOffScreenPacket>(OnClientFinishedOffScreenReceived);

            capi.Event.PlayerEntitySpawn += ApplyUnconsciousOnJoin;
        }

        private void ApplyUnconsciousOnJoin(IClientPlayer byPlayer)
        {
            dialog = new BlackScreenOverlay(capi, 300);
            var isUnconscious = byPlayer.Entity.IsUnconscious();
            UnconsciousModSystem.getCAPI().Logger.Event(isUnconscious.ToString());
            if (isUnconscious)
            {
                dialog.TryOpen();
                dialog.StartTimer();
            }
        }

        private void OnServerMessagesReceived(IServerPlayer player, SendUnconsciousPacket packet)
        {
            player.Entity.SetUnconscious(packet.isUnconscious);
            player.Entity.WatchedAttributes.MarkPathDirty("unconscious");
        }

        private void OnPlayerDeathPacket(IServerPlayer player, PlayerDeath packet)
        {
            player.Entity.SetUnconscious(false);
            player.Entity.WatchedAttributes.MarkPathDirty("unconscious");

            player.Entity.Die(EnumDespawnReason.Death, new DamageSource
            {
                Type = EnumDamageType.Injury, // Set damage type
                Source = EnumDamageSource.Suicide
            });
        }

        private void OnPlayerKill(IServerPlayer player, PlayerKill packet)
        {
            IServerPlayer attackingServerPlayer = sapi.World.PlayerByUid(packet.attackerPlayerUUID) as IServerPlayer;
            IServerPlayer victimServerPlayer = sapi.World.PlayerByUid(packet.victimPlayerUUID) as IServerPlayer;


            victimServerPlayer.Entity.SetUnconscious(false);
            victimServerPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");

            ShowUnconciousScreen responsePacket = new()
            {
                shouldShow = false,
            };

            sapi.Network.GetChannel("unconscious").SendPacket(responsePacket, victimServerPlayer);

            victimServerPlayer.Entity.Die(EnumDespawnReason.Death, new DamageSource
            {
                Type = packet.damageType, // Set damage type
                Source = EnumDamageSource.Entity,
                SourceEntity = attackingServerPlayer.Entity,
                CauseEntity = attackingServerPlayer.Entity
            });
        }

        private void HandleUnsconscious(bool isUnconscious)
        {
            if (isUnconscious)
            {
                dialog = new BlackScreenOverlay(capi, 300);
                dialog.TryOpen();
                dialog.StartTimer();
                return;
            }

            if (!isUnconscious && dialog != null)
            {
                dialog.StopTimer();
                dialog.TryClose();
                dialog = null;
                return;
            }
        }

        private void OnClientMessagesReceived(ShowUnconciousScreen packet)
        {
            if (packet == null)
            {
                return;
            }

            HandleUnsconscious(packet.shouldShow);
        }

        private void OnClientFinishedOffScreenReceived(ShowPlayerFinishOffScreenPacket packet)
        {
            if (dialogFinishOff == null || !dialogFinishOff.IsOpened())
            {
                dialogFinishOff = new FinishOffOverlay(capi, sapi, packet);
                dialogFinishOff.TryOpen();
                return;
            }
        }

    }
}
