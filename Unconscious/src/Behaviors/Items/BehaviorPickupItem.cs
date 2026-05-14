using System;
using System.Text;
using Newtonsoft.Json;
using Unconscious.src.Behaviors.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Unconscious.src.Behaviors.Items
{
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class CollectibleBehaviorPickupItem : CollectibleBehaviorHealingItem, ICanHealCreature
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

        protected new float secondsUsedToCancel;

        public CollectibleBehaviorPickupItem(CollectibleObject collectable) : base(collectable)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);

            if (properties.Exists)
            {
                JsonUtil.Populate<CollectibleBehaviorHealingItem>(properties.Token, this);
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

            if (entitySel == null)
            {
                return;
            }

            Entity targetEntity = GetTargetToPickupEntity(slot, byEntity, entitySel);
            if (targetEntity != null)
            {
                BehaviorPickupPlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorPickupPlayer>();
                if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("carryingUnconciousPlayer"))
                {
                    behaviorRevivePlayer.StopCarryingPlayer(byEntity as EntityPlayer, targetEntity as EntityPlayer, Lang.Get("unconscious:pickup-error-drop"));
                }
            }

            if  (entitySel?.Entity == null && entitySel?.Entity is not EntityPlayer && !entitySel.Entity.WatchedAttributes.GetAsBool("unconsciosu"))
            {
                return;
            }

            if (CancelApplication(byEntity))
            {
                return;
            }
            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventSubsequent;
            ICoreAPI coreAPI = api;
            if (coreAPI != null)
            {
                coreAPI.World.RegisterCallback(delegate (float _)
                {
                    ILoadedSound loadedSound = applicationSound;
                    if (loadedSound == null)
                    {
                        return;
                    }
                    loadedSound.Stop();
                }, (int)GetApplicationTime(byEntity) * 1000);
            }
            ICoreAPI coreAPI2 = api;
            if (coreAPI2 != null && coreAPI2.Side == EnumAppSide.Client)
            {
                ModSystemProgressBar progressBarSystem = api.ModLoader.GetModSystem<ModSystemProgressBar>(true);
                progressBarSystem.RemoveProgressbar(progressBarRender);
                progressBarRender = progressBarSystem.AddProgressbar();
            }
            secondsUsedToCancel = 0f;

            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            if (entitySel == null && entitySel?.Entity == null && entitySel?.Entity is not EntityPlayer)
            {
                ICoreAPI coreAPI = api;
                if (coreAPI != null)
                {
                    ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
                    if (modSystem != null)
                    {
                        modSystem.RemoveProgressbar(progressBarRender);
                    }
                }
                return false;
            }

            if (!CancelApplication(byEntity))
            {
                secondsUsedToCancel = 0f;
            }
            else if (secondsUsedToCancel == 0f)
            {
                secondsUsedToCancel = secondsUsed;
            }
            if (CancelApplication(byEntity) && (double)(secondsUsed - secondsUsedToCancel) > 0.5)
            {
                return false;
            }
            ILoadedSound loadedSound = applicationSound;
            if (loadedSound != null && loadedSound.HasStopped)
            {
                applicationSound.Start();
            }
            ILoadedSound loadedSound2 = applicationSound;
            if (loadedSound2 != null)
            {
                loadedSound2.SetPosition((float)byEntity.Pos.X, (float)byEntity.Pos.InternalY, (float)byEntity.Pos.Z);
            }
            handling = (EnumHandling)1;
            float progress = secondsUsed / (GetApplicationTime(byEntity) + (byEntity.World.Side == EnumAppSide.Client ? 0.3f : 0f));
            if (progressBarRender != null)
            {
                progressBarRender.Progress = progress;
            }
            return progress < 1f;
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
        {
            ILoadedSound loadedSound = applicationSound;
            if (loadedSound != null)
            {
                loadedSound.Stop();
            }
            ICoreAPI coreAPI = api;
            if (coreAPI != null)
            {
                ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
                if (modSystem != null)
                {
                    modSystem.RemoveProgressbar(progressBarRender);
                }
            }
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
        {
            applicationSound?.Stop();
            api?.ModLoader.GetModSystem<ModSystemProgressBar>()?.RemoveProgressbar(progressBarRender);

            handling = EnumHandling.Handled;
            ICoreAPI coreAPI = api;

            if (secondsUsed < GetApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
            {
                return;
            }

            Entity targetEntity = GetTargetToPickupEntity(slot, byEntity, entitySel);
            if (targetEntity != null)
            {
                BehaviorPickupPlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorPickupPlayer>();
                if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("unconscious") && UnconsciousModSystem.getConfig().EnableCarryMechanic)
                {
                    behaviorRevivePlayer.StartCarryingPlayer(byEntity as EntityPlayer, targetEntity as EntityPlayer);
                    handling = EnumHandling.PreventDefault;
                }
            }
           
         
            if (AppliedSound != null)
            {
                byEntity.World.PlaySoundAt(AppliedSound, byEntity, null, false, SoundRange, 1f);
            }
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            string text = "unconscious:pickup-item-info";
            dsc.AppendLine(Lang.Get(text));
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
        {
            return [];
        }

        public virtual WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return [];
        }

        protected virtual float GetPickupApplicationTime(Entity byEntity)
        {
            float healingEffectiveness = 0f;
            if (AffectedByArmor)
            {
                healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
                healingEffectiveness = Math.Clamp(healingEffectiveness, 0f, 2f) - 1f;
            }
            if (healingEffectiveness < 0f)
            {
                return ApplicationTimeSec + (ApplicationTimeSec - MaxApplicationTimeSec) * healingEffectiveness;
            }
            if (healingEffectiveness > 0f)
            {
                return ApplicationTimeSec * (1f - healingEffectiveness);
            }
            return ApplicationTimeSec;
        }

        protected virtual Entity GetTargetToPickupEntity(ItemSlot slot, EntityAgent byEntity, EntitySelection entitySelection)
        {
            Entity targetEntity = null;
            Entity? selectedEntity = entitySelection?.Entity;
            if (selectedEntity == null)
            {
                return targetEntity;
            }
            //EntityBehaviorHealth? healthBehavior = selectedEntity.GetBehavior<EntityBehaviorHealth>();
            if (
                byEntity.Controls.CtrlKey && 
                !byEntity.Controls.Forward && 
                !byEntity.Controls.Backward && 
                !byEntity.Controls.Left &&
                !byEntity.Controls.Right
                )
            {
                targetEntity = selectedEntity;
            }
            return targetEntity;
        }
    }
}
