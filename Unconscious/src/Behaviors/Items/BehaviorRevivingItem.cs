using System;
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

        //base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);

        ICoreAPI coreAPI = api;
        applicationSound?.Stop();
        api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);
        handling = EnumHandling.Handled;

        if (secondsUsed < GetPickupApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        Entity targetEntity = GetTargetEntityToRevive(slot, byEntity, entitySel);
        BehaviorRevivePlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorRevivePlayer>();

        if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("unconscious"))
        {
            DamageSource damageSource = new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal,
                DamageTier = 0,
                Duration = TimeSpan.FromSeconds(Config.EffectDurationSec),
                TicksPerDuration = Config.Ticks
            };
            behaviorRevivePlayer.FinishRevive(byEntity as EntityPlayer, targetEntity as EntityPlayer, damageSource, Config.Health);
            handling = EnumHandling.PreventDefault;
        }
        else
        {
            DamageSource damageSource = new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal,
                DamageTier = 0,
                Duration = TimeSpan.FromSeconds(Config.EffectDurationSec),
                TicksPerDuration = Config.Ticks
            };
            targetEntity.ReceiveDamage(damageSource, Config.Health);
        }

        if (Config.AppliedSound != null)
        {
            byEntity.World.PlaySoundAt(Config.AppliedSound, byEntity, null, false, Config.SoundRange, 1f);
        }
        slot.TakeOut(1);
        slot.MarkDirty();


    }

    protected virtual float GetPickupApplicationTime(Entity byEntity)
    {
        float healingEffectiveness = 0f;
        if (Config.AffectedByArmor)
        {
            healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
            healingEffectiveness = Math.Clamp(healingEffectiveness, 0f, 2f) - 1f;
        }
        if (healingEffectiveness < 0f)
        {
            return Config.ApplicationTimeSec + (Config.ApplicationTimeSec - Config.MaxApplicationTimeSec) * healingEffectiveness;
        }
        if (healingEffectiveness > 0f)
        {
            return Config.ApplicationTimeSec * (1f - healingEffectiveness);
        }
        return Config.ApplicationTimeSec;
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
