using System.Collections.Generic;
using CharacterStats;
using GameplayTags;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// An item that goes in an equipment slot and changes its wearer while it is in effect: stat modifiers,
    /// tags, and any extra Effects (granting abilities and the like, see EquipEffect). When that is depends on the slot:
    ///  - armour and rings: while equipped;
    ///  - weapons: while held, i.e. in the selected slot of a hand (not while waiting to be cycled to);
    ///  - spells: while selected (and attuned within the character's attunement).
    /// Equipment does the applying; the item itself is the source, per stack, so two copies apply twice.
    /// </summary>
    public abstract class EquipmentItem : ItemDefinition
    {
        [Header("Equipped")]
        [Tooltip("Counts towards the wearer's equip load (Equipment's Equip Weight Stat) while equipped in any slot.")]
        [Min(0f)]
        [SerializeField] float weight;

        [Tooltip("Applied to the wearer's stats while in effect (see the class description for when that is).")]
        [SerializeField] List<StatModifierSpec> modifiers = new List<StatModifierSpec>();


        [Tooltip("Gameplay tags the wearer has while in effect (e.g. 'Fire Immune').")]
        [SerializeField] List<GameplayTag> tags = new List<GameplayTag>();

        [Tooltip("Anything else done while in effect (granting abilities, for example). Add your own by subclassing EquipEffect.")]
        [SerializeReference] List<EquipEffect> effects = new List<EquipEffect>();

        public float Weight => weight;
        public IReadOnlyList<StatModifierSpec> Modifiers => modifiers;
        public IReadOnlyList<GameplayTag> Tags => tags;
        public IReadOnlyList<EquipEffect> Effects => effects;

        /// <summary>True when two of this item may not be equipped at once (rings).</summary>
        public virtual bool OnlyOneEquipped => false;
    }
}
