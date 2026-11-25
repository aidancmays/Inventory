using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace InventorySystem.Tests
{
    /// <summary>Equipment with Equipped Items Leave The Bag: slots are their own places, not references into a bag.</summary>
    public class EquippedLeaveTheBagTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        readonly List<Object> made = new List<Object>();
        ItemDatabase database;
        WeaponItem sword, axe, mace;
        RingItem ring;
        ConsumableItem flask;

        [SetUp]
        public void SetUp()
        {
            sword = Make<WeaponItem>("Test Sword");
            axe = Make<WeaponItem>("Test Axe");
            mace = Make<WeaponItem>("Test Mace");
            ring = Make<RingItem>("Test Ring");
            flask = Make<ConsumableItem>("Test Flask");
            typeof(ItemDefinition).GetField("maxStack", Private).SetValue(flask, 99);

            database = Make<ItemDatabase>("Test Database");
            var items = (List<ItemDefinition>)typeof(ItemDatabase).GetField("items", Private).GetValue(database);
            items.AddRange(new ItemDefinition[] { sword, axe, mace, ring, flask });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
            {
                if (o != null) Object.Destroy(o);
            }
            made.Clear();
        }

        T Make<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            made.Add(asset);
            return asset;
        }

        /// <summary>A character with one limited bag ("Bag") and 2 right-hand, 2 ring and 3 quick item slots.</summary>
        Equipment Character(string name, int spaces = 6, bool leave = true)
        {
            var go = new GameObject(name);
            go.SetActive(false); // set fields up before Awake
            made.Add(go);

            var inventory = go.AddComponent<Inventory>();
            inventory.Database = database;
            var bags = (List<InventoryBag>)typeof(Inventory).GetField("bags", Private).GetValue(inventory);
            bags.Clear();
            bags.Add(new InventoryBag("Bag", true, spaces));

            var equipment = go.AddComponent<Equipment>();
            typeof(Equipment).GetField("equippedItemsLeaveTheBag", Private).SetValue(equipment, leave);
            var layout = new Equipment.Layout { rightHand = 2, leftHand = 2, rings = 2, quickItems = 3, spells = 0 };
            typeof(Equipment).GetField("layout", Private).SetValue(equipment, layout);

            go.SetActive(true);
            return equipment;
        }

        static InventoryBag BagOf(Equipment equipment) => equipment.Inventory.GetBag("Bag");

        static ItemStack StackOf(Inventory inventory, ItemDefinition item)
        {
            var found = new List<ItemStack>();
            inventory.FindAll(item, found);
            return found.Count > 0 ? found[0] : null;
        }

        static List<ItemDefinition> ItemsIn(InventoryBag bag)
        {
            var items = new List<ItemDefinition>();
            foreach (ItemStack stack in bag.Stacks) items.Add(stack.Item);
            return items;
        }

        [UnityTest]
        public IEnumerator EquipTakesItOutOfTheBagAndUnequipPutsItBack()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            inventory.Add(axe);
            ItemStack swordStack = StackOf(inventory, sword);

            Assert.That(inventory.EquippedBag, Is.Not.Null, "an Equipped bag was added");
            Assert.That(equipment.Equip(swordStack, EquipSlotType.RightHand, 0), Is.True);
            Assert.That(ItemsIn(BagOf(equipment)), Is.EqualTo(new ItemDefinition[] { axe }), "the sword left the bag");
            Assert.That(swordStack.Bag, Is.SameAs(inventory.EquippedBag));
            Assert.That(equipment.InHand(EquipSlotType.RightHand), Is.SameAs(swordStack));
            Assert.That(inventory.Has(sword), Is.True, "still carried");
            Assert.That(inventory.HomeOf(mace), Is.SameAs(BagOf(equipment)), "pickups never land in the Equipped bag");

            equipment.Unequip(EquipSlotType.RightHand, 0);
            Assert.That(equipment.IsEquipped(swordStack), Is.False);
            Assert.That(ItemsIn(BagOf(equipment)), Is.EqualTo(new ItemDefinition[] { axe, sword }));
            Assert.That(swordStack.InstanceId, Is.EqualTo(StackOf(inventory, sword).InstanceId), "same stack");
        }

        [UnityTest]
        public IEnumerator UnequipIntoAFullBagIsRefusedUnlessItSwaps()
        {
            Equipment equipment = Character("Character", spaces: 2);
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            ItemStack swordStack = StackOf(inventory, sword);
            equipment.Equip(swordStack, EquipSlotType.RightHand, 0);
            inventory.Add(axe);
            inventory.Add(ring);
            Assert.That(BagOf(equipment).IsFull, Is.True);

            equipment.Unequip(EquipSlotType.RightHand, 0);
            Assert.That(equipment.IsEquipped(swordStack), Is.True, "nowhere to go: stays equipped");
            Assert.That(equipment.UnequipInto(EquipSlotType.RightHand, 0, BagOf(equipment), int.MaxValue), Is.False);
            Assert.That(equipment.UnequipInto(EquipSlotType.RightHand, 0, BagOf(equipment), 1), Is.False, "a ring can't go in a hand");

            Assert.That(equipment.UnequipInto(EquipSlotType.RightHand, 0, BagOf(equipment), 0), Is.True, "swaps with the axe");
            Assert.That(ItemsIn(BagOf(equipment)), Is.EqualTo(new ItemDefinition[] { sword, ring }));
            Assert.That(equipment.Get(EquipSlotType.RightHand, 0).Item.Item, Is.SameAs(axe));
            Assert.That(StackOf(inventory, axe).Bag, Is.SameAs(inventory.EquippedBag));
        }

        [UnityTest]
        public IEnumerator UnequipIntoPutsItAtThatSpace()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            inventory.Add(axe);
            inventory.Add(mace);
            equipment.Equip(StackOf(inventory, mace), EquipSlotType.RightHand, 0);

            Assert.That(equipment.UnequipInto(EquipSlotType.RightHand, 0, BagOf(equipment), 0), Is.True);
            Assert.That(ItemsIn(BagOf(equipment)), Is.EqualTo(new ItemDefinition[] { mace, sword, axe }));
        }

        [UnityTest]
        public IEnumerator EquippingOverAnItemSwapsItIntoTheSpaceLeft()
        {
            Equipment equipment = Character("Character", spaces: 3);
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            inventory.Add(axe);
            inventory.Add(mace);
            equipment.Equip(StackOf(inventory, sword), EquipSlotType.RightHand, 0);
            inventory.Add(ring); // the bag is full again: axe, mace, ring
            Assert.That(BagOf(equipment).IsFull, Is.True);

            Assert.That(equipment.Equip(StackOf(inventory, mace), EquipSlotType.RightHand, 0), Is.True);
            Assert.That(ItemsIn(BagOf(equipment)), Is.EqualTo(new ItemDefinition[] { axe, sword, ring }), "the sword took the mace's space");
            Assert.That(equipment.Get(EquipSlotType.RightHand, 0).Item.Item, Is.SameAs(mace));
        }

        [UnityTest]
        public IEnumerator MovingBetweenSlotsSwaps()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            inventory.Add(axe);
            ItemStack swordStack = StackOf(inventory, sword), axeStack = StackOf(inventory, axe);
            equipment.Equip(swordStack, EquipSlotType.RightHand, 0);
            equipment.Equip(axeStack, EquipSlotType.RightHand, 1);

            Assert.That(equipment.Equip(swordStack, EquipSlotType.RightHand, 1), Is.True);
            Assert.That(equipment.Get(EquipSlotType.RightHand, 1).Item, Is.SameAs(swordStack));
            Assert.That(equipment.Get(EquipSlotType.RightHand, 0).Item, Is.SameAs(axeStack));
            Assert.That(BagOf(equipment).Stacks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator DroppingOrUsingUpAnEquippedItemEmptiesTheSlot()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            inventory.Add(flask, 2);
            ItemStack swordStack = StackOf(inventory, sword), flaskStack = StackOf(inventory, flask);
            equipment.Equip(swordStack, EquipSlotType.RightHand, 0);
            equipment.Equip(flaskStack, EquipSlotType.QuickItem, 0);
            Assert.That(BagOf(equipment).Stacks, Is.Empty);

            Assert.That(inventory.Remove(swordStack, 1), Is.EqualTo(1), "dropped");
            Assert.That(equipment.Get(EquipSlotType.RightHand, 0).IsEmpty, Is.True);
            Assert.That(inventory.Has(sword), Is.False);

            Assert.That(equipment.UseQuickItem(), Is.True);
            Assert.That(equipment.SelectedQuickItem, Is.SameAs(flaskStack));
            Assert.That(flaskStack.Count, Is.EqualTo(1));
            Assert.That(equipment.UseQuickItem(), Is.True);
            Assert.That(equipment.SelectedQuickItem, Is.Null, "used up");
            Assert.That(inventory.Has(flask), Is.False);
            Assert.That(inventory.EquippedBag.Stacks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PickupsTopUpTheEquippedStackFirst()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(flask, 2);
            ItemStack flaskStack = StackOf(inventory, flask);
            equipment.Equip(flaskStack, EquipSlotType.QuickItem, 0);

            Assert.That(inventory.Add(flask, 3), Is.EqualTo(3));
            Assert.That(flaskStack.Count, Is.EqualTo(5));
            Assert.That(BagOf(equipment).Stacks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator MovesIntoTheEquippedBagAreRefused()
        {
            Equipment equipment = Character("Character");
            yield return null;
            Inventory inventory = equipment.Inventory;
            inventory.Add(sword);
            Assert.That(inventory.Move(StackOf(inventory, sword), inventory.EquippedBag), Is.Zero);
            Assert.That(equipment.IsEquipped(StackOf(inventory, sword)), Is.False);
        }

        [UnityTest]
        public IEnumerator SaveAndRestoreKeepTheSlots()
        {
            Equipment original = Character("Original");
            Equipment restored = Character("Restored");
            restored.UseStartingEquipment = false;
            yield return null;

            original.Inventory.Add(sword);
            original.Inventory.Add(axe);
            original.Inventory.Add(flask, 4);
            original.Equip(StackOf(original.Inventory, sword), EquipSlotType.RightHand, 1);
            original.Equip(StackOf(original.Inventory, flask), EquipSlotType.QuickItem, 2);

            restored.Inventory.Restore(original.Inventory.Capture());
            restored.Restore(original.Capture());

            Assert.That(restored.Get(EquipSlotType.RightHand, 1).Item.Item, Is.SameAs(sword));
            Assert.That(restored.Get(EquipSlotType.QuickItem, 2).Item.Count, Is.EqualTo(4));
            Assert.That(ItemsIn(BagOf(restored)), Is.EqualTo(new ItemDefinition[] { axe }));
            Assert.That(restored.Inventory.EquippedBag.Stacks.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RestoringAnOldSaveMovesEquippedItemsOutOfTheBag()
        {
            Equipment old = Character("Old", leave: false);
            Equipment restored = Character("Restored");
            restored.UseStartingEquipment = false;
            yield return null;

            old.Inventory.Add(sword);
            old.Inventory.Add(axe);
            old.Equip(StackOf(old.Inventory, sword), EquipSlotType.RightHand, 0);
            Assert.That(ItemsIn(BagOf(old)), Is.EqualTo(new ItemDefinition[] { sword, axe }), "off: stays in the bag");

            restored.Inventory.Restore(old.Inventory.Capture());
            restored.Restore(old.Capture());

            Assert.That(restored.Get(EquipSlotType.RightHand, 0).Item.Item, Is.SameAs(sword));
            Assert.That(ItemsIn(BagOf(restored)), Is.EqualTo(new ItemDefinition[] { axe }));
        }

        [UnityTest]
        public IEnumerator MirrorKeepsUp()
        {
            Equipment original = Character("Original");
            Equipment copy = Character("Copy");
            copy.UseStartingEquipment = false;
            yield return null;

            original.Inventory.Add(sword);
            original.Inventory.Add(axe);
            original.Equip(StackOf(original.Inventory, sword), EquipSlotType.RightHand, 0);
            copy.Inventory.Mirror(original.Inventory.Capture());
            copy.Mirror(original.Capture());

            ItemStack copySword = copy.Get(EquipSlotType.RightHand, 0).Item;
            Assert.That(copySword, Is.Not.Null);
            Assert.That(copySword.Bag, Is.SameAs(copy.Inventory.EquippedBag));
            Assert.That(ItemsIn(BagOf(copy)), Is.EqualTo(new ItemDefinition[] { axe }));

            original.Unequip(EquipSlotType.RightHand, 0);
            copy.Inventory.Mirror(original.Inventory.Capture());
            copy.Mirror(original.Capture());

            Assert.That(copy.Get(EquipSlotType.RightHand, 0).IsEmpty, Is.True);
            Assert.That(ItemsIn(BagOf(copy)), Is.EqualTo(new ItemDefinition[] { axe, sword }));
            Assert.That(StackOf(copy.Inventory, sword), Is.SameAs(copySword), "the copy keeps its stack");
        }
    }
}
