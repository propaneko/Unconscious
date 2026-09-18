using System;
using Newtonsoft.Json;
using Unconscious.Behaviors.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Unconscious.Behaviors.Items;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class CollectibleBehaviorRevivingItem : CollectibleBehaviorHealingItem, ICanHealCreature
{

    [JsonProperty]
    public float Health { get; set; } = 1;
    [JsonProperty]
    public float ApplicationTimeSec { get; set; } = 2;
    [JsonProperty]
    public float MaxApplicationTimeSec { get; set; } = 10;
    [JsonProperty]
    public int Ticks { get; set; } = 10;
    [JsonProperty]
    public float EffectDurationSec { get; set; } = 10;
    [JsonProperty]
    public bool CancelInAir { get; set; } = true;
    [JsonProperty]
    public bool CancelWhileSwimming { get; set; } = false;
    [JsonProperty]
    public AssetLocation? Sound { get; set; } = new AssetLocation("game:sounds/player/poultice");
    [JsonProperty]
    public AssetLocation? AppliedSound { get; set; } = new AssetLocation("game:sounds/player/poultice-applied");
    [JsonProperty]
    public float SoundRange { get; set; } = 8;
    [JsonProperty]
    public bool CanRevive { get; set; } = true;
    [JsonProperty]
    public bool AffectedByArmor { get; set; } = true;
    [JsonProperty]
    public float DelayToCancelSec { get; set; } = 0.5f;

    protected new IProgressBar progressBarRender;

    protected new ILoadedSound applicationSound;

    protected new ICoreAPI api;

    protected new float secondsUsedToCancel = 0;

    public CollectibleBehaviorRevivingItem(CollectibleObject collectable) : base(collectable) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        if (properties.Exists)
        {
            JsonUtil.Populate<CollectibleBehaviorRevivingItem>(properties.Token, this);
        }
    }

    public override void OnLoaded(ICoreAPI api)
    {
        if (Sound != null)
        {
            applicationSound = (api as ICoreClientAPI)?.World.LoadSound(new SoundParams() { DisposeOnFinish = false, Location = Sound, ShouldLoop = true, Range = SoundRange });
        }

        this.api = api;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        if (CancelApplication(byEntity))
        {
            return;
        }

        handHandling = EnumHandHandling.PreventDefault;
        handling = EnumHandling.PreventSubsequent;

        api?.World.RegisterCallback(_ => applicationSound?.Stop(), (int)GetApplicationTime(byEntity) * 1000);

        if (api?.Side == EnumAppSide.Client)
        {
            ModSystemProgressBar progressBarSystem = api.ModLoader.GetModSystem<ModSystemProgressBar>();
            progressBarSystem.RemoveProgressbar(progressBarRender);
            progressBarRender = progressBarSystem.AddProgressbar();
        }

        secondsUsedToCancel = 0;
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (!CancelApplication(byEntity))
        {
            secondsUsedToCancel = 0;
        }
        else
        {
            if (secondsUsedToCancel == 0) secondsUsedToCancel = secondsUsed;
        }

        if (CancelApplication(byEntity) && secondsUsed - secondsUsedToCancel > 0.5)
        {
            return false;
        }

        if (applicationSound?.HasStopped == true)
        {
            applicationSound.Start();
        }

        applicationSound?.SetPosition((float)byEntity.Pos.X, (float)byEntity.Pos.InternalY, (float)byEntity.Pos.Z);

        handling = EnumHandling.Handled;

        float progress = secondsUsed / (GetApplicationTime(byEntity));
        if (progressBarRender != null)
        {
            progressBarRender.Progress = progress;
        }
        return progress < 1;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {

        applicationSound?.Stop();
        api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);

        handling = EnumHandling.Handled;

        if (secondsUsed < GetApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        Entity targetEntity = GetTargetEntityToRevive(slot, byEntity, entitySel);
        BehaviorRevivePlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorRevivePlayer>();

        if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("unconscious"))
        {
            DamageSource damageSource = new()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal,
                DamageTier = 0,
                Duration = TimeSpan.FromSeconds(EffectDurationSec),
                TicksPerDuration = Ticks
            };

            behaviorRevivePlayer.FinishRevive(byEntity as EntityPlayer, targetEntity as EntityPlayer, Health);
            return;
        } else
        {
            DamageSource damageSource = new()
            {
                Source = EnumDamageSource.Internal,
                Type = EnumDamageType.Heal,
                DamageTier = 0,
                Duration = TimeSpan.FromSeconds(EffectDurationSec),
                TicksPerDuration = Ticks
            };

            targetEntity.ReceiveDamage(damageSource, Health);
        }

        if (AppliedSound != null)
        {
            byEntity.World.PlaySoundAt(AppliedSound, byEntity, null, false, SoundRange);
        }

        slot.TakeOut(1);
        slot.MarkDirty();
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

    protected override bool CancelApplication(Entity entity)
    {
        return !entity.OnGround && !entity.Swimming && CancelInAir || entity.Swimming && CancelWhileSwimming;
    }
}
