using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// A spell (sorcery, miracle...): attuned in a spell slot, and in effect while selected. Put the spell's
    /// ability in Abilities; it is granted while the spell is the selected one, so casting is that ability.
    ///
    /// Attunement: with an Attunement Stat set on Equipment, the stat's value is how many attunement slots the
    /// character has, and each spell takes Attunement Cost of them. Spells past that (the stat dropped after they
    /// were attuned) stay attuned but can't be selected until it is high enough again.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Spell", fileName = "Spell Item")]
    public class SpellItem : EquipmentItem
    {
        [Header("Spell")]
        [Tooltip("How many attunement slots it takes.")]
        [Min(1)]
        [SerializeField] int attunementCost = 1;

        public int AttunementCost => attunementCost;

        public override bool CanGoIn(EquipSlotType slot) => slot == EquipSlotType.Spell;
    }
}
