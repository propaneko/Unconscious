using System;
using System.Text;
using Unconscious.Player;
using Unconscious.src.Renderers;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Unconscious.Behaviors.Player
{
    public class BehaviorUnconsciousPlayer : EntityBehavior
    {
        private Cuboidf originalCollisionBox;
        private Cuboidf originalSelectionBox;
        private double originalEyeHeight;
        private EntityPlayer entityPlayer;
        private UnconsciousIndicatorRenderer indicatorRenderer;

        public BehaviorUnconsciousPlayer(Entity entity) : base(entity)
        {
            entityPlayer = entity as EntityPlayer;
        }

        public override string PropertyName() => "UnconsciousBehavior";

        public override void GetInfoText(StringBuilder infotext)
        {
            if (entityPlayer != null && entityPlayer.IsUnconscious())
            {
                infotext.AppendLine(Lang.Get("Mortally wounded, unconscious for some time..."));
            }
        }

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);

            // Store the defaults so we can restore them perfectly later
            originalCollisionBox = entity.CollisionBox.Clone();
            originalSelectionBox = entity.SelectionBox.Clone();
            originalEyeHeight = entity.LocalEyePos.Y;

            entity.WatchedAttributes.RegisterModifiedListener("unconscious", OnUnconsciousStateChanged);
            OnUnconsciousStateChanged();
            RegisterIndicator();
        }

        private void OnUnconsciousStateChanged()
        {
            bool isUnconscious = entity.WatchedAttributes.GetBool("unconscious");
            if (isUnconscious)
            {
                SetUnconsciousHitbox();
            }
            else
            {
                RestoreDefaultHitbox();
            }
        }

        private void SetUnconsciousHitbox()
        {
            entity.CollisionBox.Set(
                x1: -0.55f, y1: 0.0f, z1: -0.65f,
                x2: 0.45f, y2: 0.3f, z2: 0.15f
            );

            entity.SelectionBox.Set(
                x1: -0.5f, y1: 0.0f, z1: -0.75f,
                x2: 0.5f, y2: 0.3f, z2: 0.25f
            );

            entity.LocalEyePos.Y = 0.3f;
        }

        private void RestoreDefaultHitbox()
        {
            if (originalCollisionBox != null && originalSelectionBox != null)
            {
                entity.CollisionBox.Set(originalCollisionBox);
                entity.SelectionBox.Set(originalSelectionBox);
                entity.LocalEyePos.Y = originalEyeHeight;
            }
        }

        private void RegisterIndicator()
        {
            if (indicatorRenderer != null || entity.Api is not ICoreClientAPI capi) return;

            indicatorRenderer = new UnconsciousIndicatorRenderer(capi, entity);
            capi.Event.RegisterRenderer(indicatorRenderer, EnumRenderStage.Ortho);
        }

        private void UnregisterIndicator()
        {
            if (indicatorRenderer == null) return;

            if (entity.Api is ICoreClientAPI capi)
            {
                capi.Event.UnregisterRenderer(indicatorRenderer, EnumRenderStage.Ortho);
            }

            indicatorRenderer.Dispose();
            indicatorRenderer = null;
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            UnregisterIndicator();
            base.OnEntityDespawn(despawn);
        }

        public void SendErrorMessage(string message, EntityPlayer interactingPlayer)
        {
            if (entity.Api is ICoreServerAPI sapi && interactingPlayer.Player is IServerPlayer serverPlayer)
            {
                sapi.SendIngameError(serverPlayer, "ReviveCycle", message, Array.Empty<object>());
            }
        }
    }
}