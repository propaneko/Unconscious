using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Unconscious.Behaviors.Items;
using Unconscious.Behaviors.Player;
using Unconscious.CommandSystem;
using Unconscious.Compat;
using Unconscious.Config;
using Unconscious.Handlers;
using Unconscious.HarmonyPatches;
using Unconscious.Items;
using Unconscious.Packets;
using Unconscious.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious
{
    public class UnconsciousModSystem : ModSystem
    {
        public static UnconsciousModSystem modInstance;
        public Harmony harmony;
        static ICoreServerAPI sapi;
        static ICoreClientAPI capi;

        public class UnconsciousTimer
        {
            public DateTime date;
            public string PlayerUID;
        }

        public static List<UnconsciousTimer> unconsciousTimers = new List<UnconsciousTimer>();

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
                config = new ModConfig
                {
                    EnabledDamageTypes = new List<string>
                    {
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
                        "Injury",
                    },
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
            api.RegisterEntityBehaviorClass(
                "UnconsciousBehavior",
                typeof(BehaviorUnconsciousPlayer)
            );
            api.RegisterEntityBehaviorClass(
                "UnconsciousReviveBehavior",
                typeof(BehaviorRevivePlayer)
            );
            api.RegisterEntityBehaviorClass(
                "UnconsciousPickupBehavior",
                typeof(BehaviorPickupPlayer)
            );

            api.RegisterCollectibleBehaviorClass(
                "UnconsciousRevivingItem",
                typeof(CollectibleBehaviorRevivingItem)
            );
            api.RegisterCollectibleBehaviorClass(
                "UnconsciousPickupItem",
                typeof(CollectibleBehaviorPickupItem)
            );

            api.RegisterItemClass("ItemSmellingSalts", typeof(ItemSmellingSalts));

            harmony = new Harmony(Mod.Info.ModID);

            //harmony.Patch(
            //    AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.OnGameTick)),
            //    prefix: new HarmonyMethod(
            //        typeof(PlayerControlAndBoxPatch),
            //        nameof(PlayerControlAndBoxPatch.Prefix)
            //    ),
            //    postfix: new HarmonyMethod(
            //        typeof(PlayerControlAndBoxPatch),
            //        nameof(PlayerControlAndBoxPatch.Postfix)
            //    )
            //);

            harmony.Patch(
                AccessTools.Method(typeof(EntityPlayer), nameof(EntityPlayer.OnHurt)),
                new HarmonyMethod(
                    AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.OnHurt))
                )
            );

            harmony.Patch(
                AccessTools.Method(
                    typeof(AiTaskBaseTargetable),
                    nameof(AiTaskBaseTargetable.IsTargetableEntity)
                ),
                new HarmonyMethod(
                    AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.ShouldCancelTarget))
                )
            );

            harmony.Patch(
                AccessTools.Method(
                    typeof(AiTaskBaseTargetable),
                    nameof(AiTaskBaseTargetable.CanSensePlayer)
                ),
                new HarmonyMethod(
                    AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.CanSensePlayer))
                )
            );

            harmony.Patch(
                AccessTools.Method(
                    typeof(EntityBehaviorPlayerRevivable),
                    nameof(EntityBehaviorPlayerRevivable.AttemptRevive)
                ),
                new HarmonyMethod(
                    AccessTools.Method(typeof(PlayerPatch), nameof(PlayerPatch.AttemptRevive))
                )
            );

            base.Start(api);

            api.Network.RegisterChannel(Mod.Info.ModID)
                .RegisterMessageType<SendUnconsciousPacket>()
                .RegisterMessageType<PlayerDeath>()
                .RegisterMessageType<PlayerKill>()
                .RegisterMessageType<PlayerRevive>()
                .RegisterMessageType<PlayerAnimation>()
                .RegisterMessageType<ShowPlayerFinishOffScreenPacket>()
                .RegisterMessageType<ShowUnconciousScreen>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            api.Event.PlayerEntitySpawn += delegate(IClientPlayer player)
            {
                if (player == null || player.Entity == null)
                {
                    capi.Logger.Warning(
                        $"PlayerEntitySpawn: Player or Player.Entity is null. PlayerUID: {player?.PlayerUID ?? "null"}"
                    );
                    return;
                }
            };

            api.Event.PlayerLeave += delegate(IClientPlayer player)
            {
                if (player == null)
                {
                    capi.Logger.Warning("PlayerLeave: Player is null.");
                    return;
                }

                string playerUID = player.PlayerUID;
                if (string.IsNullOrEmpty(playerUID))
                {
                    capi.Logger.Warning("PlayerLeave: PlayerUID is null or empty.");
                    return;
                }
            };

            new ClientMessageHandler().SetMessageHandlers();
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api;

            sapi.Event.ServerRunPhase(
                EnumServerRunPhase.ModsAndConfigReady,
                () =>
                {
                    LoadConfig();
                    new ServerMessageHandler().SetMessageHandlers();

                    sapi.Event.PlayerNowPlaying += (entity) =>
                    {
                        if (!(entity.Entity is EntityPlayer player))
                        {
                            return;
                        }

                        if (string.IsNullOrEmpty(player.PlayerUID))
                        {
                            return;
                        }

                        try
                        {
                            var timerEntry = unconsciousTimers?.Find(timer =>
                                timer.PlayerUID == player.PlayerUID
                            );

                            if (timerEntry == null)
                            {
                                ApplyUnconsciousOnJoin(player, 15);
                            }

                            if (timerEntry != null)
                            {
                                DateTime unconsciousTime = timerEntry.date;
                                double elapsedSeconds = (
                                    DateTime.UtcNow - unconsciousTime
                                ).TotalSeconds;
                                double remainingSeconds = Math.Max(
                                    0,
                                    getConfig().UnconsciousDuration - elapsedSeconds
                                );

                                if (remainingSeconds > 0)
                                {
                                    ApplyUnconsciousOnJoin(player, remainingSeconds);
                                }
                                else
                                {
                                    // Timer expired; remove the entry
                                    unconsciousTimers.Remove(timerEntry);
                                }
                            }

                            // Mod compatibility with "bloodystory"
                            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
                            {
                                try
                                {
                                    BSCompat.AddOnBleedoutEH(player);
                                }
                                catch (Exception ex)
                                {
                                    sapi.Logger.Error(
                                        $"Failed to add bleed-out event handler for player {player.PlayerUID}: {ex}"
                                    );
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            sapi.Logger.Error(
                                $"Error in PlayerNowPlaying for player {player.PlayerUID}: {ex}"
                            );
                        }
                    };

                    new Commands().SetCommands();
                }
            );
        }

        public static void HandlePlayerPickup(EntityPlayer player, float healAmount)
        {
            player.AnimManager.StopAnimation("sleep");
            var health = player.WatchedAttributes.GetTreeAttribute("health");

            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            health.SetFloat("currenthealth", healAmount);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;
            PacketMethods.SendShowUnconciousScreenPacket(false, serverPlayer);

            UnconsciousModSystem.unconsciousTimers.RemoveAll(timer =>
                timer.PlayerUID == player.PlayerUID
            );

            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
            {
                BSCompat.HandleRevive(player);
            }
        }

        public static void HandlePlayerUnconscious(EntityPlayer player, double timer = 0)
        {
            player.AnimManager.StartAnimation("sleep");
            player.WatchedAttributes.SetBool("unconscious", true);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var health = player.WatchedAttributes.GetTreeAttribute("health");
            health.SetFloat("currenthealth", 1);
            player.PlayEntitySound("hurt");

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (getConfig().DropWeaponOnUnconscious)
            {
                PlayerDropActiveItemOnUnconscious(serverPlayer);
            }

            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);
            PacketMethods.SendShowUnconciousScreenPacket(true, serverPlayer, (int)timer);

            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
            {
                BSCompat.HandleUnconscious(player);
            }
        }

        public static void HandlePlayerUnconscious(EntityPlayer player, DamageSource damageSource)
        {
            player.AnimManager.StartAnimation("sleep");
            player.WatchedAttributes.SetBool("unconscious", true);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            var health = player.WatchedAttributes.GetTreeAttribute("health");
            health.SetFloat("currenthealth", 1);
            player.PlayEntitySound("hurt");

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (getConfig().DropWeaponOnUnconscious)
            {
                PlayerDropActiveItemOnUnconscious(serverPlayer);
            }
            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);
            PacketMethods.SendShowUnconciousScreenPacket(true, serverPlayer);
            //serverPlayer.Entity.CollisionBox.Set(-0.3f, 0f, -0.9f, 0.3f, 0.3f, 0.9f); // Adjust selection box to be lower when unconscious

            unconsciousTimers.Add(
                new UnconsciousTimer { PlayerUID = serverPlayer.PlayerUID, date = DateTime.UtcNow }
            );
            sapi.Logger.Audit(
                "Player {0} got unconscious by {1} at {2}",
                serverPlayer.Entity.GetName(),
                damageSource.SourceEntity,
                serverPlayer.Entity.Pos
            );

            if (getSAPI().ModLoader.GetMod("bloodystory") != null)
            {
                BSCompat.HandleUnconscious(player);
            }
        }

        public static void PlayerDropActiveItemOnUnconscious(IPlayer serverPlayer)
        {
            ItemSlot activeSlot = serverPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot?.Itemstack == null)
                return; // No item to drop\

            ItemStack itemToDrop = activeSlot.Itemstack.Clone();

            activeSlot.TakeOutWhole();
            activeSlot.MarkDirty();

            Vec3d forwardDirection = serverPlayer.Entity.Pos.AheadCopy(1).XYZ.Normalize();
            Vec3d throwVelocity = forwardDirection * 2.5; // Increase this value for further throws
            serverPlayer.Entity.World.SpawnItemEntity(
                itemToDrop,
                serverPlayer.Entity.Pos.XYZ,
                throwVelocity
            );
            activeSlot.Itemstack = null; // Clear the slot
            serverPlayer.InventoryManager.BroadcastHotbarSlot();
        }

        private void ApplyUnconsciousOnJoin(EntityPlayer player, double diffInSeconds)
        {
            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (serverPlayer.Entity.IsUnconscious())
            {
                HandlePlayerUnconscious(serverPlayer.Entity, diffInSeconds);
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }
    }
}
