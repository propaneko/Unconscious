using HarmonyLib;
using System;
using System.Linq;
using Unconscious.src.Commands;
using Unconscious.src.Config;
using Unconscious.src.Gui;
using Unconscious.src.Handlers;
using Unconscious.src.Harmony;
using Unconscious.src.Packets;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.Server;

namespace Unconscious
{
    public class UnconsciousModSystem : ModSystem
    {
        public static UnconsciousModSystem modInstance;
        public Harmony harmony;
        static ICoreServerAPI sapi;
        static ICoreClientAPI capi;
        private BlackScreenOverlay dialogBlackScreen = null;
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
            harmony = new Harmony(Mod.Info.ModID);
            var original = AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.OnHurt));
            var postfix = AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.OnHurt));
            harmony.Patch(original, new HarmonyMethod(postfix));

            base.Start(api);

            api.Network.RegisterChannel(Mod.Info.ModID)
           .RegisterMessageType(typeof(SendUnconsciousPacket))
           .RegisterMessageType(typeof(PlayerDeath))
           .RegisterMessageType(typeof(PlayerKill))
           .RegisterMessageType(typeof(PlayerRevive))
           .RegisterMessageType(typeof(PlayerAnimation))
           .RegisterMessageType(typeof(ShowPlayerFinishOffScreenPacket))
           .RegisterMessageType(typeof(ShowUnconciousScreen));

            api.RegisterEntityBehaviorClass("reviveBehavior", typeof(PlayerBehavior));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            new ClientMessageHandler().SetMessageHandlers();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            sapi.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, () =>
            {
                LoadConfig();
                new ServerMessageHandler().SetMessageHandlers();

                sapi.Event.PlayerRespawn += (entity) =>
                {
                    if (entity.Entity is EntityPlayer player)
                    {
                        if (!player.HasBehavior("reviveBehavior"))
                        {
                            sapi.Logger.Event($"{player.Player.PlayerName} got revive behavior");
                            player.AddBehavior(new PlayerBehavior(player));
                        }
                    }
                };

                sapi.Event.PlayerNowPlaying += (entity) =>
                {
                    if (entity.Entity is EntityPlayer player)
                    {
                        ApplyUnconsciousOnJoin(player);
                    }
                };

                sapi.Event.PlayerJoin += (entity) =>
                {
                    if (entity.Entity is EntityPlayer player)
                    {
                        if (!player.HasBehavior("reviveBehavior"))
                        {
                            sapi.Logger.Event($"{player.Player.PlayerName} got revive behavior");
                            player.AddBehavior(new PlayerBehavior(player));
                        }
                    }
                };

                new Commands().SetCommands();
            });
        }
        public static void HandlePlayerPickup(EntityPlayer player)
        {
            player.AnimManager.StopAnimation("sleep");
            var health = player.WatchedAttributes.GetTreeAttribute("health");

            player.Revive();
            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var maxHealth = health.GetFloat("maxhealth");
            health.SetFloat("currenthealth", maxHealth * UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;
            player.AnimManager.ActiveAnimationsByAnimCode.Clear();
            PacketMethods.SendShowUnconciousScreenPacket(false, serverPlayer);
        }

        public static void HandlePlayerUnconscious(EntityPlayer player)
        {
            player.AnimManager.ActiveAnimationsByAnimCode.Foreach((code => player.AnimManager.StopAnimation(code.Value.ToString())));
            player.AnimManager.StartAnimation("sleep");

            player.WatchedAttributes.SetBool("unconscious", true);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var health = player.WatchedAttributes.GetTreeAttribute("health");
            health.SetFloat("currenthealth", 1);
            player.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (config.DropWeaponOnUnconscious)
            {
                PlayerDropActiveItemOnUnconscious(serverPlayer);
            }
            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);
            PacketMethods.SendShowUnconciousScreenPacket(true, serverPlayer);
        }

        public static void PlayerDropActiveItemOnUnconscious(IPlayer serverPlayer)
        {
            ItemSlot activeSlot = serverPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack == null) return; // No item to drop\

    
            ItemStack itemToDrop = activeSlot.Itemstack.Clone();

            activeSlot.TakeOutWhole();
            activeSlot.MarkDirty();

            Vec3d forwardDirection = serverPlayer.Entity.SidedPos.AheadCopy(1).XYZ.Normalize();
            Vec3d throwVelocity = forwardDirection * 2.5; // Increase this value for further throws
            serverPlayer.Entity.World.SpawnItemEntity(itemToDrop, serverPlayer.Entity.Pos.XYZ, throwVelocity);
            activeSlot.Itemstack = null; // Clear the slot
            serverPlayer.InventoryManager.BroadcastHotbarSlot();
        }

        private void ApplyUnconsciousOnJoin(EntityPlayer player)
        {
            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (serverPlayer.Entity.IsUnconscious())
            {
                HandlePlayerUnconscious(serverPlayer.Entity);
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }
    }
}
