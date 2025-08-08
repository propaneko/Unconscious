using System;
using System.Runtime.CompilerServices;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Unconscious.src.Renderers
{
    public class UnconsciousIndicatorRenderer : IRenderer, IDisposable
    {

        private ICoreClientAPI capi;
        private Entity entity;
        public LoadedTexture texture;
        public string DisplayText = Lang.Get("unconscious:indicator");
        public int RenderDistance = 32;
        public double RenderOrder
        {
            get
            {
                return -0.1;
            }
        }

        public int RenderRange
        {
            get
            {
                return 999;
            }
        }

        public UnconsciousIndicatorRenderer(ICoreClientAPI capi, Entity entity)
        {
            this.capi = capi;
            this.entity = entity;
        }

        public LoadedTexture GenerateTypingTexture(ICoreClientAPI capi, Entity entity)
        {
            ReviveBehavior behavior = entity.GetBehavior<ReviveBehavior>();
            if (behavior == null || !entity.WatchedAttributes.GetAsBool("unconscious"))
            {
                texture?.Dispose();
                texture = null;
                return null;
            }

            if (texture == null)
            {
                texture = capi.Gui.TextTexture.GenUnscaledTextTexture(
                    DisplayText,
                    CairoFont.WhiteSmallText().WithColor(color),
                    background
                );
            }

            return texture;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            EntityPlayer entityPlayer = entity as EntityPlayer;
            if (entityPlayer == null || !entity.Alive)
            {
                return;
            }

            IRenderAPI render = capi.Render;
            EntityPlayer entityPlayer2 = capi.World.Player.Entity;
            Vec3d vec3d;
            if (capi.World.Player.Entity.EntityId == entity.EntityId)
            {
                if (render.CameraType == null)
                {
                    return;
                }
                vec3d = new Vec3d(entityPlayer2.CameraPos.X + entityPlayer2.LocalEyePos.X, entityPlayer2.CameraPos.Y + 0.3 + entityPlayer2.LocalEyePos.Y, entityPlayer2.CameraPos.Z + entityPlayer2.LocalEyePos.Z);
            }
            else
            {
                EntityAgent entityAgent = entity as EntityAgent;
                IMountableSeat mountableSeat = entityAgent != null ? entityAgent.MountedOn : null;
                IMountableSeat mountedOn = entityPlayer2.MountedOn;
                if ((mountableSeat != null ? mountableSeat.MountSupplier : null) != null && mountableSeat.MountSupplier == (mountedOn != null ? mountedOn.MountSupplier : null))
                {
                    Vec3f vec3f = new Vec3f((float)mountableSeat.SeatPosition.X, (float)mountableSeat.SeatPosition.Y, (float)mountableSeat.SeatPosition.Z);
                    vec3d = new Vec3d(entityPlayer2.CameraPos.X + entityPlayer2.LocalEyePos.X, entityPlayer2.CameraPos.Y + 0.3 + entityPlayer2.LocalEyePos.Y, entityPlayer2.CameraPos.Z + entityPlayer2.LocalEyePos.Z);
                    vec3d.Add(vec3f);
                }
                else
                {
                    vec3d = new Vec3d(entity.Pos.X, entity.Pos.Y + entity.SelectionBox.Y2 + -0.1, entity.Pos.Z);
                }
            }
            if ((double)entityPlayer2.Pos.SquareDistanceTo(entity.Pos) > RenderDistance)
            {
                return;
            }
            double num = (double)(entity.SelectionBox.X2 - entity.OriginSelectionBox.X2);
            double num2 = (double)(entity.SelectionBox.Z2 - entity.OriginSelectionBox.Z2);
            vec3d.Add(num, 0.0, num2);
            Vec3d vec3d2 = MatrixToolsd.Project(vec3d, render.PerspectiveProjectionMat, render.PerspectiveViewMat, render.FrameWidth, render.FrameHeight);
            if (vec3d2.Z < 0.0)
            {
                return;
            }
            LoadedTexture loadedTexture = GenerateTypingTexture(capi, entityPlayer);
            if (loadedTexture != null)
            {
                float val = 4f / Math.Max(1f, (float)vec3d2.Z);
                float num3 = Math.Min(1f, val);
                float num4 = (float)vec3d2.X - num3 * loadedTexture.Width / 2f;
                float num5 = render.FrameHeight - (float)vec3d2.Y - loadedTexture.Height / 2f * Math.Max(0f, num3);
                render.Render2DTexture(loadedTexture.TextureId, num4, num5, loadedTexture.Width, loadedTexture.Height, 50f, null);
            }
        }

        public void Dispose()
        {
        }

        public double[] color = ColorUtil.WhiteArgbDouble;

        public TextBackground background = new TextBackground
        {
            FillColor = GuiStyle.DialogLightBgColor,
            Padding = 2,
            Radius = GuiStyle.ElementBGRadius,
            Shade = true,
            BorderColor = GuiStyle.DialogBorderColor,
            BorderWidth = 3.0
        };
    }
}
