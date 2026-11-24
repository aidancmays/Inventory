using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && INVENTORY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace InventorySystem
{
    /// <summary>
    /// A debug window for trying an inventory out before there is any UI (F6 by default; editor and development
    /// builds only). Add it to the character with the Inventory.
    ///  - Equipment: every slot. Click a filled slot to unequip it; ‹ › cycle a hand, quick items or spells;
    ///    the selected slot is marked ▸ (greyed when a spell is beyond the attunement). Use uses the quick item.
    ///  - Bags: each bag with its spaces. Click an item to equip it (or use it, for a consumable in no slot);
    ///    − drops one; → moves it to the next bag (into storage and back).
    ///  - Give: one button per item in the Inventory's Item Database.
    /// A project with its own debug overlay can call <see cref="DrawControls"/> from it instead.
    /// </summary>
    [AddComponentMenu("Inventory/Inventory Debug Panel")]
    public class InventoryDebugPanel : MonoBehaviour
    {
        [Tooltip("Empty = the one on this GameObject or a parent.")]
        [SerializeField] Inventory inventory;

#if ENABLE_INPUT_SYSTEM && INVENTORY_INPUT_SYSTEM
        [SerializeField] Key toggleKey = Key.F6;
#else
        [SerializeField] KeyCode toggleKey = KeyCode.F6;
#endif
        [SerializeField] bool visible = true;
        [SerializeField] float width = 380f;

        public bool Visible { get => visible; set => visible = value; }

        static GUIStyle label, dim, small;
        static readonly Dictionary<Inventory, bool> showGive = new Dictionary<Inventory, bool>();
        static string message;
        static float messageTime;
        Rect window;

        void Awake()
        {
            if (!Debug.isDebugBuild) enabled = false;
            if (inventory == null) inventory = GetComponentInParent<Inventory>();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM && INVENTORY_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) visible = !visible;
#else
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
#endif
        }

        void OnGUI()
        {
            if (!visible) return;
            if (window.width <= 0f) window = new Rect(10f, 10f, width, 10f);
            window = GUILayout.Window(GetInstanceID(), window, id =>
            {
                DrawControls(inventory);
                GUI.DragWindow();
            }, "Inventory", GUILayout.Width(width));
        }

        /// <summary>The panel's contents (IMGUI layout), for drawing inside another debug overlay.</summary>
        public static void DrawControls(Inventory inventory)
        {
            EnsureStyles();
            if (inventory == null)
            {
                GUILayout.Label("No Inventory found.", dim);
                return;
            }

            Equipment equipment = inventory.GetComponent<Equipment>();
            if (equipment != null) DrawEquipment(equipment);

            GUILayout.Space(6f);
            DrawBags(inventory, equipment);

            GUILayout.Space(6f);
            DrawGive(inventory);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime - messageTime < 4f)
            {
                GUILayout.Space(4f);
                GUILayout.Label("<color=#ffd54f>" + message + "</color>", label);
            }
        }

        static void DrawEquipment(Equipment equipment)
        {
            GUILayout.Label("<b>Equipment</b>", label);

            DrawSlots(equipment, EquipSlotType.RightHand, "R hand");
            DrawSlots(equipment, EquipSlotType.LeftHand, "L hand");
            DrawSlots(equipment, EquipSlotType.Head, "Head");
            DrawSlots(equipment, EquipSlotType.Chest, "Chest");
            DrawSlots(equipment, EquipSlotType.Hands, "Hands");
            DrawSlots(equipment, EquipSlotType.Legs, "Legs");
            DrawSlots(equipment, EquipSlotType.Ring, "Rings");
            DrawSlots(equipment, EquipSlotType.QuickItem, "Quick");
            DrawSlots(equipment, EquipSlotType.Spell, "Spells");

            string attunement = equipment.AttunementCapacity == int.MaxValue ? "-" : equipment.AttunementCapacity.ToString();
            GUILayout.Label($"Attunement {equipment.AttunementUsed} / {attunement}    Equipped weight {equipment.EquippedWeight:0.##}", dim);
        }

        static void DrawSlots(Equipment equipment, EquipSlotType type, string heading)
        {
            IReadOnlyList<EquipSlot> slots = equipment.Slots(type);
            if (slots.Count == 0) return;

            bool cycled = Equipment.IsCycled(type);
            int selected = equipment.Selected(type);

            GUILayout.BeginHorizontal();
            GUILayout.Label(heading, label, GUILayout.Width(52f));

            IInventoryRequests requests = InventoryRequests.For(equipment.Inventory);
            if (cycled && GUILayout.Button("‹", small, GUILayout.Width(18f))) requests.Cycle(type, -1);

            for (int i = 0; i < slots.Count; i++)
            {
                EquipSlot slot = slots[i];
                string text = slot.IsEmpty ? "·" : Short(slot.Item);
                if (cycled && i == selected) text = "▸" + text;
                if (type == EquipSlotType.Spell && !slot.IsEmpty && !equipment.IsUsable(slot)) text = "<color=#888888>" + text + "</color>";

                if (GUILayout.Button(text, small) && !slot.IsEmpty) requests.Unequip(type, i);
            }

            if (cycled && GUILayout.Button("›", small, GUILayout.Width(18f))) requests.Cycle(type, 1);
            if (type == EquipSlotType.QuickItem && GUILayout.Button("Use", small, GUILayout.Width(36f)) && !requests.UseQuickItem())
                Say("Nothing to use.");

            GUILayout.EndHorizontal();
        }

        static void DrawBags(Inventory inventory, Equipment equipment)
        {
            IInventoryRequests requests = InventoryRequests.For(inventory);
            IReadOnlyList<InventoryBag> bags = inventory.Bags;
            for (int b = 0; b < bags.Count; b++)
            {
                InventoryBag bag = bags[b];
                string spaces = bag.Limited ? $"{bag.UsedSpaces} / {bag.Spaces}" : $"{bag.UsedSpaces}, unlimited";
                string kind = bag.IsStorage ? "  <color=#999999>storage</color>" : "";
                GUILayout.Label($"<b>{bag.DisplayName}</b>  <color=#999999>{spaces}</color>{kind}", label);

                if (bag.Stacks.Count == 0)
                {
                    GUILayout.Label("  empty", dim);
                    continue;
                }

                InventoryBag next = bags.Count > 1 ? bags[(b + 1) % bags.Count] : null;

                // Copied: clicking changes the bag.
                var stacks = new List<ItemStack>(bag.Stacks);
                foreach (ItemStack stack in stacks)
                {
                    GUILayout.BeginHorizontal();

                    string text = stack.ToString();
                    if (equipment != null && equipment.IsEquipped(stack)) text = "<color=#a5d6a7>" + text + "  [E]</color>";

                    if (GUILayout.Button(text, small)) Activate(requests, equipment, stack);
                    if (GUILayout.Button("−", small, GUILayout.Width(20f))) requests.Remove(stack, 1);
                    if (next != null && GUILayout.Button("→", small, GUILayout.Width(20f)) && requests.Move(stack, next) == 0)
                        Say($"{next.DisplayName} can't take {stack.Item.DisplayName}.");

                    GUILayout.EndHorizontal();
                }
            }
        }

        static void DrawGive(Inventory inventory)
        {
            ItemDatabase database = inventory.Database;
            if (database == null)
            {
                GUILayout.Label("Give: set an Item Database on the Inventory.", dim);
                return;
            }

            bool open;
            showGive.TryGetValue(inventory, out open);
            open = GUILayout.Toggle(open, " Give items", label);
            showGive[inventory] = open;
            if (!open) return;

            foreach (ItemDefinition item in database.Items)
            {
                if (item == null) continue;
                if (GUILayout.Button("+ " + item.DisplayName, small) && InventoryRequests.For(inventory).Give(item) == 0) Say($"No room for {item.DisplayName}.");
            }
        }

        static void Activate(IInventoryRequests requests, Equipment equipment, ItemStack stack)
        {
            if (stack.Bag.IsStorage)
            {
                Say("Items in storage can't be equipped or used.");
                return;
            }

            if (equipment != null)
            {
                if (equipment.IsEquipped(stack))
                {
                    Say($"{stack.Item.DisplayName} is equipped; click its slot to unequip.");
                    return;
                }

                if (requests.EquipAnywhere(stack)) return;
            }

            if (stack.Item is ConsumableItem && requests.Use(stack)) return;

            Say($"{stack.Item.DisplayName} can't be equipped.");
        }

        static void Say(string text)
        {
            message = text;
            messageTime = Time.unscaledTime;
        }

        static string Short(ItemStack stack)
        {
            string text = stack.Item.DisplayName;
            if (text.Length > 12) text = text.Substring(0, 11) + "…";
            return stack.Count > 1 ? $"{text} x{stack.Count}" : text;
        }

        static void EnsureStyles()
        {
            if (label != null) return;

            label = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = false };
            label.margin = new RectOffset(2, 2, 0, 0);
            label.padding = new RectOffset(0, 0, 1, 1);

            dim = new GUIStyle(label);
            dim.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            small = new GUIStyle(GUI.skin.button) { fontSize = 11, richText = true, wordWrap = false };
            small.padding = new RectOffset(4, 4, 1, 1);
            small.margin = new RectOffset(1, 1, 1, 1);
        }
    }
}
