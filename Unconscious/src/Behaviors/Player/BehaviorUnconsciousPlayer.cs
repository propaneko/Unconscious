using System;
using System.Text;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Unconscious.src.Behaviors.Player
{
    public class BehaviorUnconsciousPlayer : EntityBehavior
    {
        private readonly Cuboidf originalCollisionBox;
        private readonly Cuboidf originalSelectionBox;
        private EntityPlayer entityPlayer;
        private readonly ICoreAPI api; // Added for client-side rendering
        private bool wasUnconscious; // Tracks previous unconscious state

        protected IProgressBar progressBarRender;

        public BehaviorUnconsciousPlayer(Entity entity) : base(entity)
        {
            api = entity.Api;
            originalCollisionBox = entity.CollisionBox.Clone();
            originalSelectionBox = entity.SelectionBox.Clone();
            entityPlayer = entity as EntityPlayer;
        }

        public override string PropertyName() => "UnconsciousBehavior";

        public override void GetInfoText(StringBuilder infotext)
        {
            if (entityPlayer.IsUnconscious())
            {
                infotext.AppendLine(Lang.Get("Mortally wounded, unconscious for some time..."));
                return;

            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            if (entity.World.Side == EnumAppSide.Server && api.Side == EnumAppSide.Server)
            {
                ICoreServerAPI sapi = api as ICoreServerAPI;

                bool isUnconscious = entity.WatchedAttributes.GetBool("unconscious");
                if (isUnconscious != wasUnconscious)
                {
                    if (isUnconscious)
                    {
                        SetUnconsciousHitbox();
                    }
                    wasUnconscious = isUnconscious;
                }
            }
        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            bool isUnconscious = entity.WatchedAttributes.GetBool("unconscious");
            if (isUnconscious != wasUnconscious)
            {
                if (isUnconscious) SetUnconsciousHitbox();
                else RestoreDefaultHitbox();
                wasUnconscious = isUnconscious;
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

        public void SendErrorMessage(string message, EntityPlayer interactingPlayer)
        {
            var serverPlayer = interactingPlayer.Player as IServerPlayer;
            ICoreServerAPI sapi = api as ICoreServerAPI;

            sapi.SendIngameError(serverPlayer, "ReviveCycle", message, Array.Empty<object>());
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            base.OnEntityDespawn(despawn);
        }
    }
}