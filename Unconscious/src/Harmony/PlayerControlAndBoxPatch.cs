using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Unconscious.HarmonyPatches
{
    public static class PlayerControlAndBoxPatch
    {
        public static void Prefix(EntityPlayer __instance)
        {
            if (__instance == null || __instance.WatchedAttributes == null) return;

            if (__instance.WatchedAttributes.GetBool("unconscious", false))
            {
                var controls = __instance.Controls;
                if (controls != null)
                {
                    controls.Forward = false;
                    controls.Backward = false;
                    controls.Left = false;
                    controls.Right = false;
                    controls.Jump = false;
                    controls.Sprint = false;
                    controls.Sneak = false;
                }

                var animManager = __instance.AnimManager;
                if (animManager != null)
                {
                    animManager.StopAnimation("walk");
                    animManager.StopAnimation("run");
                    animManager.StopAnimation("sprint");
                    animManager.StopAnimation("sneakwalk");

                    if (!animManager.IsAnimationActive("sleep"))
                    {
                        animManager.StartAnimation("sleep");
                    }
                }
            }
        }

        public static void Postfix(EntityPlayer __instance)
        {
            if (__instance == null || __instance.WatchedAttributes == null) return;

            if (__instance.WatchedAttributes.GetBool("unconscious", false))
            {
                if (__instance.SelectionBox != null)
                {
                    __instance.SelectionBox.Set(-0.5f, 0f, -0.5f, 0.5f, 0.6f, 0.5f);
                    __instance.CollisionBox?.Set(-1.2f, 0f, -1.2f, 1.2f, 0.4f, 1.2f);

                    float yaw = __instance.Pos.Yaw;
                    double offsetDist = 0.85;

                    double offsetX = Math.Sin(yaw) * offsetDist;
                    double offsetZ = Math.Cos(yaw) * offsetDist;

                    __instance.LocalEyePos.X = offsetX;
                    __instance.LocalEyePos.Y = 0.25;
                    __instance.LocalEyePos.Z = offsetZ;
                }
            }
            else
            {
                if (__instance.SelectionBox != null && __instance.SelectionBox.Y2 < 1.0f)
                {
                    __instance.SelectionBox.Set(-0.3f, 0f, -0.3f, 0.3f, 1.85f, 0.3f);
                    __instance.CollisionBox?.Set(-0.3f, 0f, -0.3f, 0.3f, 1.85f, 0.3f);

                    __instance.LocalEyePos.X = 0;
                    __instance.LocalEyePos.Y = 1.7; 
                    __instance.LocalEyePos.Z = 0;
                }
            }
        }
    }
}