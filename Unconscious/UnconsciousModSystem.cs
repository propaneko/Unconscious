using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using Unconscious.src.Commands;
using Unconscious.src.Config;
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
using System;
using Unconscious.src.Renderers;
using Vintagestory.API.Datastructures;
using System.Linq;
using Vintagestory.Server;

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

        private Dictionary<string, UnconsciousIndicatorRenderer> renderers = new Dictionary<string, UnconsciousIndicatorRenderer>();
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
                    },
                    ReviveClassWhitelist = new string[]
                    {
                        "medic",
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
            api.RegisterEntityBehaviorClass("revivebehavior", typeof(ReviveBehavior));

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

        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api;

            api.Event.PlayerEntitySpawn += delegate (IClientPlayer player)
            {
                if (player == null || player.Entity == null)
                {
                    capi.Logger.Warning($"PlayerEntitySpawn: Player or Player.Entity is null. PlayerUID: {player?.PlayerUID ?? "null"}");
                    return;
                }

                try
                {
                    var renderer = new UnconsciousIndicatorRenderer(api, player.Entity);
                    api.Event.RegisterRenderer(renderer, EnumRenderStage.Ortho, null);
                    renderers[player.PlayerUID] = renderer;
                    capi.Logger.Debug($"Registered renderer for player {player.PlayerUID} (EntityId: {player.Entity.EntityId})");
                }
                catch (Exception ex)
                {
                    capi.Logger.Error($"Failed to register renderer for player {player.PlayerUID}: {ex}");
                }
            };

            api.Event.PlayerLeave += delegate (IClientPlayer player)
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

                if (renderers.TryGetValue(playerUID, out var renderer))
                {
                    try
                    {
                        api.Event.UnregisterRenderer(renderer, EnumRenderStage.Ortho);
                        renderer.Dispose();
                        renderers.Remove(playerUID);
                        capi.Logger.Debug($"Unregistered renderer for player {playerUID}");
                    }
                    catch (Exception ex)
                    {
                        capi.Logger.Error($"Failed to unregister renderer for player {playerUID}: {ex}");
                    }
                }
                else
                {
                    capi.Logger.Warning($"No renderer found for player {playerUID} in PlayerLeave event.");
                }
            };


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
                        var timerEntry = unconsciousTimers?.Find(timer => timer.PlayerUID == player.PlayerUID);

                        if (timerEntry == null)
                        {
                            ApplyUnconsciousOnJoin(player, 15);
                        }

                        if (timerEntry != null)
                        {
                            DateTime unconsciousTime = timerEntry.date;
                            double elapsedSeconds = (DateTime.UtcNow - unconsciousTime).TotalSeconds;
                            double remainingSeconds = Math.Max(0, getConfig().UnconsciousDuration - elapsedSeconds);

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
                                sapi.Logger.Error($"Failed to add bleed-out event handler for player {player.PlayerUID}: {ex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Error($"Error in PlayerNowPlaying for player {player.PlayerUID}: {ex}");
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

            UnconsciousModSystem.unconsciousTimers.RemoveAll(timer => timer.PlayerUID == player.PlayerUID);

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
            player.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

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
            player.PlayEntitySound("hurt", null, randomizePitch: true, 24f);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            if (getConfig().DropWeaponOnUnconscious)
            {
                PlayerDropActiveItemOnUnconscious(serverPlayer);
            }
            PacketMethods.SendAnimationPacketToClient(true, "sleep", serverPlayer);
            PacketMethods.SendShowUnconciousScreenPacket(true, serverPlayer);
            serverPlayer.Entity.CollisionBox.Set(-0.3f, 0f, -0.9f, 0.3f, 0.3f, 0.9f); // Adjust selection box to be lower when unconscious

            unconsciousTimers.Add(new UnconsciousTimer { PlayerUID = serverPlayer.PlayerUID, date = DateTime.UtcNow });
            sapi.Logger.Audit("Player {0} got unconscious by {1} at {2}", serverPlayer.Entity.GetName(), damageSource.SourceEntity, serverPlayer.Entity.Pos);

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
            foreach (var renderer in renderers.Values)
            {
                try
                {
                    renderer.Dispose();
                    capi.Event.UnregisterRenderer(renderer, EnumRenderStage.Ortho);
                }
                catch (Exception ex)
                {
                    capi.Logger.Error($"Error disposing renderer: {ex}");
                }
            }
            renderers.Clear();
            harmony?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }
    }
}
