
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Unconscious.src.Items;

public class ItemSmellingSalts : Item, ICanHealCreature
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        byEntity.World.RegisterCallback(delegate
        {
            if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
            {
                IPlayer dualCallByPlayer = null;
                if (byEntity is EntityPlayer)
                {
                    dualCallByPlayer = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                }

                byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/poultice"), byEntity, dualCallByPlayer);
            }
        }, 200);
        JsonObject attributes = slot.Itemstack.Collectible.Attributes;
        if (attributes != null && attributes["health"].Exists)
        {
            handling = EnumHandHandling.PreventDefault;
        }
        else
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        return secondsUsed < 0.7f + ((byEntity.World.Side == EnumAppSide.Client) ? 0.3f : 0f);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed > 0.7f && byEntity.World.Side == EnumAppSide.Server)
        {
            float num = slot.Itemstack.Collectible.Attributes["health"].AsFloat();
            Entity entity = byEntity;
            EntityBehaviorHealth entityBehaviorHealth = entitySel?.Entity?.GetBehavior<EntityBehaviorHealth>();
            if (byEntity.Controls.ShiftKey && !byEntity.Controls.Forward && !byEntity.Controls.Backward && !byEntity.Controls.Left && !byEntity.Controls.Right && entityBehaviorHealth != null && entityBehaviorHealth.IsHealable(byEntity))
            {
                entity = entitySel.Entity;
            }

            if (num > 0f)
            {
                float blended = entity.Stats.GetBlended("healingeffectivness");
                num *= Math.Max(0f, blended);
            }

            entity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = ((num > 0f) ? EnumDamageType.Heal : EnumDamageType.Poison)
            }, Math.Abs(num));
            slot.TakeOut(1);
            slot.MarkDirty();
        }
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        JsonObject attributes = inSlot.Itemstack.Collectible.Attributes;
        if (attributes != null && attributes["health"].Exists)
        {
            float num = attributes["health"].AsFloat();
            dsc.AppendLine(Lang.Get("When used: +{0} hp", num));
        }
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return base.GetHeldInteractionHelp(inSlot);
        //return new WorldInteraction[1]
        //{
        //    new WorldInteraction
        //    {
        //        ActionLangCode = "heldhelp-heal",
        //        MouseButton = EnumMouseButton.Right
        //    }
        //}.Append(base.GetHeldInteractionHelp(inSlot));
    }

    public bool CanHeal(Entity entity)
    {
        int num = entity.Properties.Attributes?["minGenerationToAllowHealing"].AsInt(-1) ?? (-1);
        if (num >= 0)
        {
            return num >= entity.WatchedAttributes.GetInt("generation");
        }

        return false;
    }

    public WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
        return new WorldInteraction[1]
        {
            new WorldInteraction
            {
                ActionLangCode = "heldhelp-heal",
                HotKeyCode = "ctrl",
                MouseButton = EnumMouseButton.Right
            }
        };
    }
}