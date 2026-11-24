namespace InventorySystem
{
    /// <summary>The kinds of equipment slot a character has. How many of each is set on Equipment (Layout).</summary>
    public enum EquipSlotType
    {
        /// <summary>Weapons (and catalysts) for the right hand. One is held at a time; the others are cycled to.</summary>
        RightHand,

        /// <summary>Weapons, shields and catalysts for the left hand. One is held at a time; the others are cycled to.</summary>
        LeftHand,

        Head,
        Chest,
        Hands,
        Legs,

        /// <summary>All worn at once.</summary>
        Ring,

        /// <summary>Consumables. One is selected and used at a time; the others are cycled to.</summary>
        QuickItem,

        /// <summary>Attuned spells. One is selected (and granted) at a time; the others are cycled to.</summary>
        Spell,
    }
}
