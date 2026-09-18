using System;
using System.Collections.Generic;
using System.Linq;
using Unconscious.Behaviors.Items;
using Unconscious.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Unconscious.Behaviors.Player
{
    public class BehaviorPickupPlayer : EntityBehavior
    {
        public bool isCarrying;
        public bool isCarryCooldown;
        public Vec3d initialPosition;
        public long pickupCancelCallbackId;
        public long carryTickListenerId;
        private readonly ICoreAPI api;
        public string carriedPlayerUID;

        protected IProgressBar progressBarRender;

        public BehaviorPickupPlayer(Entity entity) : base(entity)
        {
            api = entity.Api;
        }

        public override string PropertyName() => "UnconsciousPickupBehavior";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            if (entity.World.Side == EnumAppSide.Server && api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;
                sapi.Event.PlayerLeave += OnPlayerDisconnect;
            }
        }

        private void OnPlayerDisconnect(IPlayer player)
        {
            if (isCarrying && player.Entity.PlayerUID == carriedPlayerUID)
            {
                if (entity is EntityPlayer interactingPlayer && api.World.PlayerByUid(carriedPlayerUID)?.Entity is EntityPlayer targetPlayer)
                {
                    StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-disconnect"));
                    if (api != null)
                    {
                        api.Logger.Debug("ReviveBehavior: Stopped carrying player {0} due to disconnect for entity {1}", carriedPlayerUID, entity.EntityId);
                    }
                }
                carriedPlayerUID = null;
            }
        }


        public static WorldInteraction GetPicupInteractionHelp(ICoreAPI api)
        {
            ItemStack[] orCreate = ObjectCacheUtil.GetOrCreate(api, "captureropeStacks", delegate
            {
                List<ItemStack> list = new List<ItemStack>();
                foreach (Item item in api.World.Items)
                {
                    if (item.HasBehavior<CollectibleBehaviorPickupItem>())
                    {
                        list.Add(new ItemStack(item));
                    }
                }

                return list.ToArray();
            });
            return new WorldInteraction
            {
                ActionLangCode = Lang.Get("unconscious:pickupplayer"),
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "ctrl",
                Itemstacks = orCreate,
                RequireFreeHand = true
            };
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            WorldInteraction[] array = base.GetInteractionHelp(world, es, player, ref handled);
            bool isUnconscious = entity.WatchedAttributes.GetAsBool("unconscious", false);
            if (isUnconscious && UnconsciousModSystem.getConfig().EnableTeleportCarry)
            {
                if (array == null)
                {
                    array = Array.Empty<WorldInteraction>();
                }

                array = array.Append(GetPicupInteractionHelp(world.Api));
            }

            return array;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

            if (byEntity is not EntityPlayer interactingPlayer || entity is not EntityPlayer targetPlayer || mode != EnumInteractMode.Interact)
            {
                return;
            }

            if (byEntity.World.Side != EnumAppSide.Server)
            {
                return;
            }

            else if (byEntity.Controls.CtrlKey && targetPlayer.IsUnconscious() && UnconsciousModSystem.getConfig().EnableTeleportCarry && !isCarryCooldown)
            {
                initialPosition ??= interactingPlayer.Pos.AsBlockPos.ToVec3d();
                api.Event.UnregisterCallback(pickupCancelCallbackId);
                if (isCarrying)
                {
                    StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-putdown"));
                }
            }
        }

        private void SendErrorMessage(string message, EntityPlayer interactingPlayer)
        {
            var serverPlayer = interactingPlayer.Player as IServerPlayer;
            ICoreServerAPI sapi = api as ICoreServerAPI;

            sapi.SendIngameError(serverPlayer, "ReviveCycle", message, Array.Empty<object>());
        }

        // Legacy path, kept behind EnableTeleportCarry. Dragging with a rope (ItemRope plus the ropetieable behavior) is the default way now.
        public void StartCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            isCarrying = true;
            interactingPlayer.WatchedAttributes.SetBool("carryingUnconciousPlayer", true);
            interactingPlayer.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");

            ICoreServerAPI sapi = api as ICoreServerAPI;
            sapi.Event.UnregisterCallback(pickupCancelCallbackId);
            sapi.Event.UnregisterGameTickListener(carryTickListenerId);

            var serverPlayer = sapi.World.PlayerByUid(interactingPlayer.PlayerUID) as IServerPlayer;

            if (!IsWithinDistance(interactingPlayer, targetPlayer, 2))
            {
                StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-toofar"));
                return;
            }

            if (isCarrying)
            {
                carriedPlayerUID = targetPlayer.PlayerUID;
                SendErrorMessage($"{Lang.Get("unconscious:pickup-success")} {targetPlayer.Player.PlayerName}", interactingPlayer);
                serverPlayer.Entity.Stats.Set("walkspeed", "unconsciousCarry", -0.60f, false);

                carryTickListenerId = sapi.Event.RegisterGameTickListener(dt =>
                {
                    if (!isCarrying || !serverPlayer.Entity.WatchedAttributes.GetBool("carryingUnconciousPlayer"))
                    {
                        StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-drop"));
                        return;
                    }

                    if (!IsWithinDistance(interactingPlayer, targetPlayer, 5))
                    {
                        StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-distance"));
                        return;
                    }

                    if (interactingPlayer.IsUnconscious())
                    {
                        StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-unconscious"));
                        return;
                    }

                    if (!targetPlayer.IsUnconscious())
                    {
                        StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-wokeup"));
                        return;
                    }


                    var carryPosition = interactingPlayer.Pos.XYZ.Add(-0.5, 0, -0.5);
                    targetPlayer.TeleportTo(carryPosition);
                }, 1000);
            }
        }


        public void StopCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer, string message)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;

            isCarrying = false;
            isCarryCooldown = true;
            var serverPlayer = sapi.World.PlayerByUid(interactingPlayer.PlayerUID) as IServerPlayer;
            serverPlayer.Entity.Stats.Remove("walkspeed", "unconsciousCarry");
            serverPlayer.Entity.WatchedAttributes.SetBool("carryingUnconciousPlayer", false);
            serverPlayer.Entity.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");
            sapi.Event.UnregisterGameTickListener(carryTickListenerId);
            SendErrorMessage(message, interactingPlayer);

            sapi.Event.RegisterCallback(_ => isCarryCooldown = false, 2000);
        }

        private bool IsWithinDistance(EntityPlayer player1, EntityPlayer player2, double maxDistance)
        {
            return player1.Pos.XYZ.DistanceTo(player2.Pos.XYZ) < maxDistance;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            base.OnEntityDespawn(despawn);
        }
    }
}