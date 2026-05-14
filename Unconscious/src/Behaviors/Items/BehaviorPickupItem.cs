//using System;
//using System.Numerics;
//using System.Text;
//using Newtonsoft.Json;
//using Unconscious.src.Behaviors.Player;
//using Unconscious.src.Entities;
//using Vintagestory.API.Client;
//using Vintagestory.API.Common;
//using Vintagestory.API.Common.Entities;
//using Vintagestory.API.Config;
//using Vintagestory.API.Datastructures;
//using Vintagestory.API.MathTools;
//using Vintagestory.API.Server;
//using Vintagestory.GameContent;

//namespace Unconscious.src.Behaviors.Items
//{
//    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
//    public class CollectibleBehaviorPickupItem : CollectibleBehaviorHealingItem, ICanHealCreature
//    {

//        [JsonProperty]
//        public float Health { get; set; } = 1;
//        [JsonProperty]
//        public float ApplicationTimeSec { get; set; } = 2;
//        [JsonProperty]
//        public float MaxApplicationTimeSec { get; set; } = 10;
//        [JsonProperty]
//        public int Ticks { get; set; } = 10;
//        [JsonProperty]
//        public float EffectDurationSec { get; set; } = 10;
//        [JsonProperty]
//        public bool CancelInAir { get; set; } = true;
//        [JsonProperty]
//        public bool CancelWhileSwimming { get; set; } = false;
//        [JsonProperty]
//        public AssetLocation? Sound { get; set; } = new AssetLocation("game:sounds/player/poultice");
//        [JsonProperty]
//        public AssetLocation? AppliedSound { get; set; } = new AssetLocation("game:sounds/player/poultice-applied");
//        [JsonProperty]
//        public float SoundRange { get; set; } = 8;
//        [JsonProperty]
//        public bool CanRevive { get; set; } = true;
//        [JsonProperty]
//        public bool AffectedByArmor { get; set; } = true;
//        [JsonProperty]
//        public float DelayToCancelSec { get; set; } = 0.5f;

//        protected new IProgressBar progressBarRender;

//        protected new ILoadedSound applicationSound;

//        protected new ICoreAPI api;

//        protected new float secondsUsedToCancel;



//        public BehaviorPickupItem(CollectibleObject collectable) : base(collectable)
//        {
//        }

//        public override void Initialize(JsonObject properties)
//        {
//            base.Initialize(properties);
//            Config = properties.AsObject<HealOverTimeConfig>(null);
//        }

//        public override void OnLoaded(ICoreAPI api)
//        {
//            if (api.Side.IsServer())
//            {
                
//            }

//            if (Config.Sound != null)
//            {
//                ICoreClientAPI coreClientAPI = api as ICoreClientAPI;
//                applicationSound = coreClientAPI != null ? coreClientAPI.World.LoadSound(new SoundParams
//                {
//                    DisposeOnFinish = false,
//                    Location = Config.Sound,
//                    ShouldLoop = true,
//                    Range = Config.SoundRange
//                }) : null;
//            }
//            this.api = api;
//        }

//        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
//        {

//            if (entitySel == null)
//            {
//                return;
//            }

//            if  (entitySel?.Entity == null && entitySel?.Entity is not EntityPlayer && !entitySel.Entity.WatchedAttributes.GetAsBool("unconscious"))
//            {
//                return;
//            }

//            if (CancelApplication(byEntity))
//            {
//                return;
//            }
//            handHandling = EnumHandHandling.PreventDefault;
//            handling = EnumHandling.PreventSubsequent;
//            ICoreAPI coreAPI = api;
//            if (coreAPI != null)
//            {
//                coreAPI.World.RegisterCallback(delegate (float _)
//                {
//                    ILoadedSound loadedSound = applicationSound;
//                    if (loadedSound == null)
//                    {
//                        return;
//                    }
//                    loadedSound.Stop();
//                }, (int)GetApplicationTime(byEntity) * 1000);
//            }
//            ICoreAPI coreAPI2 = api;
//            if (coreAPI2 != null && coreAPI2.Side == EnumAppSide.Client)
//            {
//                ModSystemProgressBar progressBarSystem = api.ModLoader.GetModSystem<ModSystemProgressBar>(true);
//                progressBarSystem.RemoveProgressbar(progressBarRender);
//                progressBarRender = progressBarSystem.AddProgressbar();
//            }
//            secondsUsedToCancel = 0f;

//            //base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
//        }

//        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
//        {
//            if (entitySel == null && entitySel?.Entity == null && entitySel?.Entity is not EntityPlayer)
//            {
//                ICoreAPI coreAPI = api;
//                if (coreAPI != null)
//                {
//                    ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
//                    if (modSystem != null)
//                    {
//                        modSystem.RemoveProgressbar(progressBarRender);
//                    }
//                }
//                return false;
//            }

//            if (!CancelApplication(byEntity))
//            {
//                secondsUsedToCancel = 0f;
//            }
//            else if (secondsUsedToCancel == 0f)
//            {
//                secondsUsedToCancel = secondsUsed;
//            }
//            if (CancelApplication(byEntity) && (double)(secondsUsed - secondsUsedToCancel) > 0.5)
//            {
//                return false;
//            }
//            ILoadedSound loadedSound = applicationSound;
//            if (loadedSound != null && loadedSound.HasStopped)
//            {
//                applicationSound.Start();
//            }
//            ILoadedSound loadedSound2 = applicationSound;
//            if (loadedSound2 != null)
//            {
//                loadedSound2.SetPosition((float)byEntity.Pos.X, (float)byEntity.Pos.InternalY, (float)byEntity.Pos.Z);
//            }
//            handling = (EnumHandling)1;
//            float progress = secondsUsed / (GetApplicationTime(byEntity) + (byEntity.World.Side == EnumAppSide.Client ? 0.3f : 0f));
//            if (progressBarRender != null)
//            {
//                progressBarRender.Progress = progress;
//            }
//            return progress < 1f;
//        }

//        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
//        {
//            ILoadedSound loadedSound = applicationSound;
//            if (loadedSound != null)
//            {
//                loadedSound.Stop();
//            }
//            ICoreAPI coreAPI = api;
//            if (coreAPI != null)
//            {
//                ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
//                if (modSystem != null)
//                {
//                    modSystem.RemoveProgressbar(progressBarRender);
//                }
//            }
//            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
//        }

//        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
//        {
//            ICoreAPI coreAPI = api;

//            if (entitySel == null)
//            {
//                if (coreAPI != null && !byEntity.Controls.CtrlKey)
//                {
//                    ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
//                    if (modSystem != null)
//                    {
//                        modSystem.RemoveProgressbar(progressBarRender);
//                    }
//                }
//                return;
//            }

//            ILoadedSound loadedSound = applicationSound;
//            if (loadedSound != null)
//            {
//                loadedSound.Stop();
//            }
           
//            if (coreAPI != null)
//            {
//                ModSystemProgressBar modSystem = coreAPI.ModLoader.GetModSystem<ModSystemProgressBar>(true);
//                if (modSystem != null)
//                {
//                    modSystem.RemoveProgressbar(progressBarRender);
//                }
//            }
//            handling = EnumHandling.Handled;
//            if (secondsUsed < GetPickupApplicationTime(byEntity) || byEntity.World.Side != EnumAppSide.Server)
//            {
//                return;
//            }

//            Entity targetEntity = GetTargetToPickupEntity(slot, byEntity, entitySel);
//            if (targetEntity != null)
//            {
//                BehaviorPickupPlayer behaviorRevivePlayer = targetEntity.GetBehavior<BehaviorPickupPlayer>();
//                if (behaviorRevivePlayer != null && targetEntity.WatchedAttributes.GetAsBool("unconscious") && UnconsciousModSystem.getConfig().EnableCarryMechanic)
//                {
//                    behaviorRevivePlayer.StartCarryingPlayer(byEntity as EntityPlayer, targetEntity as EntityPlayer);
//                    handling = EnumHandling.PreventDefault;
//                }
//            }


//            if (Config.AppliedSound != null)
//            {
//                byEntity.World.PlaySoundAt(Config.AppliedSound, byEntity, null, false, Config.SoundRange, 1f);
//            }
//            //slot.TakeOut(1);
//            //slot.MarkDirty();
//        }

     
//        private void splitStack(ItemSlot slot, EntityAgent byEntity)
//        {
//            if (slot.StackSize > 1)
//            {
//                var split = slot.TakeOut(slot.StackSize - 1);
//                split.Attributes.RemoveAttribute("clothId");
//                split.Attributes.RemoveAttribute("ropeHeldByEntityId");

//                if (!byEntity.TryGiveItemStack(split))
//                {
//                    api.World.SpawnItemEntity(split, byEntity.ServerPos.XYZ);
//                }
//            }
//        }

//        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
//        {
//            string text = "unconscious:pickup-item-info";
//            dsc.AppendLine(Lang.Get(text));
//        }

//        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
//        {
//            return [];
//        }

//        public virtual WorldInteraction[] GetHealInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
//        {
//            return [];
//        }

//        protected virtual float GetPickupApplicationTime(Entity byEntity)
//        {
//            float healingEffectiveness = 0f;
//            if (Config.AffectedByArmor)
//            {
//                healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
//                healingEffectiveness = Math.Clamp(healingEffectiveness, 0f, 2f) - 1f;
//            }
//            if (healingEffectiveness < 0f)
//            {
//                return Config.ApplicationTimeSec + (Config.ApplicationTimeSec - Config.MaxApplicationTimeSec) * healingEffectiveness;
//            }
//            if (healingEffectiveness > 0f)
//            {
//                return Config.ApplicationTimeSec * (1f - healingEffectiveness);
//            }
//            return Config.ApplicationTimeSec;
//        }

//        //private void EnsureEntityMovable(Entity targetEntity)
//        //{
//        //    if (targetEntity is EntityPlayer playerEntity)
//        //    {
//        //        // Ensure the entity is not grounded or stuck
//        //        playerEntity.SidedPos.Motion.Y = 0.1f; // Slight upward motion to prevent ground sticking
//        //        playerEntity.Properties.KnockbackResistance = 0f; // Reduce resistance to movement
//        //        playerEntity.Properties.Weight = 10f; // Adjust mass to make dragging easier (tweak as needed)
//        //        playerEntity.WatchedAttributes.SetBool("isMovable", true); // Flag to indicate movability

//        //        // Notify clients of position and attribute updates
//        //        if (api is ICoreServerAPI sapi)
//        //        {
//        //            //sapi.Network.BroadcastEntityPacket(playerEntity.EntityId, 1, null); // Sync position
//        //            playerEntity.WatchedAttributes.MarkPathDirty("isMovable");
//        //        }
//        //    }
//        //}

//        protected virtual Entity GetTargetToPickupEntity(ItemSlot slot, EntityAgent byEntity, EntitySelection entitySelection)
//        {
//            Entity targetEntity = null;
//            Entity selectedEntity = entitySelection != null ? entitySelection.Entity : null;
//            if (selectedEntity == null)
//            {
//                return targetEntity;
//            }

//            if (selectedEntity is EntityClonePlayer)
//            {
//                targetEntity = selectedEntity;
//            }

//            BehaviorPickupPlayer selectedEntityHealthBehavior = selectedEntity.GetBehavior<BehaviorPickupPlayer>();
//            if (byEntity.Controls.CtrlKey && !byEntity.Controls.Forward && !byEntity.Controls.Backward && !byEntity.Controls.Left && !byEntity.Controls.Right && selectedEntityHealthBehavior != null)
//            {
//                targetEntity = selectedEntity;
//            }
//            return targetEntity;
//        }
//    }
//}
