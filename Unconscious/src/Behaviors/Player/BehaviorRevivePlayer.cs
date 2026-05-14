using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unconscious.src.Behaviors.Items;
using Unconscious.src.Items;
using Unconscious.src.Player;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Unconscious.src.Behaviors.Player
{
    public class BehaviorRevivePlayer : EntityBehavior
    {
        private readonly ICoreAPI api; // Added for client-side rendering

        protected IProgressBar progressBarRender;

        public BehaviorRevivePlayer(Entity entity) : base(entity)
        {
            api = entity.Api;
        }

        public override string PropertyName() => "UnconsciousReviveBehavior";

        public override void Initialize(EntityProperties properties, JsonObject attributes)
        {
            base.Initialize(properties, attributes);
        }

     

        public static WorldInteraction GetReviveInteractionHelp(ICoreAPI api)
        {
            ItemStack[] orCreate = ObjectCacheUtil.GetOrCreate(api, "smellingsaltsStacks", delegate
            {
                List<ItemStack> list = new List<ItemStack>();
                foreach (Item item in api.World.Items)
                {
                    if (item.HasBehavior<BehaviorRevivingItem>() || item is ItemSmellingSalts)
                    {
                        list.Add(new ItemStack(item));
                    }
                }

                return list.ToArray();
            });
            return new WorldInteraction
            {
                ActionLangCode = "reviveplayer",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift",
                Itemstacks = orCreate,
                RequireFreeHand = true
            };
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
        {
            WorldInteraction[] array = base.GetInteractionHelp(world, es, player, ref handled);
            bool isUnconscious = entity.WatchedAttributes.GetAsBool("unconscious", false);
            if (isUnconscious)
            {
                if (array == null)
                {
                    array = Array.Empty<WorldInteraction>();
                }

                array = array.Append(GetReviveInteractionHelp(world.Api));
            }

            return array;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
        {
            base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

            if (byEntity is not EntityPlayer interactingPlayer || entity is not EntityPlayer targetPlayer || mode != EnumInteractMode.Interact)
            {
                return;
            }

            if (byEntity.World.Side != EnumAppSide.Server)
            {
                return;
            }
        }

        private void SendErrorMessage(string message, EntityPlayer interactingPlayer)
        {
            var serverPlayer = interactingPlayer.Player as IServerPlayer;
            ICoreServerAPI sapi = api as ICoreServerAPI;

            sapi.SendIngameError(serverPlayer, "ReviveCycle", message, Array.Empty<object>());
        }

        public void FinishRevive(EntityPlayer interactingPlayer, EntityPlayer targetPlayer, DamageSource damageSource, float healAmount)
        {
            SendErrorMessage($"{targetPlayer.Player.PlayerName} {Lang.Get("unconscious:reviving-success")}", interactingPlayer);
            UnconsciousModSystem.HandlePlayerPickup(targetPlayer, damageSource, healAmount);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            base.OnEntityDespawn(despawn);
        }
    }
}