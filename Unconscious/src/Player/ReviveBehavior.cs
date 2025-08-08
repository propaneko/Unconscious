using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious.src.Player
{
    public class ReviveBehavior : EntityBehavior
    {
        private bool isHoldingRevive; // Tracks if revive action is being held
        private bool isCarrying;      // Tracks if player is being carried
        private bool isCarryCooldown; // Tracks carry action cooldown
        private Vec3d initialPosition; // Tracks player's initial position when holding starts
        private float holdReviveTime; // Tracks duration of revive hold
        private float holdCarryTime;  // Tracks duration of carry hold
        private long reviveCancelCallbackId;
        private long pickupCancelCallbackId;
        private long carryTickListenerId;
        private readonly Cuboidf originalCollisionBox;
        private readonly Cuboidf originalSelectionBox;
        private readonly ICoreServerAPI sapi;
        private readonly ICoreClientAPI capi; // Added for client-side rendering
        private WorldInteraction[] interactions;
        private MeshRef circleMeshRef;
        private ProgressCircleRenderer progressRenderer; // Changed to concrete type for clarity
        private bool wasUnconscious; // Tracks previous unconscious state
        private string carriedPlayerUID; // Tracks the UID of the player being carried

        public ReviveBehavior(Entity entity) : base(entity)
        {
            sapi = entity.Api as ICoreServerAPI;
            capi = entity.Api as ICoreClientAPI; // Initialize client API
            originalCollisionBox = entity.CollisionBox.Clone();
            originalSelectionBox = entity.SelectionBox.Clone();
        }

        public override string PropertyName() => "ReviveBehavior";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            // Register client-side renderer for progress bar
            if (entity.World.Side == EnumAppSide.Client && capi != null)
            {
                progressRenderer = new ProgressCircleRenderer(this, capi);
                capi.Event.RegisterRenderer(progressRenderer, EnumRenderStage.Ortho);
            }

            if (entity.World.Side == EnumAppSide.Server && sapi != null)
            {
                sapi.Event.PlayerLeave += OnPlayerDisconnect;
            }

            bool isUnconscious = entity.WatchedAttributes.GetBool("unconscious");
            if (isUnconscious != wasUnconscious)
            {
                if (isUnconscious)
                {
                    SetUnconsciousHitbox();
                    if (sapi != null)
                    {
                        sapi.Logger.Debug("ReviveBehavior: Set unconscious hitbox for entity {0} on Initialize", entity.EntityId);
                    }
                }
                wasUnconscious = isUnconscious;
            }
        }

        private void OnPlayerDisconnect(IPlayer player)
        {
            if (isCarrying && player.Entity.PlayerUID == carriedPlayerUID)
            {
                var interactingPlayer = entity as EntityPlayer;
                var targetPlayer = sapi.World.PlayerByUid(carriedPlayerUID)?.Entity as EntityPlayer;
                if (interactingPlayer != null && targetPlayer != null)
                {
                    StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-error-disconnect"));
                    if (sapi != null)
                    {
                        sapi.Logger.Debug("ReviveBehavior: Stopped carrying player {0} due to disconnect for entity {1}", carriedPlayerUID, entity.EntityId);
                    }
                }
                carriedPlayerUID = null;
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            // Check for unconscious state changes
            bool isUnconscious = entity.WatchedAttributes.GetBool("unconscious");
            if (isUnconscious != wasUnconscious)
            {
                if (isUnconscious)
                {
                    SetUnconsciousHitbox();
                    if (sapi != null)
                    {
                        sapi.Logger.Debug("ReviveBehavior: Set unconscious hitbox for entity {0} on state change", entity.EntityId);
                    }
                }
                else
                {
                    RestoreDefaultHitbox();
                    if (sapi != null)
                    {
                        sapi.Logger.Debug("ReviveBehavior: Restored default hitbox for entity {0} on state change", entity.EntityId);
                    }
                }
                wasUnconscious = isUnconscious; // Update state
            }
        }

        private void SetUnconsciousHitbox()
        {
            entity.CollisionBox.Set(
                x1: -0.55f, y1: 0.0f, z1: -0.65f,
                x2: 0.45f, y2: 0.9f, z2: 0.15f
            );

            entity.SelectionBox.Set(
                x1: -0.5f, y1: 0.0f, z1: -0.75f,
                x2: 0.5f, y2: 0.7f, z2: 0.25f
            );
        }

        private void RestoreDefaultHitbox()
        {
            entity.CollisionBox.Set(originalCollisionBox);
            entity.SelectionBox.Set(originalSelectionBox);
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

            if (byEntity is not EntityPlayer interactingPlayer || entity is not EntityPlayer targetPlayer || mode != EnumInteractMode.Interact)
            {
                return;
            }

            if (byEntity.World.Side != EnumAppSide.Server)
            {
                return;
            }

            if (byEntity.Controls.Sneak && targetPlayer.IsUnconscious())
            {
                isHoldingRevive = true;
                initialPosition ??= interactingPlayer.Pos.AsBlockPos.ToVec3d();
                sapi.Event.UnregisterCallback(reviveCancelCallbackId);
                HandleReviveHold(interactingPlayer, targetPlayer);
            }
            else if (byEntity.Controls.CtrlKey && targetPlayer.IsUnconscious() && UnconsciousModSystem.getConfig().EnableCarryMechanic && !isCarryCooldown)
            {
                initialPosition ??= interactingPlayer.Pos.AsBlockPos.ToVec3d();
                sapi.Event.UnregisterCallback(pickupCancelCallbackId);
                if (isCarrying)
                {
                    StopCarryingPlayer(interactingPlayer, targetPlayer, Lang.Get("unconscious:pickup-putdown"));
                }
                else
                {
                    HandleCarryHold(interactingPlayer, targetPlayer);
                }
            }
        }

        private void SendErrorMessage(string message, EntityPlayer interactingPlayer)
        {
            var serverPlayer = interactingPlayer.Player as IServerPlayer;
            sapi.SendIngameError(serverPlayer, "ReviveCycle", message, Array.Empty<object>());
        }

        private void StartCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
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


                    var carryPosition = interactingPlayer.ServerPos.XYZ.Add(-0.5, 0, -0.5);
                    targetPlayer.Pos.SetPos(carryPosition);
                    if (!targetPlayer.ServerPos.XYZ.Equals(carryPosition))
                    {
                        targetPlayer.TeleportTo(carryPosition);
                    }
                }, 200);
            }
        }

        private void StopCarryingPlayer(EntityPlayer interactingPlayer, EntityPlayer targetPlayer, string message)
        {
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
            return player1.ServerPos.XYZ.DistanceTo(player2.ServerPos.XYZ) < maxDistance;
        }

        private void HandleCarryHold(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            pickupCancelCallbackId = sapi.Event.RegisterCallback(_ => CancelCarryHold(interactingPlayer), 2000);

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelCarryHold(interactingPlayer);
                return;
            }

            entity.WatchedAttributes.SetLong("interactingPlayerId", interactingPlayer.EntityId);
            entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");

            holdCarryTime += UnconsciousModSystem.getConfig().PickupPerTickDuration;
            // Update entity attribute for client-side rendering
            entity.WatchedAttributes.SetFloat("carryProgress", holdCarryTime / 10f);
            entity.WatchedAttributes.MarkPathDirty("carryProgress");

            if (holdCarryTime > 10f)
            {
                isCarrying = true;
                interactingPlayer.WatchedAttributes.SetBool("carryingUnconciousPlayer", true);
                interactingPlayer.WatchedAttributes.MarkPathDirty("carryingUnconciousPlayer");
                entity.WatchedAttributes.SetFloat("carryProgress", 0f);
                entity.WatchedAttributes.SetLong("interactingPlayerId", 0); // Clear ID
                entity.WatchedAttributes.MarkPathDirty("carryProgress");
                entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");
                StartCarryingPlayer(interactingPlayer, targetPlayer);
                holdCarryTime = 0;
                initialPosition = null;
            }
        }

        private void HandleReviveHold(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            reviveCancelCallbackId = sapi.Event.RegisterCallback(_ => CancelReviveHold(interactingPlayer), 2000);

            if (!isHoldingRevive) return;

            var heldItem = interactingPlayer.ActiveHandItemSlot.Itemstack;
            var characterClass = interactingPlayer.WatchedAttributes?.GetAsString("characterClass");
            var config = UnconsciousModSystem.getConfig();

            if (string.IsNullOrEmpty(characterClass) ||
                (config.RequireSmellingSaltsForRevive &&
                 !config.ReviveClassWhitelist.Contains(characterClass) &&
                 (heldItem == null || !heldItem.Collectible.Code.Path.Contains("smellingsalts"))))
            {
                CancelReviveHold(interactingPlayer);
                return;
            }

            if (!IsWithinDistance(interactingPlayer, targetPlayer, 2))
            {
                isCarrying = false;
                sapi.Event.UnregisterGameTickListener(carryTickListenerId);
                CancelReviveHold(interactingPlayer);
                return;
            }

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelReviveHold(interactingPlayer);
                return;
            }

            entity.WatchedAttributes.SetLong("interactingPlayerId", interactingPlayer.EntityId);
            entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");

            float reviveSpeed = config.RevivePerTickDuration;
            if (config.ReviveClassWhitelist.Contains(characterClass))
            {
                reviveSpeed += 0.2f;
            }
            if (heldItem?.Collectible.Code.Path.Contains("smellingsalts-strong") == true)
            {
                reviveSpeed += 0.1f;
            }

            holdReviveTime += reviveSpeed;
            // Update entity attribute for client-side rendering
            entity.WatchedAttributes.SetFloat("reviveProgress", holdReviveTime / 10f);
            entity.WatchedAttributes.MarkPathDirty("reviveProgress");

            if (holdReviveTime > 10f)
            {
                FinishRevive(interactingPlayer, targetPlayer);
                entity.WatchedAttributes.SetFloat("reviveProgress", 0f); // Reset progress
                entity.WatchedAttributes.SetLong("interactingPlayerId", 0); // Clear ID
                entity.WatchedAttributes.MarkPathDirty("reviveProgress");
                entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");
                isHoldingRevive = false;
                holdReviveTime = 0;
                initialPosition = null;
            }
        }

        private bool HasPlayerMoved(EntityPlayer player)
        {
            return initialPosition != null && !player.Pos.AsBlockPos.ToVec3d().Equals(initialPosition);
        }

        private void CancelCarryHold(EntityPlayer interactingPlayer)
        {
            isCarrying = false;
            holdCarryTime = 0;
            initialPosition = null;
            entity.WatchedAttributes.SetFloat("carryProgress", 0f);
            entity.WatchedAttributes.SetLong("interactingPlayerId", 0);
            entity.WatchedAttributes.MarkPathDirty("carryProgress");
            entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");
            SendErrorMessage(Lang.Get("unconscious:reviving-cancel"), interactingPlayer);
        }

        private void CancelReviveHold(EntityPlayer interactingPlayer)
        {
            isHoldingRevive = false;
            holdReviveTime = 0;
            initialPosition = null;
            entity.WatchedAttributes.SetFloat("reviveProgress", 0f);
            entity.WatchedAttributes.SetLong("interactingPlayerId", 0);
            entity.WatchedAttributes.MarkPathDirty("reviveProgress");
            entity.WatchedAttributes.MarkPathDirty("interactingPlayerId");
            SendErrorMessage(Lang.Get("unconscious:reviving-cancel"), interactingPlayer);
        }

        private void FinishRevive(EntityPlayer interactingPlayer, EntityPlayer targetPlayer)
        {
            var config = UnconsciousModSystem.getConfig();
            var heldItem = interactingPlayer.ActiveHandItemSlot.Itemstack;
            float healingPotency = config.MaxHealthPercentAfterRevive;

            SendErrorMessage($"{targetPlayer.Player.PlayerName} {Lang.Get("unconscious:reviving-success")}", interactingPlayer);

            if (config.RequireSmellingSaltsForRevive && heldItem?.Collectible.Code.Path.Contains("smellingsalts") == true)
            {
                if (heldItem.Collectible.Code.Path.Contains("smellingsalts-strong"))
                {
                    healingPotency += 0.30f;
                }
                var activeSlot = interactingPlayer.Player.InventoryManager.ActiveHotbarSlot;
                activeSlot.TakeOut(1);
                activeSlot.MarkDirty();
            }

            UnconsciousModSystem.HandlePlayerPickup(targetPlayer, healingPotency);
        }

        public void RenderProgressCircle(float progress)
        {
            var renderApi = capi.Render;
            long interactingPlayerId = entity.WatchedAttributes.GetLong("interactingPlayerId", 0);
            long localPlayerId = capi.World.Player.Entity.EntityId;
            if (interactingPlayerId == 0 || interactingPlayerId != localPlayerId)
            {
                return; // Only render for the interacting player
            }

            float scaledProgress = progress;
            float outerRadius = 40f;
            float innerRadius = 30f;
            int maxSteps = 32;
            float circleAlpha = 1f;

            Vec4f circleColor;
            float reviveProgress = entity.WatchedAttributes.GetFloat("reviveProgress", 0f);
            if (reviveProgress > 0)
            {
                circleColor = new Vec4f(0.596078431372549f, 0.8431372549019608f, 0.6888888888888889f, circleAlpha); // Green for revive
            }
            else
            {
                circleColor = new Vec4f(1f, 1f, 0f, circleAlpha); // Yellow for pickup
            }

            // Calculate number of segments based on progress
            int numSegments = 1 + (int)Math.Ceiling(maxSteps * scaledProgress);

            // Create mesh for progress circle
            MeshData meshData = new MeshData(numSegments * 2, numSegments * 6, false, false, true, false);
            for (int i = 0; i < numSegments; i++)
            {
                double angle = Math.Min(scaledProgress, (float)i / maxSteps) * 2.0 * Math.PI;
                float sin = (float)Math.Sin(angle);
                float cos = -(float)Math.Cos(angle);
                meshData.AddVertexSkipTex(sin * outerRadius, cos * outerRadius, 0f, -1);
                meshData.AddVertexSkipTex(sin * innerRadius, cos * innerRadius, 0f, -1);
                if (i > 0)
                {
                    meshData.AddIndices(new int[] { i * 2 - 2, i * 2 - 1, i * 2 });
                    meshData.AddIndices(new int[] { i * 2, i * 2 - 1, i * 2 + 1 });
                }
            }

            // Update or create mesh
            try
            {
                if (circleMeshRef != null)
                {
                    renderApi.UpdateMesh(circleMeshRef, meshData);
                }
                else
                {
                    circleMeshRef = renderApi.UploadMesh(meshData);
                }

                // Set up rendering
                var shader = renderApi.CurrentActiveShader;
                shader.Uniform("rgbaIn", circleColor); // Use dynamic color
                shader.Uniform("extraGlow", 0);
                shader.Uniform("applyColor", 0);
                shader.Uniform("tex2d", 0);
                shader.Uniform("noTexture", 1f);
                shader.UniformMatrix("projectionMatrix", renderApi.CurrentProjectionMatrix);

                // Center at cursor or screen center if mouse is grabbed
                int centerX = capi.Input.MouseGrabbed ? renderApi.FrameWidth / 2 : capi.Input.MouseX;
                int centerY = capi.Input.MouseGrabbed ? renderApi.FrameHeight / 2 : capi.Input.MouseY;

                renderApi.GlPushMatrix();
                renderApi.GlTranslate(centerX, centerY, 0f);
                shader.UniformMatrix("modelViewMatrix", renderApi.CurrentModelviewMatrix);
                renderApi.RenderMesh(circleMeshRef);
                renderApi.GlPopMatrix();
            }
            finally
            {
                // Clean up mesh to avoid memory leaks
                if (circleMeshRef != null)
                {
                    renderApi.DeleteMesh(circleMeshRef);
                    circleMeshRef = null;
                }
            }
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);
            if (capi != null)
            {
                if (progressRenderer != null)
                {
                    capi.Event.UnregisterRenderer(progressRenderer, EnumRenderStage.Ortho);
                    progressRenderer.Dispose();
                    progressRenderer = null;
                }
                if (circleMeshRef != null)
                {
                    capi.Render.DeleteMesh(circleMeshRef);
                    circleMeshRef = null;
                }
            }
        }
    }
}