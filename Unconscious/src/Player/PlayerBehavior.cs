using Cairo;
using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Unconscious.src.Player
{
    public class PlayerBehavior : EntityBehavior
    {
        private bool isHolding = false; // Track if the right-click is being held
        private bool isCarried = false; // Track if the right-click is being held
        private bool isCarriedCooldown = false; // Track if the right-click is being held

        private Vec3d initialPosition;  // Tracks the player's initial position when holding starts
        long reviveCancelcallbackId;
        long pickupCancelcallbackId;


        private float holdRevileTime = 0;    // Track the duration of the hold
        private float holdCarryTime = 0;    // Track the duration of the hold

        private readonly ICoreServerAPI sapi;
        public PlayerBehavior(Entity entity) : base(entity)
        {
            sapi = entity.Api as ICoreServerAPI;
        }

        public override string PropertyName()
        {
            return "reviveBehavior";
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);


            var targetPlayer = entity as EntityPlayer;

            if (byEntity is EntityAgent interactingPlayer)
            {
                var entityPlayer = byEntity as EntityAgent;
                if (entityPlayer != null && mode == EnumInteractMode.Interact && byEntity.Controls.Sneak && byEntity.World.Side == EnumAppSide.Server)
                {
                    if (!targetPlayer.IsUnconscious())
                    {
                        return;
                    }

                    isHolding = true;
                    if (initialPosition == null)
                    {
                        initialPosition = interactingPlayer.Pos.AsBlockPos.ToVec3d();
                    }
                    sapi.Event.UnregisterCallback(reviveCancelcallbackId);
                    HandleReviveHold(byEntity as EntityPlayer, targetPlayer);

                }
               
                if (
                    entityPlayer != null &&
                    mode == EnumInteractMode.Interact &&
                    byEntity.Controls.CtrlKey &&
                    byEntity.World.Side == EnumAppSide.Server
                    )
                {
                    if (!UnconsciousModSystem.getConfig().EnableCarryMechanic)
                    {
                        return;
                    }

                    if (!targetPlayer.IsUnconscious())
                    {
                        return;
                    }

                    if (initialPosition == null)
                    {
                        initialPosition = interactingPlayer.Pos.AsBlockPos.ToVec3d();
                    }

                    if (isCarriedCooldown)
                    {
                        return;
                    }

                    if (!isCarried)
                    {
                        sapi.Event.UnregisterCallback(pickupCancelcallbackId);
                        HandleCarryHold(byEntity as EntityPlayer, targetPlayer);
                    }
                    else
                    {
                        StopCarryingPlayer(byEntity as EntityPlayer, targetPlayer, Lang.Get("unconscious:pickup-putdown"));
                    }

                }
            }
        }

        private void SendErrorMessage(string text, EntityPlayer interactingPlayer)
        {
            string mainText = "ReviveCycle";
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
            defaultInterpolatedStringHandler.AppendLiteral(text);
            sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, mainText, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
        }

        long eventlistenerid;
        private void StartCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            sapi.Event.UnregisterCallback(pickupCancelcallbackId);
            sapi.Event.UnregisterGameTickListener(eventlistenerid);

            IServerPlayer interactingServerPlayer = sapi.World.PlayerByUid(interactingPlayer.PlayerUID) as IServerPlayer;

            if (!IsWithinDistance(interactingPlayer, targetPlayer, 2))
            {
                StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-toofar"));
                sapi.Event.UnregisterGameTickListener(eventlistenerid);
                return;
            }

            if (isCarried)
            {
                SendErrorMessage($"{Lang.Get("unconscious:pickup-success")} {targetPlayer.Player.PlayerName}", interactingPlayer);
                interactingServerPlayer.Entity.Stats.Remove("walkspeed", "unconsciousCarry");
                interactingServerPlayer.Entity.Stats.Set("walkspeed", "unconsciousCarry", -0.60f, false);

                eventlistenerid = sapi.Event.RegisterGameTickListener((dt) =>
                {
                    interactingServerPlayer = sapi.World.PlayerByUid(interactingPlayer.PlayerUID) as IServerPlayer;
                    if (!isCarried && !interactingServerPlayer.Entity.WatchedAttributes.GetBool("carryingUnconciousPlayer", false))
                    {
                        StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-drop"));
                        return;
                    }

                    if (!IsWithinDistance(interactingPlayer, targetPlayer, 2))
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

                    Vec3d carryPosition = interactingPlayer.ServerPos.XYZ.Add(-0.5, 0, -0.5);
                    targetPlayer.Pos.SetPos(carryPosition);

                    if(targetPlayer.ServerPos.XYZ != interactingPlayer.ServerPos.XYZ)
                    {
                        targetPlayer.TeleportTo(carryPosition);
                    }
                }, 200);
            }    
        }

        private void StopCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer, string errorMessage)
        {
            isCarried = false;
            IServerPlayer interactingServerPlayer = sapi.World.PlayerByUid(interactingPlayer.PlayerUID) as IServerPlayer;
            interactingServerPlayer.Entity.Stats.Remove("walkspeed", "unconsciousCarry");

            interactingServerPlayer.Entity.WatchedAttributes.SetBool("carryingUnconciousPlayer", false);
            interactingServerPlayer.Entity.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");
            sapi.Event.UnregisterGameTickListener(eventlistenerid);
            SendErrorMessage(errorMessage, interactingPlayer);
        }

        private bool IsWithinDistance(EntityPlayer player1, EntityPlayer player2, double maxDistance)
        {
            // Get the positions of the players
            Vec3d pos1 = player1.ServerPos.XYZ;
            Vec3d pos2 = player2.ServerPos.XYZ;

            // Calculate the distance between them
            double distance = pos1.DistanceTo(pos2);

            // Check if the distance is less than the specified maxDistance
            return distance < maxDistance;
        }

        private void HandleCarryHold(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {

            pickupCancelcallbackId = sapi.Event.RegisterCallback((dt) =>
            {
                CancelCarryHold(interactingPlayer);
            }, 2000);

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelCarryHold(interactingPlayer);
                return;
            }

            holdCarryTime += UnconsciousModSystem.getConfig().PickupPerTickDuration; ; // Increment hold time (50ms = 0.05s)

            if (holdCarryTime > 10f)
            {
                isCarried = true;
                interactingPlayer.WatchedAttributes.SetBool("carryingUnconciousPlayer", true);
                interactingPlayer.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");
                StartCarryingPlayer(interactingPlayer, targetPlayer);
                holdCarryTime = 0;
                initialPosition = null;
                isCarriedCooldown = true;
                sapi.Event.RegisterCallback((dt) =>
                {
                    isCarriedCooldown = false;
                }, 2000);
            }
            else
            {
                string text = "SpeedCycle";
                EntityPlayer entityPlayer = entity as EntityPlayer;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);

                defaultInterpolatedStringHandler.AppendLiteral(Lang.Get("unconscious:pickup-progress"));
                defaultInterpolatedStringHandler.AppendFormatted(Math.Truncate(holdCarryTime * 10f));
                defaultInterpolatedStringHandler.AppendLiteral("%");

                sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
            }
        }

        private void HandleReviveHold(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            reviveCancelcallbackId = sapi.Event.RegisterCallback((dt) =>
            {
                CancelReviveHold(interactingPlayer);
            }, 2000);

            if (!isHolding) return;

            if (UnconsciousModSystem.getConfig().RequireTemporalGearForRevive)
            {
                var helditem = interactingPlayer.ActiveHandItemSlot.Itemstack;
                if (helditem == null) return;
                if (helditem != null && helditem.Collectible.Code.Path != "gear-temporal") return;
            }

            if (!IsWithinDistance(interactingPlayer, targetPlayer, 2))
            {
                isCarried = false;
                sapi.Event.UnregisterGameTickListener(eventlistenerid);
                return;
            }

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelReviveHold(interactingPlayer);
                return;
            }

            holdRevileTime += UnconsciousModSystem.getConfig().RevivePerTickDuration; // Increment hold time (50ms = 0.05s)

            if (holdRevileTime > 10f)
            {
                RunLongPressAction(interactingPlayer);

                isHolding = false;
                holdRevileTime = 0;
                initialPosition = null;
            }
            else
            {
                string text = "SpeedCycle";
                EntityPlayer entityPlayer = entity as EntityPlayer;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
                
                defaultInterpolatedStringHandler.AppendLiteral(Lang.Get("unconscious:reviving-progress"));
                defaultInterpolatedStringHandler.AppendFormatted(Math.Truncate(holdRevileTime * 10f));
                defaultInterpolatedStringHandler.AppendLiteral("%");

                sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
            }
        }

        private bool HasPlayerMoved(EntityPlayer player)
        {
            Vec3d currentPosition = player.Pos.AsBlockPos.ToVec3d();
            return !currentPosition.Equals(initialPosition);
        }

        private void CancelCarryHold(EntityPlayer interactingPlayer)
        {
            isCarried = false;
            holdCarryTime = 0;
            initialPosition = null;

            SendErrorMessage(Lang.Get("unconscious:reviving-cancel"), interactingPlayer);
        }

        private void CancelReviveHold(EntityPlayer interactingPlayer)
        {
            isHolding = false;
            holdRevileTime = 0;
            initialPosition = null;

            SendErrorMessage(Lang.Get("unconscious:reviving-cancel"), interactingPlayer);
        }

        private void RunLongPressAction(EntityPlayer interactingPlayer)
        {
            var player = entity as EntityPlayer;

            SendErrorMessage(($"{player.Player.PlayerName} {Lang.Get("unconscious:reviving-success")}"), interactingPlayer);

            if (UnconsciousModSystem.getConfig().RequireTemporalGearForRevive)
            {
                var helditem = interactingPlayer.ActiveHandItemSlot.Itemstack;
                if (helditem != null && helditem.Collectible.Code.Path == "gear-temporal")
                {
                    var activeSlot = interactingPlayer.Player.InventoryManager.ActiveHotbarSlot;

                    activeSlot.TakeOut(1);
                    activeSlot.MarkDirty();
                };
            }

            UnconsciousModSystem.HandlePlayerPickup(player);
        }

    }
}
