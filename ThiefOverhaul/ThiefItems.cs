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

        public ItemLockpicks() : base(ItemGroups.Jewellery, ThiefOverhaul.templateIndex_LockPicks)
        {

        }

        public override EquipSlots GetEquipSlot()
        {
            return itemEquipTable.GetFirstSlot(EquipSlots.Ring0, EquipSlots.Ring1);
        }

        public override SoundClips GetEquipSound()
        {
            return SoundClips.EquipJewellery;
        }

    }
}
