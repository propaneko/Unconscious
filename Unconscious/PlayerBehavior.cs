using Microsoft.Win32.SafeHandles;
using NoticeBoard.Packets;
using System;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;

namespace Unconscious
{
    public class PlayerBehavior : EntityBehavior
    {
        private bool isHolding = false; // Track if the right-click is being held
        private bool startedInteraction = false; // Track if the right-click is being held
        private Vec3d initialPosition;  // Tracks the player's initial position when holding starts
        private Entity targetEntity;    // The target entity the player must look at

        private float holdTime = 0;    // Track the duration of the hold
        private ICoreServerAPI sapi;
        public PlayerBehavior(Entity entity) : base(entity)
        {
            sapi = entity.Api as ICoreServerAPI;
            this.targetEntity = entity; // Set the target entity
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

                if (byEntity is EntityAgent entityPlayer && mode == EnumInteractMode.Interact && byEntity.Controls.Sneak && byEntity.World.Side == EnumAppSide.Server)
                {
                    //UnconsciousModSystem.getSAPI().Logger.Event("test");
                    if (!targetPlayer.IsUnconscious())
                    {
                        return;
                    }

                    isHolding = true;
                    if (initialPosition == null)
                    {
                        initialPosition = interactingPlayer.Pos.AsBlockPos.ToVec3d();
                    }

                    HandleHold(byEntity as EntityPlayer);

                }
            }
        }

        private void HandleHold(EntityPlayer interactingPlayer)
        {
            if (!isHolding) return; // Stop if the hold has been released

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelHold(interactingPlayer);
                return;
            }

            holdTime += 0.5f; // Increment hold time (50ms = 0.05s)

            // Check if the hold time has reached 5 seconds
            if (holdTime > 10f)
            {
                RunLongPressAction(interactingPlayer);

                // Stop tracking once the action is executed
                isHolding = false;
                holdTime = 0;
                startedInteraction = false;
                initialPosition = null;
            }
            else
            {
                string text = "SpeedCycle";
                EntityPlayer entityPlayer = this.entity as EntityPlayer;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
                defaultInterpolatedStringHandler.AppendLiteral("Revive hold ");
                defaultInterpolatedStringHandler.AppendFormatted<float>((float)holdTime * 10f);
                defaultInterpolatedStringHandler.AppendLiteral("%");

                sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
            }
        }

        private bool HasPlayerMoved(EntityPlayer player)
        {
            Vec3d currentPosition = player.Pos.AsBlockPos.ToVec3d();
            return !currentPosition.Equals(initialPosition);
        }

        private void CancelHold(EntityPlayer interactingPlayer)
        {
            isHolding = false;
            holdTime = 0;
            startedInteraction = false;
            initialPosition = null;

            // Notify the player that the action was canceled
            string text = "SpeedCycle";
            EntityPlayer entityPlayer = this.entity as EntityPlayer;
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
            defaultInterpolatedStringHandler.AppendLiteral("Canceled revive!");
            sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
        }

        private void RunLongPressAction(EntityPlayer interactingPlayer)
        {
            var player = this.entity as EntityPlayer;
            string text = "SpeedCycle";
            EntityPlayer entityPlayer = this.entity as EntityPlayer;
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
            defaultInterpolatedStringHandler.AppendLiteral($"{player.Player.PlayerName} picked up!");
            sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
            sapi.Logger.Debug("player healed!");

            player.Revive();

            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            player.AnimManager.StopAnimation("sleep");

            var health = player.WatchedAttributes.GetTreeAttribute("health");
            var maxHealth = health.GetFloat("maxhealth");
            health.SetFloat("currenthealth", maxHealth * 0.25f);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            ShowUnconciousScreen responsePacket = new()
            {
                shouldShow = false,
            };

            UnconsciousModSystem.getSAPI().Network.GetChannel("unconscious").SendPacket(responsePacket, serverPlayer);
        }

    }
}
