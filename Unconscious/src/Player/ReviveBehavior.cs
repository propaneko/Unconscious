using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using static System.Net.Mime.MediaTypeNames;

namespace Unconscious.src.Player
{
    public class ReviveBehavior : EntityBehavior
    {
        private bool isHolding = false; // Track if the right-click is being held
        private bool isCarried = false; // Track if the right-click is being held
        private bool isCarriedCooldown = false; // Track if the right-click is being held

        private Vec3d initialPosition;  // Tracks the player's initial position when holding starts
        long reviveCancelcallbackId;
        long pickupCancelcallbackId;


        private float holdReviveTime = 0;    // Track the duration of the hold
        private float holdCarryTime = 0;    // Track the duration of the hold

        private readonly ICoreServerAPI sapi;

        private Cuboidf originalCollisionBox;
        private Cuboidf originalSelectionBox;

        public ReviveBehavior(Entity entity) : base(entity)
        {
            sapi = entity.Api as ICoreServerAPI;
        }

        public override string PropertyName()
        {
            return "ReviveBehavior";
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            // Save the original collision and selection boxes when the entity is initialized
            originalCollisionBox = entity.CollisionBox.Clone();
            originalSelectionBox = entity.SelectionBox.Clone();
        }

        public override void GetInfoText(StringBuilder infotext)
        {
            base.GetInfoText(infotext);
            if (entity.WatchedAttributes.GetBool("unconscious"))
            {
                infotext.AppendLine("Unconscious: Shift + Right Click to Revive");
            }
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            if (es == null || !(es.Entity is EntityPlayer))
            {
                return base.GetInteractionHelp(world, es, player, ref handled);
            }

            if (!entity.WatchedAttributes.GetBool("unconscious"))
                return base.GetInteractionHelp(world, es, player, ref handled);

            // Define the items to display
            ItemStack weakSalts = new ItemStack(world.GetItem(new AssetLocation("unconscious:smellingsalts-weak")));
            ItemStack strongSalts = new ItemStack(world.GetItem(new AssetLocation("unconscious:smellingsalts-strong")));

            if (weakSalts == null || strongSalts == null)
            {
                return base.GetInteractionHelp(world, es, player, ref handled);
            }

            return new WorldInteraction[]
            {
                new WorldInteraction
                {
                    ActionLangCode = "Pick Up",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right,
                },
                new WorldInteraction
                {
                    ActionLangCode = "Revive",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = new ItemStack[] { weakSalts, strongSalts },
                },
            };
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            // Example: Check if the player is unconscious (use your existing logic)
            if (entity.WatchedAttributes.GetBool("unconscious"))
            {
                SetUnconsciousHitbox();
            }
            else if (!entity.WatchedAttributes.GetBool("unconscious"))
            {
                RestoreDefaultHitbox();
            }
        }

        private void SetUnconsciousHitbox()
        {
           // Set collision box for side-lying posture (sleep animation), centered at X=0.3, Z=0.5
            entity.CollisionBox.X1 = -0.55f; // Center: 0.3 - 0.15 = 0.15
            entity.CollisionBox.Y1 = 0.0f;  // Bottom at ground level
            entity.CollisionBox.Z1 = -0.65f; // Center: 0.5 - 1.15 = -0.65
            entity.CollisionBox.X2 = 0.45f; // Width: 0.45 - 0.15 = 0.3 blocks
            entity.CollisionBox.Y2 = 0.9f;  // Height: 0.6 blocks
            entity.CollisionBox.Z2 = 0.15f; // Length: 1.15 - (-0.65) = 1.8 blocks

            // Set selection box to match, slightly larger for targeting
            entity.SelectionBox.X1 = -0.5f;  // Center: 0.3 - 0.2 = 0.1
            entity.SelectionBox.Y1 = 0.0f;  // Bottom at ground level
            entity.SelectionBox.Z1 = -0.75f; // Center: 0.5 - 1.25 = -0.75
            entity.SelectionBox.X2 = 0.5f;  // Width: 0.5 - 0.1 = 0.4 blocks
            entity.SelectionBox.Y2 = 0.7f;  // Height: 0.7 blocks
            entity.SelectionBox.Z2 = 0.25f; // Length: 1.25 - (-0.75) = 2.0 blocks

        }

        private void RestoreDefaultHitbox()
        {
            entity.CollisionBox.X1 = originalCollisionBox.X1;
            entity.CollisionBox.Y1 = originalCollisionBox.Y1;
            entity.CollisionBox.Z1 = originalCollisionBox.Z1;
            entity.CollisionBox.X2 = originalCollisionBox.X2;
            entity.CollisionBox.Y2 = originalCollisionBox.Y2;
            entity.CollisionBox.Z2 = originalCollisionBox.Z2;

            entity.SelectionBox.X1 = originalSelectionBox.X1;
            entity.SelectionBox.Y1 = originalSelectionBox.Y1;
            entity.SelectionBox.Z1 = originalSelectionBox.Z1;
            entity.SelectionBox.X2 = originalSelectionBox.X2;
            entity.SelectionBox.Y2 = originalSelectionBox.Y2;
            entity.SelectionBox.Z2 = originalSelectionBox.Z2;
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (entity.WatchedAttributes.GetBool("unconscious"))
            {
                SetUnconsciousHitbox();
            }
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

            var helditem = interactingPlayer.ActiveHandItemSlot.Itemstack;

            string characterClass;
            SyncedTreeAttribute watchedAttributes = interactingPlayer.WatchedAttributes;
            characterClass = ((watchedAttributes != null) ? watchedAttributes.GetAsString("characterClass", null) : null);

            if (string.IsNullOrEmpty(characterClass))
            {
                return;
            }

            if (UnconsciousModSystem.getConfig().RequireSmellingSaltsForRevive && !UnconsciousModSystem.getConfig().ReviveClassWhitelist.Contains(characterClass))
            {
                if (helditem == null) return;
                if (helditem != null && !helditem.Collectible.Code.Path.Contains("smellingsalts")) return;
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

            if (helditem != null && helditem.Collectible.Code.Path.Contains("smellingsalts-strong"))
            {
                holdReviveTime += UnconsciousModSystem.getConfig().ReviveClassWhitelist.Contains(characterClass) ? +0.2f : 0f;
                holdReviveTime += UnconsciousModSystem.getConfig().RevivePerTickDuration + 0.1f;
            } 
            else if (helditem != null && helditem.Collectible.Code.Path.Contains("smellingsalts-weak")) 
            {
                holdReviveTime += UnconsciousModSystem.getConfig().ReviveClassWhitelist.Contains(characterClass) ? +0.2f : 0f;
                holdReviveTime += UnconsciousModSystem.getConfig().RevivePerTickDuration;
            }
            else
            {
                holdReviveTime += UnconsciousModSystem.getConfig().ReviveClassWhitelist.Contains(characterClass) ? +0.2f : 0f;
                holdReviveTime += UnconsciousModSystem.getConfig().RevivePerTickDuration;
            }

            if (holdReviveTime > 10f)
            {
                FinishRunLongPressAction(interactingPlayer);

                isHolding = false;
                holdReviveTime = 0;
                initialPosition = null;
            }
            else
            {
                string text = "SpeedCycle";
                EntityPlayer entityPlayer = entity as EntityPlayer;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
                
                defaultInterpolatedStringHandler.AppendLiteral(Lang.Get("unconscious:reviving-progress"));
                defaultInterpolatedStringHandler.AppendFormatted(Math.Truncate(holdReviveTime * 10f));
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
            holdReviveTime = 0;
            initialPosition = null;

            SendErrorMessage(Lang.Get("unconscious:reviving-cancel"), interactingPlayer);
        }

        private void FinishRunLongPressAction(EntityPlayer interactingPlayer)
        {
            var player = entity as EntityPlayer;

            SendErrorMessage(($"{player.Player.PlayerName} {Lang.Get("unconscious:reviving-success")}"), interactingPlayer);

            if (UnconsciousModSystem.getConfig().RequireSmellingSaltsForRevive)
            {
                var helditem = interactingPlayer.ActiveHandItemSlot.Itemstack;
                if (helditem != null && helditem.Collectible.Code.Path.Contains("smellingsalts"))
                {
                    var activeSlot = interactingPlayer.Player.InventoryManager.ActiveHotbarSlot;

                    activeSlot.TakeOut(1);
                    activeSlot.MarkDirty();

                    float healingPotency = helditem.Collectible.Code.Path.Contains("smellingsalts-strong") ? UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive + 0.30f : UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive;

                    UnconsciousModSystem.HandlePlayerPickup(player, healingPotency);
                    return;
                };
            }

            UnconsciousModSystem.HandlePlayerPickup(player, UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive);
        }

    }
}
