﻿using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
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
using Vintagestory.GameContent;
using Unconscious.src.Compat;

namespace Unconscious
{
    public class UnconsciousModSystem : ModSystem
    {
        public static UnconsciousModSystem modInstance;
        public Harmony harmony;
        static ICoreServerAPI sapi;
        static ICoreClientAPI capi;

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
            string configPath = Path.Combine(GamePaths.ModConfig, ConfigName);

            // Create default config if not present
            if (!File.Exists(configPath))
            {
                config = new ModConfig{
                    EnabledDamageTypes = new List<string> {     
                        "Gravity",
                        "Fire",
                        "BluntAttack",
                        "SlashingAttack",
                        "PiercingAttack",
                        "Suffocation",
                        "Poison",
                        "Hunger",
                        "Crushing",
                        "Frost",
                        "Electricity",
                        "Heat",
                        "Injury"
                    }
                };

                //File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));
                sapi.StoreModConfig(config, ConfigName);
            }
            else
            {
                // Load the existing configuration
                config = sapi.LoadModConfig<ModConfig>(ConfigName);
                //config = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(configPath));
            }
        }

        public override void Start(ICoreAPI api)
        {
            harmony = new Harmony(Mod.Info.ModID);
            var originalOnHurt = AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.OnHurt));
            var postfixOnHurt = AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.OnHurt));
            harmony.Patch(originalOnHurt, new HarmonyMethod(postfixOnHurt));

            var originalIsTargetableEntity = AccessTools.Method(typeof(AiTaskBaseTargetable), nameof(AiTaskBaseTargetable.IsTargetableEntity));
            var postfixIsTargetableEntity = AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.ShouldCancelTarget));
            harmony.Patch(originalIsTargetableEntity, new HarmonyMethod(postfixIsTargetableEntity));

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
                        if (getSAPI().ModLoader.GetMod("bloodystory") != null)
                        {
                            BSCompat.AddOnBleedoutEH(player);
                        }
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

        public static void HandlePlayerPickup(EntityPlayer player, float pickupPercentHealth)
        {
            player.AnimManager.StopAnimation("sleep");
            var health = player.WatchedAttributes.GetTreeAttribute("health");

            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var maxHealth = health.GetFloat("maxhealth");
            var maxHealthConfig = pickupPercentHealth > 1.0f ? 1.0f : pickupPercentHealth;
            health.SetFloat("currenthealth", maxHealth * maxHealthConfig);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;
            PacketMethods.SendShowUnconciousScreenPacket(false, serverPlayer);

            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
            {
                BSCompat.HandleRevive(player);
            }
        }

        public static void HandlePlayerUnconscious(EntityPlayer player)
        {
            player.AnimManager.StartAnimation("sleep");
            player.WatchedAttributes.SetBool("unconscious", true);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var health = player.WatchedAttributes.GetTreeAttribute("health");
            health.SetFloat("currenthealth", 1);
            player.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (getConfig().DropWeaponOnUnconscious)
            {
                PlayerDropActiveItemOnUnconscious(serverPlayer);
            }
            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);
            PacketMethods.SendShowUnconciousScreenPacket(true, serverPlayer);

            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
            {
                BSCompat.HandleUnconscious(player);
            }
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
