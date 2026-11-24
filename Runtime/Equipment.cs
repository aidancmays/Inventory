using System;
using System.Collections.Generic;
using CharacterStats;
using GameplayTags;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// A character's souls-like equipment slots: weapons for each hand (several, one held at a time), head, chest,
    /// hands and legs armour, rings, quick items and attuned spells. How many of each is set in Layout.
    /// Slots hold stacks from the character's Inventory; an item stays in its bag while equipped, and is unequipped
    /// if it leaves the inventory (used up, dropped, moved to storage).
    ///
    /// What is in effect (see EquipmentItem): armour and rings while equipped; the selected weapon of each hand;
    /// the selected spell. Their stat modifiers go on the StatSheet (if there is one) and their tags onto the
    /// TagHolder, each with the stack as the source, and their EquipEffects are applied. HeldChanged tells your
    /// weapon code which weapon to put in each hand.
    ///
    /// Cycling (Cycle) and using quick items (UseQuickItem) are for input code to call; nothing here reads input.
    /// Put it on the character root next to Inventory (and StatSheet, if the character has stats).
    /// </summary>
    [DefaultExecutionOrder(10)] // Start after most scripts, so starting equipment is applied over their defaults
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Inventory))]
    public class Equipment : MonoBehaviour
    {
        [Serializable]
        public class Layout
        {
            [Min(0)] public int rightHand = 3;
            [Min(0)] public int leftHand = 3;
            [Min(0)] public int rings = 4;
            [Min(0)] public int quickItems = 5;
            [Min(0)] public int spells = 10;

            [Tooltip("Off: no armour slots (e.g. for a creature).")]
            public bool armor = true;

            public int CountOf(EquipSlotType type)
            {
                switch (type)
                {
                    case EquipSlotType.RightHand: return rightHand;
                    case EquipSlotType.LeftHand: return leftHand;
                    case EquipSlotType.Ring: return rings;
                    case EquipSlotType.QuickItem: return quickItems;
                    case EquipSlotType.Spell: return spells;
                    default: return armor ? 1 : 0;
                }
            }
        }

        [Serializable]
        public struct StartingItem
        {
            public EquipSlotType slot;
            [Min(0)] public int index;
            [Tooltip("Equipped from the inventory; added to it first if it is not there.")]
            public ItemDefinition item;
        }

        [SerializeField] Layout layout = new Layout();

        [Tooltip("Optional. How many attunement slots the character has, for spells. Empty = every spell slot can be used.")]
        [SerializeField] StatDefinition attunementStat;

        [Tooltip("Optional. Gets the total weight of everything equipped as a Flat modifier (equip load, for roll speed and the like).")]
        [SerializeField] StatDefinition equipWeightStat;

        [SerializeField] List<StartingItem> startingEquipment = new List<StartingItem>();

        static readonly EquipSlotType[] AllTypes = (EquipSlotType[])Enum.GetValues(typeof(EquipSlotType));

        readonly Dictionary<EquipSlotType, EquipSlot[]> slots = new Dictionary<EquipSlotType, EquipSlot[]>();
        readonly Dictionary<EquipSlotType, int> selected = new Dictionary<EquipSlotType, int>();

        // What is in effect, in the order it was applied, with the hand a weapon was applied for (null otherwise).
        readonly List<(ItemStack stack, object hand)> applied = new List<(ItemStack, object)>();
        readonly List<(ItemStack stack, object hand)> wanted = new List<(ItemStack, object)>();
        readonly ItemStack[] heldWeapon = new ItemStack[2];
        readonly ItemStack[] reportedWeapon = new ItemStack[2];
        readonly bool[] reported = new bool[2];
        readonly StatModifierSpec weightModifier = new StatModifierSpec { op = ModifierOp.Flat };

        readonly HashSet<object> stowers = new HashSet<object>();

        StatSheet stats;
        TagHolder tags;
        Inventory inventory;
        bool started;
        bool refreshing;
        bool refreshQueued;

        /// <summary>A slot's contents changed (equipped, unequipped, or its stack ran out).</summary>
        public event Action<EquipSlot> SlotChanged;

        /// <summary>(type, index): a different slot of a cycled type is selected.</summary>
        public event Action<EquipSlotType, int> SelectionChanged;

        /// <summary>
        /// (hand, stack): the weapon held in a hand changed (RightHand or LeftHand; stack null = empty-handed).
        /// Raised only on changes, once Start has equipped the starting equipment. Put the WeaponItem's Prefab in the
        /// hand from here. Each hand is reported once at start, even when empty, so your code can tell "no weapon item"
        /// apart from "not reported yet" (only replace a hand's default weapon once a weapon item has been held there).
        /// </summary>
        public event Action<EquipSlotType, ItemStack> HeldChanged;

        /// <summary>HandsStowed changed.</summary>
        public event Action<bool> HandsStowedChanged;

        public Inventory Inventory => inventory;

        /// <summary>Total weight of everything equipped, in any slot.</summary>
        public float EquippedWeight { get; private set; }

        /// <summary>The character's stats, or null if it has no StatSheet.</summary>
        public StatSheet Stats => stats;

        /// <summary>The character's tags (a TagHolder is added if there is none).</summary>
        public TagHolder Tags => tags;

        void Awake()
        {
            stats = GetComponent<StatSheet>();
            tags = TagHolder.On(this);
            inventory = GetComponent<Inventory>();

            foreach (EquipSlotType type in AllTypes)
            {
                var array = new EquipSlot[layout.CountOf(type)];
                for (int i = 0; i < array.Length; i++) array[i] = new EquipSlot(type, i);
                slots[type] = array;
                if (IsCycled(type)) selected[type] = 0;
            }
        }

        void OnEnable()
        {
            inventory.StackChanged += HandleStackChanged;
            if (stats != null) stats.StatChanged += HandleStatChanged;
        }

        void OnDisable()
        {
            inventory.StackChanged -= HandleStackChanged;
            if (stats != null) stats.StatChanged -= HandleStatChanged;
        }

        /// <summary>
        /// Equip the Starting Equipment in Start. Set it false before Start on a copy whose equipment comes from
        /// somewhere else (a networked player mirroring the server's), or it would equip its own.
        /// </summary>
        public bool UseStartingEquipment { get; set; } = true;

        void Start()
        {
            foreach (StartingItem entry in startingEquipment)
            {
                if (!UseStartingEquipment) break;
                if (entry.item == null) continue;

                ItemStack stack = FindUnequipped(entry.item);
                if (stack == null && inventory.Add(entry.item) > 0) stack = FindUnequipped(entry.item);

                string reason = null;
                if (stack == null || !Equip(stack, entry.slot, entry.index, out reason))
                    Debug.LogWarning($"{name}: could not equip starting '{entry.item.DisplayName}' in {entry.slot} {entry.index + 1}: {reason ?? "it is not in the inventory"}.", this);
            }

            started = true;
            Refresh();
        }

        void OnDestroy()
        {
            // Take back whatever is in effect, so nothing is left behind on the stats, abilities or tags.
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                RemoveEffects(applied[i].stack, applied[i].hand);
                applied.RemoveAt(i);
            }
            if (stats != null) stats.RemoveModifiers(this);
        }

        // ---- Reading ----

        /// <summary>
        /// True while something has put the hands away (see StowHands): neither hand holds a weapon, so the held
        /// weapons' modifiers, tags and effects are off and HeldChanged reports both hands empty. Slots and selections
        /// are untouched, and the weapons come back out when nothing stows the hands any more.
        /// </summary>
        public bool HandsStowed => stowers.Count > 0;

        /// <summary>The slots of one type (empty array if the layout has none).</summary>
        public IReadOnlyList<EquipSlot> Slots(EquipSlotType type) => slots[type];

        /// <summary>One slot, or null if there is no such slot.</summary>
        public EquipSlot Get(EquipSlotType type, int index)
        {
            EquipSlot[] array = slots[type];
            return index >= 0 && index < array.Length ? array[index] : null;
        }

        /// <summary>Types with several slots of which one is selected at a time: hands, quick items, spells.</summary>
        public static bool IsCycled(EquipSlotType type)
        {
            return type == EquipSlotType.RightHand || type == EquipSlotType.LeftHand
                || type == EquipSlotType.QuickItem || type == EquipSlotType.Spell;
        }

        /// <summary>The selected index of a cycled type (0 for others).</summary>
        public int Selected(EquipSlotType type) => selected.TryGetValue(type, out int index) ? index : 0;

        /// <summary>The selected slot of a cycled type. Null if the layout has none.</summary>
        public EquipSlot SelectedSlot(EquipSlotType type) => Get(type, Selected(type));

        /// <summary>The weapon stack held in a hand (RightHand or LeftHand). Null = unarmed.</summary>
        public ItemStack InHand(EquipSlotType hand) => hand == EquipSlotType.LeftHand ? heldWeapon[1] : hand == EquipSlotType.RightHand ? heldWeapon[0] : null;

        public ItemStack SelectedQuickItem => SelectedSlot(EquipSlotType.QuickItem)?.Item;

        /// <summary>The selected spell, if it is within the attunement (else null).</summary>
        public ItemStack SelectedSpell
        {
            get
            {
                EquipSlot slot = SelectedSlot(EquipSlotType.Spell);
                return slot != null && IsUsable(slot) ? slot.Item : null;
            }
        }

        public bool IsEquipped(ItemStack stack) => SlotOf(stack) != null;

        /// <summary>The slot holding a stack, or null.</summary>
        public EquipSlot SlotOf(ItemStack stack)
        {
            if (stack == null) return null;

            foreach (EquipSlot[] array in slots.Values)
            {
                foreach (EquipSlot slot in array)
                {
                    if (slot.Item == stack) return slot;
                }
            }
            return null;
        }

        /// <summary>How many attunement slots the character has (int.MaxValue with no Attunement Stat).</summary>
        public int AttunementCapacity
        {
            get
            {
                if (attunementStat == null || stats == null) return int.MaxValue;
                return Mathf.Max(0, Mathf.FloorToInt(stats.Get(attunementStat) + 0.0001f));
            }
        }

        /// <summary>Attunement slots taken by every spell attuned.</summary>
        public int AttunementUsed
        {
            get
            {
                int used = 0;
                foreach (EquipSlot slot in slots[EquipSlotType.Spell]) used += CostOf(slot.Item);
                return used;
            }
        }

        /// <summary>
        /// Can the slot's item be selected? False when empty. For spells, also false when the spells before it
        /// and this one take more attunement than the character has.
        /// </summary>
        public bool IsUsable(EquipSlot slot)
        {
            if (slot == null || slot.IsEmpty) return false;
            if (slot.Type != EquipSlotType.Spell) return true;

            int capacity = AttunementCapacity;
            int used = 0;
            foreach (EquipSlot spell in slots[EquipSlotType.Spell])
            {
                used += CostOf(spell.Item);
                if (spell == slot) return used <= capacity;
            }
            return false;
        }

        // ---- Equipping ----

        /// <summary>Could the stack go in that slot now? Reason says why not (for UI).</summary>
        public bool CanEquip(ItemStack stack, EquipSlotType type, int index, out string reason)
        {
            reason = null;
            EquipSlot slot = Get(type, index);

            if (slot == null) reason = $"there is no {type} slot {index + 1}";
            else if (stack == null) reason = "no item";
            else if (!inventory.Contains(stack)) reason = "it is not in the inventory";
            else if (stack.Bag.IsStorage) reason = "it is in storage";
            else if (!stack.Item.CanGoIn(type)) reason = $"{stack.Item.DisplayName} can't go in {type}";
            else if (stack.Item is EquipmentItem equipment && equipment.OnlyOneEquipped && HoldsOther(stack.Item, slot)) reason = $"{stack.Item.DisplayName} is already equipped";
            else if (type == EquipSlotType.Spell && !IsInSpellSlot(stack)
                     && AttunementUsed - CostOf(slot.Item) + CostOf(stack) > AttunementCapacity) reason = "not enough attunement";

            return reason == null;
        }

        public bool Equip(ItemStack stack, EquipSlotType type, int index) => Equip(stack, type, index, out _);

        /// <summary>
        /// Puts a stack in a slot, replacing what was there. A stack already in another slot moves. In a cycled type
        /// whose selected slot is empty, the new slot becomes the selected one.
        /// </summary>
        public bool Equip(ItemStack stack, EquipSlotType type, int index, out string reason)
        {
            if (!CanEquip(stack, type, index, out reason)) return false;

            EquipSlot slot = Get(type, index);
            if (slot.Item == stack) return true;

            EquipSlot previous = SlotOf(stack);
            if (previous != null) SetSlot(previous, null);

            SetSlot(slot, stack);
            if (IsCycled(type) && SelectedSlot(type).IsEmpty) SetSelected(type, index);

            Refresh();
            return true;
        }

        /// <summary>
        /// Equips a stack wherever it fits: the first empty slot of the first type it can go in, else (when every
        /// such slot is full) replacing the selected one, or the first. For "equip" buttons and picking things up.
        /// </summary>
        public bool EquipAnywhere(ItemStack stack)
        {
            if (stack == null || IsEquipped(stack)) return IsEquipped(stack);

            EquipSlot fallback = null;
            foreach (EquipSlotType type in AllTypes)
            {
                if (!stack.Item.CanGoIn(type)) continue;

                foreach (EquipSlot slot in slots[type])
                {
                    if (slot.IsEmpty && CanEquip(stack, type, slot.Index, out _)) return Equip(stack, type, slot.Index);
                }

                if (fallback == null)
                {
                    EquipSlot candidate = IsCycled(type) ? SelectedSlot(type) : Get(type, 0);
                    if (candidate != null && CanEquip(stack, type, candidate.Index, out _)) fallback = candidate;
                }
            }

            return fallback != null && Equip(stack, fallback.Type, fallback.Index);
        }

        public void Unequip(EquipSlotType type, int index)
        {
            EquipSlot slot = Get(type, index);
            if (slot == null || slot.IsEmpty) return;

            SetSlot(slot, null);
            Refresh();
        }

        public void Unequip(ItemStack stack)
        {
            EquipSlot slot = SlotOf(stack);
            if (slot != null) Unequip(slot.Type, slot.Index);
        }

        // ---- Selecting ----

        /// <summary>
        /// Puts both hands' weapons away while <paramref name="source"/> wants them away (carrying something, a
        /// cutscene, climbing). Several sources can stow at once; the weapons come back when the last one unstows.
        /// Not synced and not saved: on a networked character, call it on every copy from state they all share.
        /// </summary>
        public void StowHands(object source)
        {
            if (source == null || !stowers.Add(source) || stowers.Count != 1) return;
            HandsStowedChanged?.Invoke(true);
            Refresh();
        }

        /// <summary>Stops <paramref name="source"/> keeping the hands away (see StowHands).</summary>
        public void UnstowHands(object source)
        {
            if (source == null || !stowers.Remove(source) || stowers.Count != 0) return;
            HandsStowedChanged?.Invoke(false);
            Refresh();
        }

        /// <summary>Selects a slot of a cycled type (a hand, quick items, spells), even an empty one.</summary>
        public void Select(EquipSlotType type, int index)
        {
            if (!IsCycled(type) || Get(type, index) == null || Selected(type) == index) return;

            SetSelected(type, index);
            Refresh();
        }

        /// <summary>
        /// Selects the next (direction 1) or previous (-1) usable slot of a cycled type, skipping empty ones, like
        /// switching weapons on the d-pad. Returns false if there is nothing else to switch to.
        /// </summary>
        public bool Cycle(EquipSlotType type, int direction = 1)
        {
            if (!IsCycled(type)) return false;

            EquipSlot[] array = slots[type];
            int count = array.Length;
            if (count == 0) return false;

            int current = Selected(type);
            int step = direction < 0 ? -1 : 1;
            for (int i = 1; i < count; i++)
            {
                int index = ((current + step * i) % count + count) % count;
                if (!IsUsable(array[index])) continue;

                Select(type, index);
                return true;
            }

            return false;
        }

        /// <summary>Uses the selected quick item. Returns false if there is none or it could not be used.</summary>
        public bool UseQuickItem()
        {
            ItemStack stack = SelectedQuickItem;
            if (stack == null || !(stack.Item is ConsumableItem consumable)) return false;
            if (!consumable.Use(gameObject)) return false;

            if (consumable.ConsumedOnUse) inventory.Remove(stack, 1);
            return true;
        }

        // ---- Saving ----

        public EquipmentSnapshot Capture()
        {
            var snapshot = new EquipmentSnapshot();
            foreach (EquipSlotType type in AllTypes)
            {
                foreach (EquipSlot slot in slots[type])
                {
                    if (slot.IsEmpty) continue;
                    snapshot.slots.Add(new EquipmentSnapshot.Slot { type = type, index = slot.Index, instance = slot.Item.InstanceId });
                }

                if (IsCycled(type)) snapshot.selected.Add(new EquipmentSnapshot.Selection { type = type, index = Selected(type) });
            }
            return snapshot;
        }

        /// <summary>Applies saved equipment. Restore the Inventory first: slots find their stacks by instance id.</summary>
        public void Restore(EquipmentSnapshot snapshot)
        {
            if (snapshot == null) return;

            foreach (EquipSlot[] array in slots.Values)
            {
                foreach (EquipSlot slot in array)
                {
                    if (!slot.IsEmpty) SetSlot(slot, null);
                }
            }

            foreach (EquipmentSnapshot.Slot saved in snapshot.slots)
            {
                ItemStack stack = inventory.Find(saved.instance);
                if (stack == null || !CanEquip(stack, saved.type, saved.index, out string reason))
                {
                    Debug.LogWarning($"{name}: saved {saved.type} {saved.index + 1} could not be equipped again.", this);
                    continue;
                }

                SetSlot(Get(saved.type, saved.index), stack);
            }

            foreach (EquipmentSnapshot.Selection saved in snapshot.selected)
            {
                if (IsCycled(saved.type) && Get(saved.type, saved.index) != null) SetSelected(saved.type, saved.index);
            }

            Refresh();
        }

        /// <summary>
        /// Makes the slots and selections match a snapshot, changing only the slots that differ, without the checks
        /// Equip makes: for a copy kept in step with equipment somewhere else (a networked player). Mirror the
        /// Inventory first. What is in effect then changes by the difference only.
        /// </summary>
        public void Mirror(EquipmentSnapshot snapshot)
        {
            if (snapshot == null) return;

            var wantedSlots = new Dictionary<EquipSlot, ItemStack>();
            foreach (EquipmentSnapshot.Slot saved in snapshot.slots)
            {
                EquipSlot slot = Get(saved.type, saved.index);
                ItemStack stack = inventory.Find(saved.instance);
                if (slot != null && stack != null) wantedSlots[slot] = stack;
            }

            foreach (EquipSlot[] array in slots.Values)
            {
                foreach (EquipSlot slot in array)
                {
                    wantedSlots.TryGetValue(slot, out ItemStack stack);
                    if (slot.Item != stack) SetSlot(slot, stack);
                }
            }

            foreach (EquipmentSnapshot.Selection saved in snapshot.selected)
            {
                if (IsCycled(saved.type) && Get(saved.type, saved.index) != null && Selected(saved.type) != saved.index)
                    SetSelected(saved.type, saved.index);
            }

            Refresh();
        }

        // ---- Inside ----

        void SetSlot(EquipSlot slot, ItemStack stack)
        {
            slot.Item = stack;
            SlotChanged?.Invoke(slot);
        }

        void SetSelected(EquipSlotType type, int index)
        {
            selected[type] = index;
            SelectionChanged?.Invoke(type, index);
        }

        void HandleStackChanged(ItemStack stack, int previousCount)
        {
            // Gone from the inventory, or put in storage: it can't stay equipped.
            if (stack.Count > 0 && stack.Bag != null && !stack.Bag.IsStorage) return;

            EquipSlot slot = SlotOf(stack);
            if (slot == null) return;

            SetSlot(slot, null);
            Refresh();
        }

        void HandleStatChanged(StatDefinition stat, float previous, float current)
        {
            // Less attunement can put the selected spell out of reach; more can bring it back.
            if (stat == attunementStat) Refresh();
        }

        /// <summary>Works out what should be in effect and applies the difference.</summary>
        void Refresh()
        {
            if (!started) return;
            if (refreshing)
            {
                refreshQueued = true; // changed from inside something this set off: go round again
                return;
            }

            refreshing = true;
            do
            {
                refreshQueued = false;

                wanted.Clear();
                AddAll(EquipSlotType.Head);
                AddAll(EquipSlotType.Chest);
                AddAll(EquipSlotType.Hands);
                AddAll(EquipSlotType.Legs);
                AddAll(EquipSlotType.Ring);

                heldWeapon[0] = HandsStowed ? null : SelectedSlot(EquipSlotType.RightHand)?.Item;
                heldWeapon[1] = HandsStowed ? null : SelectedSlot(EquipSlotType.LeftHand)?.Item;
                if (heldWeapon[0] != null) wanted.Add((heldWeapon[0], EquipSlotType.RightHand));
                if (heldWeapon[1] != null) wanted.Add((heldWeapon[1], EquipSlotType.LeftHand));

                ItemStack spell = SelectedSpell;
                if (spell != null) wanted.Add((spell, null));

                for (int i = applied.Count - 1; i >= 0; i--)
                {
                    if (wanted.Contains(applied[i])) continue;
                    RemoveEffects(applied[i].stack, applied[i].hand);
                    applied.RemoveAt(i);
                }

                foreach ((ItemStack stack, object hand) entry in wanted)
                {
                    if (applied.Contains(entry)) continue;
                    ApplyEffects(entry.stack, entry.hand);
                    applied.Add(entry);
                }

                ReportHeld(0);
                ReportHeld(1);
                UpdateWeight();
            } while (refreshQueued);
            refreshing = false;
        }

        void AddAll(EquipSlotType type)
        {
            foreach (EquipSlot slot in slots[type])
            {
                if (!slot.IsEmpty) wanted.Add((slot.Item, null));
            }
        }

        /// <param name="hand">For a held weapon, its hand (EquipSlotType.RightHand or LeftHand), else null.</param>
        void ApplyEffects(ItemStack stack, object hand)
        {
            if (!(stack.Item is EquipmentItem item)) return;

            // One weapon held in both hands is applied twice; its modifiers' source is the stack either way, so they
            // are only added for the first hand (and removed with the last, see RemoveEffects).
            if (stats != null && !AppliedElsewhere(stack, hand)) stats.AddModifiers(item.Modifiers, stack);
            foreach (GameplayTag tag in item.Tags) tags.Add(tag);

            var context = new EquipContext(this, stack, hand as EquipSlotType?);
            foreach (EquipEffect effect in item.Effects)
            {
                if (effect == null) continue;
                try { effect.Apply(context); }
                catch (Exception e) { Debug.LogException(e, this); }
            }
        }

        void RemoveEffects(ItemStack stack, object hand)
        {
            if (!(stack.Item is EquipmentItem item)) return;

            var context = new EquipContext(this, stack, hand as EquipSlotType?);
            for (int i = item.Effects.Count - 1; i >= 0; i--)
            {
                EquipEffect effect = item.Effects[i];
                if (effect == null) continue;
                try { effect.Remove(context); }
                catch (Exception e) { Debug.LogException(e, this); }
            }

            foreach (GameplayTag tag in item.Tags) tags.Remove(tag);
            if (stats != null && !AppliedElsewhere(stack, hand)) stats.RemoveModifiers(stack);
        }

        /// <summary>Is the stack also applied for another hand right now?</summary>
        bool AppliedElsewhere(ItemStack stack, object hand)
        {
            foreach ((ItemStack stack, object hand) entry in applied)
            {
                if (entry.stack == stack && !Equals(entry.hand, hand)) return true;
            }
            return false;
        }

        void ReportHeld(int index)
        {
            ItemStack stack = heldWeapon[index];
            if (reported[index] && stack == reportedWeapon[index]) return;

            reportedWeapon[index] = stack;
            reported[index] = true;
            HeldChanged?.Invoke(index == 0 ? EquipSlotType.RightHand : EquipSlotType.LeftHand, stack);
        }

        void UpdateWeight()
        {
            float total = 0f;
            foreach (EquipSlot[] array in slots.Values)
            {
                foreach (EquipSlot slot in array)
                {
                    if (slot.Item?.Item is EquipmentItem item) total += item.Weight;
                }
            }

            if (Mathf.Approximately(total, EquippedWeight) && weightModifier.stat == equipWeightStat) return;
            EquippedWeight = total;

            if (stats == null) return;
            stats.RemoveModifier(weightModifier, this);
            weightModifier.stat = equipWeightStat;
            weightModifier.value = total;
            if (equipWeightStat != null) stats.AddModifier(weightModifier, this);
        }

        bool HoldsOther(ItemDefinition item, EquipSlot except)
        {
            foreach (EquipSlot[] array in slots.Values)
            {
                foreach (EquipSlot slot in array)
                {
                    if (slot != except && slot.Item != null && slot.Item.Item == item) return true;
                }
            }
            return false;
        }

        bool IsInSpellSlot(ItemStack stack)
        {
            EquipSlot slot = SlotOf(stack);
            return slot != null && slot.Type == EquipSlotType.Spell;
        }

        static int CostOf(ItemStack stack) => stack != null && stack.Item is SpellItem spell ? spell.AttunementCost : 0;

        ItemStack FindUnequipped(ItemDefinition item)
        {
            var found = new List<ItemStack>();
            inventory.FindAll(item, found);
            foreach (ItemStack stack in found)
            {
                if (!IsEquipped(stack)) return stack;
            }
            return null;
        }
    }
}
