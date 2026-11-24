using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace InventorySystem.Netcode
{
    /// <summary>
    /// Netcode for GameObjects: keeps a character's Inventory and Equipment the same on every machine, with the
    /// server's copy the real one. Put it next to Inventory (and Equipment) on a networked character.
    ///
    /// The server changes the contents as usual (pickups, rewards: call Inventory.Add on the server) and every
    /// change is sent to everyone else, whose copies mirror it (Inventory.Mirror, Equipment.Mirror), so the
    /// equipment is in effect on every copy: stat modifiers, tags and EquipEffects, and HeldChanged for the weapon in
    /// each hand. Late joiners get the contents when the character spawns for them.
    ///
    /// The owner asks for changes through this component (it is the character's <see cref="IInventoryRequests"/>,
    /// so <c>InventoryRequests.For(inventory)</c> finds it, and the Inventory Debug Panel uses it); the server checks
    /// and makes them. Code on a client must not change its copy directly: the next update from the server undoes it.
    ///
    /// Consumables do what they do on the owner or on the server (Consumables Used On), whichever has the real stats.
    /// On the owner they take effect at once and the server then takes the item.
    /// </summary>
    [AddComponentMenu("Inventory/Network Inventory")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Inventory))]
    public class NetworkInventory : NetworkBehaviour, IInventoryRequests
    {
        public enum UseOn
        {
            /// <summary>The owner's StatSheet is the real one (owner-authoritative stats).</summary>
            Owner,

            /// <summary>The server's StatSheet is the real one.</summary>
            Server,
        }

        [Tooltip("Where a consumable does what it does: on the owner (when the owner's stats are the real ones) or on the server.")]
        [SerializeField] UseOn consumablesUsedOn = UseOn.Owner;

        [Tooltip("Let the owner add any item from the item database (the debug panel's Give). Development builds always may.")]
        [SerializeField] bool ownerCanGive;

        Inventory inventory;
        Equipment equipment;

        bool dirty;
        State synced;
        bool hasSynced;

        int handled; // server: the last owner request handled
        int sent;    // owner: the last request sent
        readonly List<(int request, string instance)> unconfirmedUses = new List<(int, string)>();

        /// <summary>
        /// (item, count). On the server: items were taken out because the owner asked (thrown away, dropped). Put a
        /// pickup in the world from here.
        /// </summary>
        public event Action<ItemDefinition, int> Dropped;

        public Inventory Inventory => inventory;
        public Equipment Equipment => equipment;

        bool CanGive => ownerCanGive || Debug.isDebugBuild;

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            equipment = GetComponent<Equipment>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                inventory.StackChanged += OnStackChanged;
                if (equipment != null)
                {
                    equipment.SlotChanged += OnSlotChanged;
                    equipment.SelectionChanged += OnSelectionChanged;
                }
                return;
            }

            // The contents come from the server: none of this copy's own starting items or equipment.
            if (equipment != null) equipment.UseStartingEquipment = false;
            Apply(hasSynced ? synced : new State { inventory = new InventorySnapshot() });
            hasSynced = false;
        }

        public override void OnNetworkDespawn()
        {
            inventory.StackChanged -= OnStackChanged;
            if (equipment != null)
            {
                equipment.SlotChanged -= OnSlotChanged;
                equipment.SelectionChanged -= OnSelectionChanged;
            }
            unconfirmedUses.Clear();
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            if (serializer.IsWriter)
            {
                State state = Capture();
                serializer.SerializeValue(ref state);
            }
            else
            {
                serializer.SerializeValue(ref synced);
                hasSynced = true;
            }
        }

        void LateUpdate()
        {
            if (!dirty || !IsSpawned || !IsServer) return;

            dirty = false;
            if (NetworkManager.ConnectedClientsIds.Count > (IsHost ? 1 : 0)) StateRpc(Capture());
        }

        void OnStackChanged(ItemStack stack, int previous) => dirty = true;
        void OnSlotChanged(EquipSlot slot) => dirty = true;
        void OnSelectionChanged(EquipSlotType type, int index) => dirty = true;

        // ---- Requests (IInventoryRequests) ----

        /// <summary>Offline, or on the server: change the contents here and now.</summary>
        bool Direct => !IsSpawned || IsServer;

        bool CanAsk
        {
            get
            {
                if (IsOwner) return true;
                Debug.LogWarning($"{name}: only the owner (or the server) can ask for inventory changes.", this);
                return false;
            }
        }

        public bool Equip(ItemStack stack, EquipSlotType type, int index)
        {
            if (equipment == null) return false;
            if (Direct) return equipment.Equip(stack, type, index);
            if (!CanAsk || !equipment.CanEquip(stack, type, index, out _)) return false;

            EquipRpc(stack.InstanceId, type, index, ++sent);
            return true;
        }

        public bool EquipAnywhere(ItemStack stack)
        {
            if (equipment == null || stack == null) return false;
            if (Direct) return equipment.EquipAnywhere(stack);
            if (!CanAsk || !inventory.Contains(stack) || stack.Bag.IsStorage) return false;
            if (equipment.IsEquipped(stack)) return true;

            EquipAnywhereRpc(stack.InstanceId, ++sent);
            return true;
        }

        public void Unequip(EquipSlotType type, int index)
        {
            if (equipment == null) return;
            if (Direct) equipment.Unequip(type, index);
            else if (CanAsk) UnequipRpc(type, index, ++sent);
        }

        public void Select(EquipSlotType type, int index)
        {
            if (equipment == null) return;
            if (Direct) equipment.Select(type, index);
            else if (CanAsk) SelectRpc(type, index, ++sent);
        }

        public bool Cycle(EquipSlotType type, int direction = 1)
        {
            if (equipment == null) return false;
            if (Direct) return equipment.Cycle(type, direction);
            if (!CanAsk || !HasOtherUsable(type)) return false;

            CycleRpc(type, direction, ++sent);
            return true;
        }

        public bool UseQuickItem()
        {
            if (equipment == null) return false;
            return Use(equipment.SelectedQuickItem);
        }

        public bool Use(ItemStack stack)
        {
            if (Direct) return UseOnServer(stack);
            if (!CanAsk || !InventoryRequests.CanUse(inventory, stack, out ConsumableItem consumable)) return false;

            if (consumablesUsedOn == UseOn.Server)
            {
                UseRpc(stack.InstanceId, ++sent);
                return true;
            }

            // Not yet confirmed uses still count, so a last flask can't be drunk twice before the server answers.
            int unconfirmed = 0;
            foreach ((int request, string instance) use in unconfirmedUses)
            {
                if (use.instance == stack.InstanceId) unconfirmed++;
            }
            if (consumable.ConsumedOnUse && stack.Count - unconfirmed <= 0) return false;

            if (!consumable.Use(gameObject)) return false;

            if (consumable.ConsumedOnUse)
            {
                int request = ++sent;
                unconfirmedUses.Add((request, stack.InstanceId));
                ConsumeRpc(stack.InstanceId, request);
            }
            return true;
        }

        public int Remove(ItemStack stack, int count = 1)
        {
            if (Direct) return Drop(stack, count);
            if (!CanAsk || !inventory.Contains(stack) || count <= 0) return 0;

            RemoveRpc(stack.InstanceId, count, ++sent);
            return Mathf.Min(count, stack.Count);
        }

        public int Move(ItemStack stack, InventoryBag to, int count = int.MaxValue)
        {
            if (Direct) return inventory.Move(stack, to, count);
            if (!CanAsk || !inventory.Contains(stack) || to == null || to == stack.Bag || count <= 0) return 0;
            if (!to.IsStorage && !to.Accepts(stack.Item)) return 0;

            MoveRpc(stack.InstanceId, to.Id, count, ++sent);
            return Mathf.Min(count, stack.Count);
        }

        public int Give(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;
            if (Direct) return inventory.Add(item, count);
            if (!CanAsk || !CanGive) return 0;

            int room = inventory.RoomFor(item);
            if (room <= 0) return 0;

            GiveRpc(item.Id, count, ++sent);
            return Mathf.Min(count, room);
        }

        // ---- On the server ----

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void EquipRpc(string instance, EquipSlotType type, int index, int request)
        {
            ItemStack stack = inventory.Find(instance);
            if (stack != null && equipment != null) equipment.Equip(stack, type, index);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void EquipAnywhereRpc(string instance, int request)
        {
            ItemStack stack = inventory.Find(instance);
            if (stack != null && equipment != null) equipment.EquipAnywhere(stack);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UnequipRpc(EquipSlotType type, int index, int request)
        {
            if (equipment != null) equipment.Unequip(type, index);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void SelectRpc(EquipSlotType type, int index, int request)
        {
            if (equipment != null) equipment.Select(type, index);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void CycleRpc(EquipSlotType type, int direction, int request)
        {
            if (equipment != null) equipment.Cycle(type, direction);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void UseRpc(string instance, int request)
        {
            ItemStack stack = inventory.Find(instance);
            if (stack != null) InventoryRequests.UseHere(inventory, stack);
            Handled(request);
        }

        /// <summary>The owner used it already: take one.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void ConsumeRpc(string instance, int request)
        {
            ItemStack stack = inventory.Find(instance);
            if (InventoryRequests.CanUse(inventory, stack, out ConsumableItem consumable) && consumable.ConsumedOnUse)
                inventory.Remove(stack, 1);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void RemoveRpc(string instance, int count, int request)
        {
            Drop(inventory.Find(instance), count);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void MoveRpc(string instance, string bag, int count, int request)
        {
            ItemStack stack = inventory.Find(instance);
            InventoryBag to = inventory.GetBag(bag);
            if (stack != null && to != null) inventory.Move(stack, to, count);
            Handled(request);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        void GiveRpc(string itemId, int count, int request)
        {
            if (CanGive)
            {
                ItemDefinition item = inventory.Database != null ? inventory.Database.Find(itemId) : null;
                if (item != null) inventory.Add(item, count);
                else Debug.LogWarning($"{name}: can't give '{itemId}': it is not in the Inventory's Item Database.", this);
            }
            Handled(request);
        }

        void Handled(int request)
        {
            if (request > handled) handled = request;
            dirty = true; // even when nothing changed, so the owner hears the request was answered
        }

        int Drop(ItemStack stack, int count)
        {
            if (stack == null) return 0;

            ItemDefinition item = stack.Item;
            int taken = inventory.Remove(stack, count);
            if (taken > 0) Dropped?.Invoke(item, taken);
            return taken;
        }

        bool UseOnServer(ItemStack stack)
        {
            if (consumablesUsedOn == UseOn.Server || IsOwner || !IsSpawned) return InventoryRequests.UseHere(inventory, stack);

            // The owner has the real stats: take the item here and have the owner do what it does.
            if (!InventoryRequests.CanUse(inventory, stack, out ConsumableItem consumable)) return false;
            if (consumable.ConsumedOnUse) inventory.Remove(stack, 1);
            UseOnOwnerRpc(consumable.Id);
            return true;
        }

        [Rpc(SendTo.Owner)]
        void UseOnOwnerRpc(string itemId)
        {
            if (inventory.Database != null && inventory.Database.Find(itemId) is ConsumableItem consumable) consumable.Use(gameObject);
        }

        // ---- Everyone else ----

        [Rpc(SendTo.NotServer)]
        void StateRpc(State state) => Apply(state);

        State Capture()
        {
            return new State
            {
                handled = handled,
                inventory = inventory.Capture(),
                equipment = equipment != null ? equipment.Capture() : null,
            };
        }

        void Apply(State state)
        {
            inventory.Mirror(state.inventory ?? new InventorySnapshot(), inventory.Database);
            if (equipment != null) equipment.Mirror(state.equipment ?? new EquipmentSnapshot());

            unconfirmedUses.RemoveAll(use => use.request <= state.handled);
        }

        bool HasOtherUsable(EquipSlotType type)
        {
            if (!Equipment.IsCycled(type)) return false;

            int current = equipment.Selected(type);
            IReadOnlyList<EquipSlot> slots = equipment.Slots(type);
            for (int i = 0; i < slots.Count; i++)
            {
                if (i != current && equipment.IsUsable(slots[i])) return true;
            }
            return false;
        }

        /// <summary>The whole contents, as sent over the network.</summary>
        struct State : INetworkSerializable
        {
            public int handled;
            public InventorySnapshot inventory;
            public EquipmentSnapshot equipment;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                bool reading = serializer.IsReader;
                serializer.SerializeValue(ref handled);

                if (reading) inventory = new InventorySnapshot();
                int stacks = reading ? 0 : inventory.stacks.Count;
                serializer.SerializeValue(ref stacks);
                for (int i = 0; i < stacks; i++)
                {
                    InventorySnapshot.Stack stack = reading ? default : inventory.stacks[i];
                    string bag = stack.bag ?? "", item = stack.item ?? "", instance = stack.instance ?? "";
                    int count = stack.count;
                    serializer.SerializeValue(ref bag);
                    serializer.SerializeValue(ref item);
                    serializer.SerializeValue(ref count);
                    serializer.SerializeValue(ref instance);
                    if (reading) inventory.stacks.Add(new InventorySnapshot.Stack { bag = bag, item = item, count = count, instance = instance });
                }

                bool hasEquipment = equipment != null;
                serializer.SerializeValue(ref hasEquipment);
                if (!hasEquipment)
                {
                    equipment = null;
                    return;
                }

                if (reading) equipment = new EquipmentSnapshot();
                int slots = reading ? 0 : equipment.slots.Count;
                serializer.SerializeValue(ref slots);
                for (int i = 0; i < slots; i++)
                {
                    EquipmentSnapshot.Slot slot = reading ? default : equipment.slots[i];
                    int type = (int)slot.type, index = slot.index;
                    string instance = slot.instance ?? "";
                    serializer.SerializeValue(ref type);
                    serializer.SerializeValue(ref index);
                    serializer.SerializeValue(ref instance);
                    if (reading) equipment.slots.Add(new EquipmentSnapshot.Slot { type = (EquipSlotType)type, index = index, instance = instance });
                }

                int selected = reading ? 0 : equipment.selected.Count;
                serializer.SerializeValue(ref selected);
                for (int i = 0; i < selected; i++)
                {
                    EquipmentSnapshot.Selection selection = reading ? default : equipment.selected[i];
                    int type = (int)selection.type, index = selection.index;
                    serializer.SerializeValue(ref type);
                    serializer.SerializeValue(ref index);
                    if (reading) equipment.selected.Add(new EquipmentSnapshot.Selection { type = (EquipSlotType)type, index = index });
                }
            }
        }
    }
}
