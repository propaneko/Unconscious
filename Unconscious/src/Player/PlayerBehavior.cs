using System;
using System.Runtime.CompilerServices;
using Unconscious.src.Packets;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Unconscious.src.Player
{
    public class PlayerBehavior : EntityBehavior
    {
        private bool isHolding = false; // Track if the right-click is being held
        private Vec3d initialPosition;  // Tracks the player's initial position when holding starts

        private float holdTime = 0;    // Track the duration of the hold
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

                if (byEntity is EntityAgent entityPlayer && mode == EnumInteractMode.Interact && byEntity.Controls.Sneak && byEntity.World.Side == EnumAppSide.Server)
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

                    HandleHold(byEntity as EntityPlayer);

                }
            }
        }

        private void HandleHold(EntityPlayer interactingPlayer)
        {
            if (!isHolding) return;

            if (HasPlayerMoved(interactingPlayer))
            {
                CancelHold(interactingPlayer);
                return;
            }

            holdTime += UnconsciousModSystem.getConfig().RevivePerTickDuration; ; // Increment hold time (50ms = 0.05s)

            if (holdTime > 10f)
            {
                RunLongPressAction(interactingPlayer);

                isHolding = false;
                holdTime = 0;
                initialPosition = null;
            }
            else
            {
                string text = "SpeedCycle";
                EntityPlayer entityPlayer = entity as EntityPlayer;
                DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
                defaultInterpolatedStringHandler.AppendLiteral("Pickin up... ");
                defaultInterpolatedStringHandler.AppendFormatted(Math.Truncate(holdTime * 10f));
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
            initialPosition = null;

            string text = "ReviveCycle";
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
            defaultInterpolatedStringHandler.AppendLiteral("Canceled pickin up!");
            sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());
        }

        private void RunLongPressAction(EntityPlayer interactingPlayer)
        {
            var player = entity as EntityPlayer;
            string text = "ReviveCycle";
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(19, 1);
            defaultInterpolatedStringHandler.AppendLiteral($"{player.Player.PlayerName} picked up!");
            sapi.SendIngameError(interactingPlayer.Player as IServerPlayer, text, defaultInterpolatedStringHandler.ToStringAndClear(), Array.Empty<object>());

            player.Revive();
            player.WatchedAttributes.SetBool("unconscious", false);
            player.WatchedAttributes.MarkPathDirty("unconscious");
            player.AnimManager.StopAnimation("sleep");

            var health = player.WatchedAttributes.GetTreeAttribute("health");
            var maxHealth = health.GetFloat("maxhealth");
            health.SetFloat("currenthealth", maxHealth * UnconsciousModSystem.getConfig().MaxHealthPercentAfterRevive);

            IServerPlayer serverPlayer = sapi.World.PlayerByUid(player.PlayerUID) as IServerPlayer;

            ShowUnconciousScreen responsePacket = new()
            {
                shouldShow = false,
                unconsciousTime = 0,
            };

            UnconsciousModSystem.getSAPI().Network.GetChannel("unconscious").SendPacket(responsePacket, serverPlayer);
        }

    }
}
