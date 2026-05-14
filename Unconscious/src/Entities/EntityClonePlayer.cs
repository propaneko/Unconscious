using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using System;
using Vintagestory.API.MathTools;
using System.IO;
using Vintagestory.API.Datastructures;
using Unconscious.src.Inventory;

namespace Unconscious.src.Entities
{
    public class EntityClonePlayer : EntityAgent
    {
        protected InventoryBase inv;
        protected InventoryBase invOthers;
        public string sourceEntityUID;
        public string sourceName;
        long timeStampToDisappear;
        double counter = 0;
        public static bool coActive = false;

        public override bool StoreWithChunk
        {
            get { return true; }
        }


        public IInventory GearInventory
        {
            get
            {
                EntityBehaviorTexturedClothing ebhtc = this.GetBehavior<EntityBehaviorTexturedClothing>();
                InventoryBase inv = ebhtc.Inventory;
                return inv;
            }
        }
        public IInventory OtherInventory
        {
            get
            {
                return invOthers;
            }
        }

        public int ActiveSlotNumber
        {
            get { return WatchedAttributes.GetInt("ActiveSlotNumber", 16); }
            set { WatchedAttributes.SetInt("ActiveSlotNumber", GameMath.Clamp(value, 16, 25)); WatchedAttributes.MarkPathDirty("ActiveSlotNumber"); }
        }

        public override ItemSlot RightHandItemSlot
        {
            get
            {
                return inv[ActiveSlotNumber];
            }
        }

        public override ItemSlot LeftHandItemSlot
        {
            get
            {
                return inv[16];
            }
        }

        public override byte[] LightHsv
        {
            get
            {
                byte[] rightHsv = RightHandItemSlot?.Itemstack?.Block?.GetLightHsv(World.BlockAccessor, null, RightHandItemSlot.Itemstack);
                byte[] leftHsv = LeftHandItemSlot?.Itemstack?.Block?.GetLightHsv(World.BlockAccessor, null, LeftHandItemSlot.Itemstack);

                if (rightHsv == null) return leftHsv;
                if (leftHsv == null) return rightHsv;

                float totalval = rightHsv[2] + leftHsv[2];
                float t = leftHsv[2] / totalval;

                return new byte[]
                {
                    (byte)(leftHsv[0] * t + rightHsv[0] * (1-t)),
                    (byte)(leftHsv[1] * t + rightHsv[1] * (1-t)),
                    Math.Max(leftHsv[2], rightHsv[2])
                };
            }
        }

        public EntityClonePlayer() : base()
        {
            //WatchedAttributes.GetString()
            if (coActive)
            {
                inv = new InventoryNPCGear(null, null, 44);
            }
            else
            {
                inv = new InventoryNPCGear(null, null, 17);
            }
            invOthers = new InventoryOther(null, null, 29);
        }
        public void setSourceEntityUID(string uid)
        {
            sourceEntityUID = uid;
        }
        public override void Initialize(EntityProperties properties, ICoreAPI api, long chunkindex3d)
        {
            base.Initialize(properties, api, chunkindex3d);

            coActive = api.ModLoader.IsModEnabled("combatoverhaul");
            if (coActive)
            {
                inv = new InventoryNPCGear(null, null, 44);
            }

            inv.LateInitialize("gearinv-" + EntityId, api);
            invOthers.LateInitialize("invOther-" + EntityId, api);

            //AnimManager.HeadController = new EntityHeadController(AnimManager, this, Properties.Client.LoadedShape);
        }

        public override void OnEntitySpawn()
        {
            base.OnEntitySpawn();
            if (World.Side == EnumAppSide.Client)
            {
                (Properties.Client.Renderer as EntityShapeRenderer).DoRenderHeldItem = true;
            }
            if (World.Side == EnumAppSide.Server && sourceEntityUID != null)
            {
                UnconsciousModSystem.playersClones.Add(sourceEntityUID, this);
            }
            JsonObject attributes = base.Properties.Attributes;
            JsonObject inv = (attributes != null) ? attributes["inventory"] : null;
            if (inv != null && inv.Exists)
            {
                foreach (JsonItemStack jstack in inv.AsArray<JsonItemStack>(null, null))
                {
                    if (jstack.Resolve(this.World, "player bot inventory", true))
                    {
                        this.TryGiveItemStack(jstack.ResolvedItemstack);
                    }
                }
            }
        }
        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (World.Side == EnumAppSide.Server && sourceEntityUID != "")
            {
                if (!UnconsciousModSystem.playersClones.ContainsKey(sourceEntityUID))
                {
                    UnconsciousModSystem.playersClones.Add(sourceEntityUID, this);
                }
            }
        }
        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            TreeAttribute tree;
            WatchedAttributes["gearInv"] = tree = new TreeAttribute();
            inv.ToTreeAttributes(tree);

            TreeAttribute treeO;
            WatchedAttributes["invOther"] = treeO = new TreeAttribute();
            inv.ToTreeAttributes(treeO);
            WatchedAttributes.SetString("sourceEntityUID", sourceEntityUID);
            WatchedAttributes.SetString("sourceName", sourceName);
            base.ToBytes(writer, forClient);
        }

        public void setTimestampToDisappear(long val)
        {
            timeStampToDisappear = val;
        }
        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            TreeAttribute tree = WatchedAttributes["gearInv"] as TreeAttribute;
            if (tree != null) inv.FromTreeAttributes(tree);

            TreeAttribute treeO = WatchedAttributes["invOther"] as TreeAttribute;
            if (treeO != null) inv.FromTreeAttributes(treeO);

            sourceEntityUID = WatchedAttributes.GetString("sourceEntityUID");
            sourceName = WatchedAttributes.GetString("sourceName");
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot slot, Vec3d hitPosition, EnumInteractMode mode)
        {
            base.OnInteract(byEntity, slot, hitPosition, mode);
        }

        //public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        //{
        //    base.Die(reason, damageSourceForDeath);
        //    if (World.Side == EnumAppSide.Server)
        //        SleepingPlayerModSystem.playersClones.Remove(sourceEntityUID);
        //    if (reason != EnumDespawnReason.Removed && reason != EnumDespawnReason.Unload && reason != EnumDespawnReason.OutOfRange)
        //    {
        //        (GearInventory as InventoryBase).DropAll(SidedPos.XYZ);
        //        invOthers.Api = this.Api;
        //        invOthers.DropAll(SidedPos.XYZ);
             
        //    }
        //}
        //public void addDrops(IServerPlayer player)
        //{
        //    //Players cloths and armor
        //    InventoryBase playerCharacter = (InventoryBase)player.InventoryManager.GetOwnInventory("character");
        //    var cc = player.Entity.GetBehavior<EntityBehaviorPlayerInventory>()?.Inventory;
        //    EntityBehaviorTexturedClothing ebhtc = this.GetBehavior<EntityBehaviorTexturedClothing>();
        //    InventoryBase inv2 = ebhtc.Inventory;

        //    IInventory playerHotbar = player.InventoryManager.GetHotbarInventory();
        //    InventoryPlayerBackPacks playerBackpacks = (InventoryPlayerBackPacks)player.InventoryManager.GetOwnInventory("backpack");
        //    if (playerCharacter.Count > inv.Count || invOthers.Count < playerHotbar.Count + 4)
        //    {
        //        inv = new InventoryNPCGear(null, null, playerCharacter.Count);
        //        invOthers = new InventoryOther(null, null, playerHotbar.Count + 4);
        //    }
        //    for (int i = 0; i < playerCharacter.Count; i++)
        //    {
        //        if (playerCharacter[i].Itemstack == null)
        //            continue;

        //        GearInventory[i].Itemstack = playerCharacter[i].Itemstack.Clone();
        //        GearInventory[i].MarkDirty();
        //    }


        //    for (int i = 0; i < playerHotbar.Count; i++)
        //    {
        //        if (playerHotbar[i].Itemstack == null)
        //            continue;
        //        var t = playerHotbar[i];
        //        invOthers[i].Itemstack = playerHotbar[i].Itemstack.Clone();

        //    }

        //    for (int i = 0; i < 4; i++)
        //    {
        //        if (playerBackpacks[i].Itemstack == null)
        //            continue;
        //        invOthers[i + 11].Itemstack = playerBackpacks[i].Itemstack.Clone();
        //    }
        //}
        //public void returnDrops(IServerPlayer player)
        //{
        //    //Players cloths and armor
        //    InventoryCharacter playerCharacter = (InventoryCharacter)player.InventoryManager.GetOwnInventory("character");
        //    //Player's hotbar
        //    IInventory playerHotbar = player.InventoryManager.GetHotbarInventory();
        //    InventoryPlayerBackPacks playerBackpacks = (InventoryPlayerBackPacks)player.InventoryManager.GetOwnInventory("backpack");
        //    if (playerCharacter.Count + playerHotbar.Count + playerBackpacks.Count > inv.Count)
        //    {
        //        inv = new InventoryNPCGear(null, null, playerCharacter.Count + playerHotbar.Count + playerBackpacks.Count);
        //    }
        //    //Armor, cloths
        //    for (int i = 0; i < 15; i++)
        //    {
        //        if (inv[i].Itemstack == null)
        //            continue;
        //        playerCharacter[i].Itemstack = inv[i].Itemstack.Clone();
        //        playerCharacter[i].MarkDirty();
        //        inv[i].Itemstack = null;
        //    }

        //    //Hotbar
         
        //    for (int i = 0; i < 11; i++)
        //    {
        //        if (inv[15 + i].Itemstack == null)
        //            continue;
        //        playerHotbar[i].Itemstack = inv[15 + i].Itemstack;
        //        playerHotbar[i].MarkDirty();
        //        inv[15 + i].Itemstack = null;
        //    }

        //    //Bags
           
        //    for (int i = 0; i < inv.Count - 11 - 15; i++)
        //    {
        //        if (inv[i + 11 + 15].Itemstack == null)
        //            continue;
        //        playerBackpacks[i].Itemstack = inv[i + 11 + 15].Itemstack;
        //        playerBackpacks[i].MarkDirty();
        //        inv[i + 11 + 15].Itemstack = null;
        //    }
        //}
        //public override bool ReceiveDamage(DamageSource damageSource, float damage)
        //{
        //    if (!coActive)
        //    {
        //        float dmg = handleDefense(damage, damageSource);
        //        return base.ReceiveDamage(damageSource, dmg);
        //    }
        //    return base.ReceiveDamage(damageSource, damage);
        //}


        //Dictionary<int, EnumCharacterDressType[]> clothingDamageTargetsByAttackTacket = new Dictionary<int, EnumCharacterDressType[]>()
        //{
        //    { 0, new EnumCharacterDressType[] { EnumCharacterDressType.Head, EnumCharacterDressType.Face, EnumCharacterDressType.Neck } },
        //    { 1, new EnumCharacterDressType[] { EnumCharacterDressType.UpperBody, EnumCharacterDressType.UpperBodyOver, EnumCharacterDressType.Shoulder, EnumCharacterDressType.Arm, EnumCharacterDressType.Hand } },
        //    { 2, new EnumCharacterDressType[] { EnumCharacterDressType.LowerBody, EnumCharacterDressType.Foot } }
        //};
        //private float handleDefense(float damage, DamageSource dmgSource)
        //{
        //    // Does not protect against non-attack damages
        //    EnumDamageType type = dmgSource.Type;
        //    if (type != EnumDamageType.BluntAttack && type != EnumDamageType.PiercingAttack && type != EnumDamageType.SlashingAttack) return damage;
        //    if (dmgSource.Source == EnumDamageSource.Internal || dmgSource.Source == EnumDamageSource.Suicide) return damage;

        //    ItemSlot armorSlot;
        //    IInventory inv = GearInventory;
        //    double rnd = Api.World.Rand.NextDouble();


        //    int attackTarget;

        //    if ((rnd -= 0.2) < 0)
        //    {
        //        // Head
        //        armorSlot = inv[12];
        //        attackTarget = 0;
        //    }
        //    else if ((rnd -= 0.5) < 0)
        //    {
        //        // Body
        //        armorSlot = inv[13];
        //        attackTarget = 1;
        //    }
        //    else
        //    {
        //        // Legs
        //        armorSlot = inv[14];
        //        attackTarget = 2;
        //    }

        //    // Apply full damage if no armor is in this slot
        //    if (armorSlot.Empty || !(armorSlot.Itemstack.Item is ItemWearable))
        //    {
        //        EnumCharacterDressType[] dressTargets = clothingDamageTargetsByAttackTacket[attackTarget];
        //        EnumCharacterDressType target = dressTargets[Api.World.Rand.Next(dressTargets.Length)];

        //        ItemSlot targetslot = GearInventory[(int)target];
        //        if (!targetslot.Empty)
        //        {
        //            // Wolf: 10 hp damage = 10% condition loss
        //            // Ram: 10 hp damage = 2.5% condition loss
        //            // Bronze locust: 10 hp damage = 5% condition loss
        //            float mul = 0.25f;
        //            if (type == EnumDamageType.SlashingAttack) mul = 1f;
        //            if (type == EnumDamageType.PiercingAttack) mul = 0.5f;

        //            float diff = -damage / 100 * mul;

        //            if (Math.Abs(diff) > 0.05)
        //            {
        //                Api.World.PlaySoundAt(new AssetLocation("sounds/effect/clothrip"), this);
        //            }

        //            (targetslot.Itemstack.Collectible as ItemWearable)?.ChangeCondition(targetslot, diff);
        //        }

        //        return damage;
        //    }

        //    ProtectionModifiers protMods = (armorSlot.Itemstack.Item as ItemWearable).ProtectionModifiers;

        //    int weaponTier = dmgSource.DamageTier;
        //    float flatDmgProt = protMods.FlatDamageReduction;
        //    float percentProt = protMods.RelativeProtection;

        //    for (int tier = 1; tier <= weaponTier; tier++)
        //    {
        //        bool aboveTier = tier > protMods.ProtectionTier;

        //        float flatLoss = aboveTier ? protMods.PerTierFlatDamageReductionLoss[1] : protMods.PerTierFlatDamageReductionLoss[0];
        //        float percLoss = aboveTier ? protMods.PerTierRelativeProtectionLoss[1] : protMods.PerTierRelativeProtectionLoss[0];

        //        if (aboveTier && protMods.HighDamageTierResistant)
        //        {
        //            flatLoss /= 2;
        //            percLoss /= 2;
        //        }

        //        flatDmgProt -= flatLoss;
        //        percentProt *= 1 - percLoss;
        //    }

        //    // Durability loss is the one before the damage reductions
        //    float durabilityLoss = 0.5f + damage * Math.Max(0.5f, (weaponTier - protMods.ProtectionTier) * 3);
        //    int durabilityLossInt = GameMath.RoundRandom(Api.World.Rand, durabilityLoss);

        //    // Now reduce the damage
        //    damage = Math.Max(0, damage - flatDmgProt);
        //    damage *= 1 - Math.Max(0, percentProt);

        //    armorSlot.Itemstack.Collectible.DamageItem(Api.World, this, armorSlot, durabilityLossInt);

        //    if (armorSlot.Empty)
        //    {
        //        Api.World.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), this);
        //    }

        //    return damage;
        //}
    }
}