using HarmonyLib;
using System;
using System.Linq;
using Unconscious.src.Config;
using Unconscious.src.Gui;
using Unconscious.src.Harmony;
using Unconscious.src.Packets;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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

        public static ModConfig config;
        private const string ConfigName = "unconscious.json";

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

        public static ModConfig getConfig()
        {
            return config;
        }

        private void LoadConfig()
        {
            try
            {
                config = sapi.LoadModConfig<ModConfig>(ConfigName);
            }
            catch (Exception)
            {
                sapi.Server.LogError("Unconscious: Failed to load mod config!");
                return;
            }

            if (config == null)
            {
                sapi.Server.LogNotification("Unconscious: non-existant modconfig at '" + ConfigName +
                                           "', creating default...");
                config = new ModConfig();
                sapi.StoreModConfig(config, ConfigName);
            }
        }

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

            sapi.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, () =>
            {
                LoadConfig();
                sapi.Network.GetChannel("unconscious").SetMessageHandler<SendUnconsciousPacket>(OnServerMessagesReceived);
                sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerDeath>(OnPlayerDeathPacket);
                sapi.Network.GetChannel("unconscious").SetMessageHandler<PlayerKill>(OnPlayerKill);
            });

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
                    ApplyUnconsciousOnJoin(player);
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
               .RequiresPrivilege(UnconsciousModSystem.getConfig().UnconsciousCmdPrivilege)
               .HandleWith((TextCommandCallingArgs args) =>
               {
                   var targetPlayer = api.World.AllPlayers.FirstOrDefault(player => player.PlayerName == (string)args.Parsers[0].GetValue());
                   if (targetPlayer != null)
                   {
                       if (targetPlayer.Entity.IsUnconscious())
                       {
                           //PacketMethods.SendShowUnconciousScreenPacket(true, config.UnconsciousDuration, targetPlayer as IServerPlayer);
                           return new TextCommandResult
                           {
                               Status = EnumCommandStatus.Error,
                               StatusMessage = Lang.Get($"Player is already unconscious!"),
                           };
                       }

                       targetPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode.Clear();
                       targetPlayer.Entity.AnimManager.ActiveAnimationsByAnimCode.Foreach((code => targetPlayer.Entity.AnimManager.StopAnimation(code.Value.ToString())));
                       targetPlayer.Entity.AnimManager.StartAnimation("sleep");

                       targetPlayer.Entity.WatchedAttributes.SetBool("unconscious", true);
                       targetPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");
                       var health = targetPlayer.Entity.WatchedAttributes.GetTreeAttribute("health");
                       health.SetFloat("currenthealth", 1);
                       targetPlayer.Entity.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

                       PacketMethods.SendShowUnconciousScreenPacket(true, config.UnconsciousDuration, targetPlayer as IServerPlayer);

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
               .RequiresPrivilege(UnconsciousModSystem.getConfig().ReviveCmdPrivilege)
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
                           var maxHealth = health.GetFloat("maxhealth");
                           health.SetFloat("currenthealth", maxHealth * UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive);

                           PacketMethods.SendShowUnconciousScreenPacket(false, 0, targetPlayer as IServerPlayer);

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
        }

        private void ApplyUnconsciousOnJoin(EntityPlayer player)
        {

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (player.IsUnconscious())
            {
                ShowUnconciousScreen responsePacket = new()
                {
                    shouldShow = true,
                };

                sapi.Network.GetChannel("unconscious").SendPacket(responsePacket, serverPlayer);
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

            if (victimServerPlayer.Entity.IsUnconscious())
            {
                victimServerPlayer.Entity.SetUnconscious(false);
                victimServerPlayer.Entity.WatchedAttributes.MarkPathDirty("unconscious");


                ShowUnconciousScreen responsePacket = new()
                {
                    shouldShow = false,
                    unconsciousTime = 0
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
        }

        private void OnClientMessagesReceived(ShowUnconciousScreen packet)
        {
            if (packet.shouldShow)
            {
                dialog = new BlackScreenOverlay(capi, packet.unconsciousTime);
                dialog.TryOpen();
                dialog.StartTimer();
                return;
            }

            if (!packet.shouldShow && dialog != null)
            {
                dialog.StopTimer();
                dialog.TryClose();
                dialog = null;
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

    }
}
