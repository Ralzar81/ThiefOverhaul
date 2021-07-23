using UnityEngine;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Serialization;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game;

namespace ThiefOverhaul
{

    public class ItemLockpicks : DaggerfallUnityItem
    {
        ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemLockpicks() : base(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Ring)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Ring0, EquipSlots.Ring1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipChain;
        }

    }

    public class ItemMark : DaggerfallUnityItem
    {
        ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemMark() : base(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Mark)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Mark0, EquipSlots.Mark1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipClothing;
        }

    }

    public class ItemBracelet : DaggerfallUnityItem
    {
        ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemBracelet() : base(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracelet)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Bracelet0, EquipSlots.Bracelet1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipJewellery;
        }

    }

    public class ItemRope : DaggerfallUnityItem
    {
        ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemRope() : base(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Bracer)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Bracer0, EquipSlots.Bracer1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipClothing;
        }

    }

    public class ItemPebbles : DaggerfallUnityItem
    {
        ItemEquipTable itemEquipTable = GameManager.Instance.PlayerEntity.ItemEquipTable;

        public ItemPebbles() : base(ItemGroups.MiscItems, ThiefOverhaul.templateIndex_Crystal)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Crystal0, EquipSlots.Crystal0);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipClothing;
        }

    }
}
