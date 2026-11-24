using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace InventorySystem.Tests
{
    /// <summary>Inventory.Mirror and Equipment.Mirror: a copy matches the original and keeps its stacks.</summary>
    public class MirrorTests
    {
        readonly List<Object> made = new List<Object>();
        ItemDatabase database;
        WeaponItem sword, axe;
        ConsumableItem flask;

        [SetUp]
        public void SetUp()
        {
            sword = Make<WeaponItem>("Test Sword");
            axe = Make<WeaponItem>("Test Axe");
            flask = Make<ConsumableItem>("Test Flask");
            typeof(ItemDefinition).GetField("maxStack", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(flask, 99);

            database = Make<ItemDatabase>("Test Database");
            var items = (List<ItemDefinition>)typeof(ItemDatabase).GetField("items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
            items.AddRange(new ItemDefinition[] { sword, axe, flask });
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

        Equipment Character(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            go.AddComponent<Inventory>().Database = database;
            return go.AddComponent<Equipment>();
        }

        static ItemStack StackOf(Inventory inventory, ItemDefinition item)
        {
            var found = new List<ItemStack>();
            inventory.FindAll(item, found);
            return found.Count > 0 ? found[0] : null;
        }

        static void Sync(Equipment from, Equipment to)
        {
            to.Inventory.Mirror(from.Inventory.Capture());
            to.Mirror(from.Capture());
        }

        [UnityTest]
        public IEnumerator CopyMatchesAndKeepsItsStacks()
        {
            Equipment original = Character("Original");
            Equipment copy = Character("Copy");
            copy.UseStartingEquipment = false;
            yield return null; // Start

            original.Inventory.Add(sword);
            original.Inventory.Add(axe);
            original.Inventory.Add(flask, 3);
            original.Equip(StackOf(original.Inventory, sword), EquipSlotType.RightHand, 0);
            original.Equip(StackOf(original.Inventory, axe), EquipSlotType.RightHand, 1);
            original.EquipAnywhere(StackOf(original.Inventory, flask));

            Sync(original, copy);

            ItemStack copySword = copy.Get(EquipSlotType.RightHand, 0).Item;
            Assert.That(copySword, Is.Not.Null);
            Assert.That(copySword.InstanceId, Is.EqualTo(StackOf(original.Inventory, sword).InstanceId));
            Assert.That(copy.InHand(EquipSlotType.RightHand), Is.SameAs(copySword));
            Assert.That(copy.SelectedQuickItem.Count, Is.EqualTo(3));

            int slotEvents = 0, heldEvents = 0, stackEvents = 0;
            copy.SlotChanged += slot => slotEvents++;
            copy.HeldChanged += (hand, stack) => heldEvents++;
            copy.Inventory.StackChanged += (stack, previous) => stackEvents++;

            // Nothing changed: nothing happens.
            Sync(original, copy);
            Assert.That(slotEvents + heldEvents + stackEvents, Is.Zero);

            original.Cycle(EquipSlotType.RightHand);
            original.Inventory.Remove(StackOf(original.Inventory, flask), 1);
            Sync(original, copy);

            Assert.That(copy.Get(EquipSlotType.RightHand, 0).Item, Is.SameAs(copySword), "the sword's stack is kept");
            Assert.That(copy.Selected(EquipSlotType.RightHand), Is.EqualTo(1));
            Assert.That(copy.InHand(EquipSlotType.RightHand).Item, Is.SameAs(axe));
            Assert.That(copy.SelectedQuickItem.Count, Is.EqualTo(2));
            Assert.That(slotEvents, Is.Zero);
            Assert.That(heldEvents, Is.EqualTo(1));
            Assert.That(stackEvents, Is.EqualTo(1));

            // Used up on the original: gone from the copy's slot too.
            original.Inventory.Remove(StackOf(original.Inventory, flask), 2);
            Sync(original, copy);
            Assert.That(copy.SelectedQuickItem, Is.Null);
            Assert.That(copy.Inventory.Count(flask), Is.Zero);
        }

        [UnityTest]
        public IEnumerator MirrorReplacesTheCopysOwnContents()
        {
            Equipment original = Character("Original");
            Equipment copy = Character("Copy");
            copy.Inventory.Add(flask, 5); // like its own starting items
            yield return null;

            original.Inventory.Add(sword);
            Sync(original, copy);

            Assert.That(copy.Inventory.Count(flask), Is.Zero);
            Assert.That(copy.Inventory.Count(sword), Is.EqualTo(1));
        }
    }
}
