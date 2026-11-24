using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Everything a character carries, split into bags by item type (see InventoryBag): each bag unlimited or with
    /// a number of spaces. Put it on the character root; Equipment uses it.
    ///
    /// Adding an item puts it in the first bag that takes it (and isn't storage): first into stacks of it already
    /// there, then into free spaces; what still does not fit follows the bag's Overflow To. Add returns how many went in.
    /// </summary>
    [DefaultExecutionOrder(-90)] // after StatSheet and TagHolder, before Equipment and anything that reads the contents
    [DisallowMultipleComponent]
    public class Inventory : MonoBehaviour
    {
        [Serializable]
        public struct ItemAmount
        {
            public ItemDefinition item;
            [Min(1)] public int count;
        }

        [Tooltip("The bags, in the order items look for a home: put specific bags above catch-alls.")]
        [SerializeField] List<InventoryBag> bags = new List<InventoryBag>
        {
            new InventoryBag("Items"),
        };

        [Tooltip("Carried at the start.")]
        [SerializeField] List<ItemAmount> startingItems = new List<ItemAmount>();

        [Tooltip("Every item in the game, so saved items can be found again by id. Only needed for Restore.")]
        [SerializeField] ItemDatabase database;

        /// <summary>
        /// (stack, its previous count). Raised when a stack is created (previous 0), grows, shrinks, is emptied
        /// (Count 0, Bag null), is moved to another bag (Count unchanged; read Bag) or to another place in its bag
        /// (Count unchanged; see Reorder).
        /// </summary>
        public event Action<ItemStack, int> StackChanged;

        public IReadOnlyList<InventoryBag> Bags => bags;
        /// <summary>Every item in the game, for Restore (and debug panels). Set it here or in the inspector.</summary>
        public ItemDatabase Database
        {
            get => database;
            set => database = value;
        }

        void Awake()
        {
            foreach (ItemAmount entry in startingItems)
            {
                if (entry.item == null) continue;

                int added = Add(entry.item, Mathf.Max(1, entry.count));
                if (added < entry.count) Debug.LogWarning($"{name}: only {added} of {entry.count} starting '{entry.item.DisplayName}' fit.", this);
            }
        }

        void OnValidate()
        {
            var ids = new HashSet<string>();
            foreach (InventoryBag bag in bags)
            {
                if (bag != null && !ids.Add(bag.Id)) Debug.LogWarning($"{name}: two inventory bags have the id '{bag.Id}'.", this);
            }
        }

        // ---- Finding ----

        public InventoryBag GetBag(string id)
        {
            foreach (InventoryBag bag in bags)
            {
                if (bag.Id == id) return bag;
            }
            return null;
        }

        /// <summary>The bag an item goes in when picked up: the first non-storage bag that takes it. Null if none does.</summary>
        public InventoryBag HomeOf(ItemDefinition item)
        {
            foreach (InventoryBag bag in bags)
            {
                if (!bag.IsStorage && bag.Accepts(item)) return bag;
            }
            return null;
        }

        /// <summary>How many of the item are carried (storage bags not counted unless asked).</summary>
        public int Count(ItemDefinition item, bool includeStorage = false)
        {
            int count = 0;
            foreach (InventoryBag bag in bags)
            {
                if (includeStorage || !bag.IsStorage) count += bag.Count(item);
            }
            return count;
        }

        public bool Has(ItemDefinition item, int count = 1) => Count(item) >= count;

        /// <summary>Is the stack in this inventory (in any bag)?</summary>
        public bool Contains(ItemStack stack) => stack != null && stack.Count > 0 && stack.Bag != null && bags.Contains(stack.Bag);

        /// <summary>The stack with that instance id, or null.</summary>
        public ItemStack Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;

            foreach (InventoryBag bag in bags)
            {
                foreach (ItemStack stack in bag.List)
                {
                    if (stack.InstanceId == instanceId) return stack;
                }
            }
            return null;
        }

        /// <summary>Adds every carried stack of the item to results (not storage).</summary>
        public void FindAll(ItemDefinition item, List<ItemStack> results)
        {
            foreach (InventoryBag bag in bags)
            {
                if (bag.IsStorage) continue;
                foreach (ItemStack stack in bag.List)
                {
                    if (stack.Item == item) results.Add(stack);
                }
            }
        }

        /// <summary>How many more of the item would fit, counting overflow bags.</summary>
        public int RoomFor(ItemDefinition item)
        {
            long room = 0;
            var visited = new HashSet<InventoryBag>();
            for (InventoryBag bag = HomeOf(item); bag != null && visited.Add(bag); bag = NextOverflow(bag, item))
            {
                room += bag.RoomFor(item);
                if (room >= int.MaxValue) return int.MaxValue;
            }
            return (int)room;
        }

        // ---- Changing ----

        /// <summary>Picks up items. Returns how many went in (less than count when the bags are full).</summary>
        public int Add(ItemDefinition item, int count = 1)
        {
            if (item == null || count <= 0) return 0;

            InventoryBag bag = HomeOf(item);
            if (bag == null)
            {
                Debug.LogWarning($"{name}: no inventory bag takes '{item.DisplayName}' (category {(item.Category != null ? item.Category.name : "none")}).", this);
                return 0;
            }

            int left = count;
            var visited = new HashSet<InventoryBag>();
            while (bag != null && left > 0 && visited.Add(bag))
            {
                left -= Put(bag, item, left);
                bag = NextOverflow(bag, item);
            }

            return count - left;
        }

        /// <summary>Takes up to count from a stack. Returns how many were taken.</summary>
        public int Remove(ItemStack stack, int count = 1)
        {
            if (!Contains(stack) || count <= 0) return 0;

            int previous = stack.Count;
            int taken = Mathf.Min(count, previous);
            stack.Count = previous - taken;

            if (stack.Count == 0)
            {
                stack.Bag.List.Remove(stack);
                stack.Bag = null;
            }

            StackChanged?.Invoke(stack, previous);
            return taken;
        }

        /// <summary>Takes up to count of an item, from carried stacks (not storage), smallest stacks first. Returns how many were taken.</summary>
        public int Remove(ItemDefinition item, int count = 1)
        {
            var found = new List<ItemStack>();
            FindAll(item, found);
            found.Sort((a, b) => a.Count.CompareTo(b.Count));

            int taken = 0;
            foreach (ItemStack stack in found)
            {
                if (taken >= count) break;
                taken += Remove(stack, count - taken);
            }
            return taken;
        }

        /// <summary>
        /// Moves up to count from a stack into another bag of this inventory (into storage and back out). The bag must
        /// take the item; storage bags take anything moved in. A whole stack that does not stack with others keeps
        /// its identity (its InstanceId). Returns how many moved.
        /// </summary>
        public int Move(ItemStack stack, InventoryBag to, int count = int.MaxValue)
        {
            if (!Contains(stack) || to == null || to == stack.Bag || !bags.Contains(to)) return 0;
            if (!to.IsStorage && !to.Accepts(stack.Item)) return 0;

            count = Mathf.Min(count, stack.Count);
            if (count <= 0) return 0;

            if (count == stack.Count && !MergesInto(stack.Item, to))
            {
                if (to.IsFull) return 0;

                int previous = stack.Count;
                stack.Bag.List.Remove(stack);
                stack.Bag = to;
                to.List.Add(stack);
                StackChanged?.Invoke(stack, previous);
                return count;
            }

            int moved = Put(to, stack.Item, count);
            Remove(stack, moved);
            return moved;
        }

        /// <summary>
        /// Moves a stack to another place in its own bag: it ends up at index (clamped to the bag, so anything past
        /// the end puts it last) and the stacks in between shift along. Returns false if nothing moved.
        /// </summary>
        public bool Reorder(ItemStack stack, int index)
        {
            if (!Contains(stack)) return false;

            List<ItemStack> list = stack.Bag.List;
            int from = list.IndexOf(stack);
            int to = Mathf.Clamp(index, 0, list.Count - 1);
            if (from == to) return false;

            list.RemoveAt(from);
            list.Insert(to, stack);
            StackChanged?.Invoke(stack, stack.Count);
            return true;
        }

        /// <summary>Empties every bag.</summary>
        public void Clear()
        {
            foreach (InventoryBag bag in bags)
            {
                for (int i = bag.List.Count - 1; i >= 0; i--) Remove(bag.List[i], int.MaxValue);
            }
        }

        bool MergesInto(ItemDefinition item, InventoryBag bag)
        {
            if (!item.Stacks) return false;
            foreach (ItemStack other in bag.List)
            {
                if (other.Item == item && other.Room > 0) return true;
            }
            return false;
        }

        InventoryBag NextOverflow(InventoryBag bag, ItemDefinition item)
        {
            if (string.IsNullOrEmpty(bag.OverflowTo)) return null;

            InventoryBag next = GetBag(bag.OverflowTo);
            if (next == null) return null;
            return next.IsStorage || next.Accepts(item) ? next : null;
        }

        /// <summary>Puts as many as fit into one bag, whatever it takes (callers check). Returns how many.</summary>
        int Put(InventoryBag bag, ItemDefinition item, int count)
        {
            int left = count;

            if (item.Stacks)
            {
                foreach (ItemStack stack in bag.List)
                {
                    if (left <= 0) break;
                    if (stack.Item != item || stack.Room <= 0) continue;

                    int previous = stack.Count;
                    int amount = Mathf.Min(left, stack.Room);
                    stack.Count += amount;
                    left -= amount;
                    StackChanged?.Invoke(stack, previous);
                }
            }

            while (left > 0 && !bag.IsFull)
            {
                int amount = Mathf.Min(left, item.StackLimit);
                CreateStack(bag, item, amount, null);
                left -= amount;
            }

            return count - left;
        }

        ItemStack CreateStack(InventoryBag bag, ItemDefinition item, int count, string instanceId)
        {
            var stack = new ItemStack(item, count, instanceId) { Bag = bag };
            bag.List.Add(stack);
            StackChanged?.Invoke(stack, 0);
            return stack;
        }

        // ---- Saving ----

        public InventorySnapshot Capture()
        {
            var snapshot = new InventorySnapshot();
            foreach (InventoryBag bag in bags)
            {
                foreach (ItemStack stack in bag.List)
                {
                    snapshot.stacks.Add(new InventorySnapshot.Stack
                    {
                        bag = bag.Id,
                        item = stack.Item.Id,
                        count = stack.Count,
                        instance = stack.InstanceId,
                    });
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Replaces the contents with saved ones. Items are found by id in the database (the one passed, else this
        /// component's). Restore Equipment after this, since emptying the bags unequips everything.
        /// </summary>
        public void Restore(InventorySnapshot snapshot, ItemDatabase itemDatabase = null)
        {
            if (snapshot == null) return;

            itemDatabase ??= database;
            if (itemDatabase == null)
            {
                Debug.LogError($"{name}: Inventory.Restore needs an Item Database to find items by id.", this);
                return;
            }

            Clear();

            foreach (InventorySnapshot.Stack saved in snapshot.stacks)
            {
                ItemDefinition item = itemDatabase.Find(saved.item);
                if (item == null)
                {
                    Debug.LogWarning($"{name}: saved item '{saved.item}' is not in the item database; skipped.", this);
                    continue;
                }

                InventoryBag bag = GetBag(saved.bag) ?? HomeOf(item);
                if (bag == null)
                {
                    Debug.LogWarning($"{name}: no bag for saved item '{saved.item}'; skipped.", this);
                    continue;
                }

                CreateStack(bag, item, Mathf.Max(1, saved.count), saved.instance);
            }
        }

        /// <summary>
        /// Makes the contents match a snapshot, keeping the stacks that are still there (same instance id) instead of
        /// emptying the bags first like Restore does, so equipment pointing at them stays put and only real changes
        /// raise StackChanged. For a copy kept in step with an inventory somewhere else (a networked player): the
        /// snapshot is taken as it is, without checking bag rules or space. Mirror Equipment after this.
        /// </summary>
        public void Mirror(InventorySnapshot snapshot, ItemDatabase itemDatabase = null)
        {
            if (snapshot == null) return;

            itemDatabase ??= database;
            if (itemDatabase == null)
            {
                Debug.LogError($"{name}: Inventory.Mirror needs an Item Database to find items by id.", this);
                return;
            }

            var order = new Dictionary<ItemStack, int>();
            var changed = new HashSet<ItemStack>();
            foreach (InventorySnapshot.Stack saved in snapshot.stacks)
            {
                ItemDefinition item = itemDatabase.Find(saved.item);
                if (item == null)
                {
                    Debug.LogWarning($"{name}: item '{saved.item}' is not in the item database; skipped.", this);
                    continue;
                }

                InventoryBag bag = GetBag(saved.bag) ?? HomeOf(item);
                if (bag == null) continue;

                int count = Mathf.Max(1, saved.count);
                ItemStack stack = Find(saved.instance);
                if (stack != null && stack.Item == item && !order.ContainsKey(stack))
                {
                    int previous = stack.Count;
                    bool moved = stack.Bag != bag;
                    if (moved)
                    {
                        stack.Bag.List.Remove(stack);
                        stack.Bag = bag;
                        bag.List.Add(stack);
                    }
                    stack.Count = count;
                    if (moved || previous != count)
                    {
                        changed.Add(stack);
                        StackChanged?.Invoke(stack, previous);
                    }
                }
                else
                {
                    stack = CreateStack(bag, item, count, saved.instance);
                    changed.Add(stack);
                }

                order[stack] = order.Count;
            }

            foreach (InventoryBag bag in bags)
            {
                for (int i = bag.List.Count - 1; i >= 0; i--)
                {
                    if (!order.ContainsKey(bag.List[i])) Remove(bag.List[i], int.MaxValue);
                }

                var before = new List<ItemStack>(bag.List);
                bag.List.Sort((a, b) => order[a].CompareTo(order[b]));

                // Only the order changed (Reorder on the original): say so for the stacks that moved.
                for (int i = 0; i < before.Count; i++)
                {
                    ItemStack stack = bag.List[i];
                    if (stack != before[i] && changed.Add(stack)) StackChanged?.Invoke(stack, stack.Count);
                }
            }
        }
    }
}
