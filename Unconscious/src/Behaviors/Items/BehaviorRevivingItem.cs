using Unconscious.src.Behaviors.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Unconscious.src.Behaviors.Items;

public class BehaviorRevivingItem : BehaviorHealingItem, ICanHealCreature
{
    protected new IProgressBar progressBarRender;

    protected new ILoadedSound applicationSound;

    protected new ICoreAPI api;

    public BehaviorRevivingItem(CollectibleObject collectable) : base(collectable)
    {
    }
    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        Config = properties.AsObject<HealOverTimeConfig>(null);
    }

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {

        Entity targetEntity = GetTargetEntityToRevive(slot, byEntity, entitySel);
        BehaviorRevivePlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorRevivePlayer>();

        if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("unconscious"))
        {
            behaviorRevivePlayer.FinishRevive(byEntity as EntityPlayer, targetEntity as EntityPlayer, Config.Health);
            return;
        }

        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);

    }

    protected virtual Entity GetTargetEntityToRevive(ItemSlot slot, EntityAgent byEntity, EntitySelection entitySelection)
    {
        Entity targetEntity = byEntity;
        Entity selectedEntity = entitySelection != null ? entitySelection.Entity : null;
        if (selectedEntity == null)
        {
            return targetEntity;
        }
        EntityBehaviorHealth selectedEntityHealthBehavior = selectedEntity.GetBehavior<EntityBehaviorHealth>();
        if (byEntity.Controls.ShiftKey && !byEntity.Controls.Forward && !byEntity.Controls.Backward && !byEntity.Controls.Left && !byEntity.Controls.Right && selectedEntityHealthBehavior != null && selectedEntityHealthBehavior.IsHealable(byEntity, slot))
        {
            targetEntity = selectedEntity;
        }
        return targetEntity;
    }

    //public override WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    //{
    //    return new WorldInteraction[]
    //    {
    //            new WorldInteraction
    //            {
    //                ActionLangCode = "heldhelp-heal",
    //                HotKeyCode = "shift",
    //                MouseButton = EnumMouseButton.Right
    //            }
    //    };
    //}

    //public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    //{
    //    return new WorldInteraction[]
    //    {
    //            new WorldInteraction
    //            {
    //                ActionLangCode = "game:heldhelp-heal",
    //                MouseButton = EnumMouseButton.Right
    //            }
    //    };
    //}

    protected override bool CancelApplication(Entity entity)
    {
        return !entity.OnGround && !entity.Swimming && Config.CancelInAir || entity.Swimming && Config.CancelWhileSwimming;
    }
}
