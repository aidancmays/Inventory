using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace InventorySystem.Tests
{
    /// <summary>Inventory.Reorder: a stack moves within its own bag, and copies (Mirror) follow the new order.</summary>
    public class ReorderTests
    {
        readonly List<Object> made = new List<Object>();
        ItemDatabase database;
        WeaponItem sword, axe, mace;
        ConsumableItem flask;

        [SetUp]
        public void SetUp()
        {
            sword = Make<WeaponItem>("Test Sword");
            axe = Make<WeaponItem>("Test Axe");
            mace = Make<WeaponItem>("Test Mace");
            flask = Make<ConsumableItem>("Test Flask");

            database = Make<ItemDatabase>("Test Database");
            var items = (List<ItemDefinition>)typeof(ItemDatabase).GetField("items", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
            items.AddRange(new ItemDefinition[] { sword, axe, mace, flask });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
            {
                if (o != null) Object.DestroyImmediate(o);
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

        /// <summary>An inventory with two catch-all bags; everything added goes in the first.</summary>
        Inventory TwoBags(string name)
        {
            var go = new GameObject(name);
            made.Add(go);
            var inventory = go.AddComponent<Inventory>();
            inventory.Database = database;
            var bags = (List<InventoryBag>)typeof(Inventory).GetField("bags", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory);
            bags.Clear();
            bags.Add(new InventoryBag("First"));
            bags.Add(new InventoryBag("Second"));
            return inventory;
        }

        static List<ItemDefinition> ItemsIn(InventoryBag bag)
        {
            var items = new List<ItemDefinition>();
            foreach (ItemStack stack in bag.Stacks) items.Add(stack.Item);
            return items;
        }

        static ItemStack StackOf(Inventory inventory, ItemDefinition item)
        {
            var found = new List<ItemStack>();
            inventory.FindAll(item, found);
            return found.Count > 0 ? found[0] : null;
        }

        [Test]
        public void MovesTheStackAndShiftsTheRest()
        {
            Inventory inventory = TwoBags("Inventory");
            inventory.Add(sword);
            inventory.Add(axe);
            inventory.Add(mace);
            InventoryBag first = inventory.GetBag("First");

            var events = new List<(ItemStack stack, int previous)>();
            inventory.StackChanged += (stack, previous) => events.Add((stack, previous));

            ItemStack maceStack = StackOf(inventory, mace);
            Assert.That(inventory.Reorder(maceStack, 0), Is.True);
            Assert.That(ItemsIn(first), Is.EqualTo(new ItemDefinition[] { mace, sword, axe }));
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].stack, Is.SameAs(maceStack));
            Assert.That(events[0].previous, Is.EqualTo(1), "count unchanged");

            Assert.That(inventory.Reorder(maceStack, 1), Is.True);
            Assert.That(ItemsIn(first), Is.EqualTo(new ItemDefinition[] { sword, mace, axe }));
        }

        [Test]
        public void ClampsTheIndex()
        {
            Inventory inventory = TwoBags("Inventory");
            inventory.Add(sword);
            inventory.Add(axe);
            inventory.Add(mace);
            InventoryBag first = inventory.GetBag("First");

            Assert.That(inventory.Reorder(StackOf(inventory, sword), 99), Is.True);
            Assert.That(ItemsIn(first), Is.EqualTo(new ItemDefinition[] { axe, mace, sword }));

            Assert.That(inventory.Reorder(StackOf(inventory, sword), -5), Is.True);
            Assert.That(ItemsIn(first), Is.EqualTo(new ItemDefinition[] { sword, axe, mace }));
        }

        [Test]
        public void NothingHappensWhenNothingMoves()
        {
            Inventory inventory = TwoBags("Inventory");
            Inventory other = TwoBags("Other");
            inventory.Add(sword);
            inventory.Add(axe);
            other.Add(mace);
            int events = 0;
            inventory.StackChanged += (stack, previous) => events++;

            Assert.That(inventory.Reorder(StackOf(inventory, axe), 1), Is.False, "already there");
            Assert.That(inventory.Reorder(StackOf(inventory, axe), 5), Is.False, "clamped to where it is");
            Assert.That(inventory.Reorder(null, 0), Is.False);
            Assert.That(inventory.Reorder(StackOf(other, mace), 0), Is.False, "not in this inventory");
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void OtherBagsAreUntouched()
        {
            Inventory inventory = TwoBags("Inventory");
            inventory.Add(sword);
            inventory.Add(axe);
            inventory.Add(mace);
            inventory.Add(flask);
            InventoryBag first = inventory.GetBag("First");
            InventoryBag second = inventory.GetBag("Second");
            inventory.Move(StackOf(inventory, mace), second);
            inventory.Move(StackOf(inventory, flask), second);

            inventory.Reorder(StackOf(inventory, axe), 0);

            Assert.That(ItemsIn(first), Is.EqualTo(new ItemDefinition[] { axe, sword }));
            Assert.That(ItemsIn(second), Is.EqualTo(new ItemDefinition[] { mace, flask }));
        }

        [Test]
        public void MirrorFollowsTheNewOrder()
        {
            Inventory original = TwoBags("Original");
            Inventory copy = TwoBags("Copy");
            original.Add(sword);
            original.Add(axe);
            original.Add(mace);
            copy.Mirror(original.Capture());

            ItemStack copyMace = StackOf(copy, mace);
            var events = new List<ItemStack>();
            copy.StackChanged += (stack, previous) => events.Add(stack);

            original.Reorder(StackOf(original, mace), 0);
            copy.Mirror(original.Capture());

            Assert.That(ItemsIn(copy.GetBag("First")), Is.EqualTo(new ItemDefinition[] { mace, sword, axe }));
            Assert.That(StackOf(copy, mace), Is.SameAs(copyMace), "the copy keeps its stacks");
            Assert.That(events, Is.Not.Empty, "the copy says something changed, so UI redraws");

            events.Clear();
            copy.Mirror(original.Capture());
            Assert.That(events, Is.Empty, "nothing changed: nothing happens");
        }
    }
}
