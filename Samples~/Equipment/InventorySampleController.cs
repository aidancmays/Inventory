using CharacterStats;
using InventorySystem;
using UnityEngine;

namespace InventorySystem.Samples
{
    /// <summary>
    /// Equipment sample. Add it to an empty GameObject and press Play: it adds an Inventory, Equipment and a StatSheet
    /// (set up from the Sample Character profile), gives one of every item in the Sample Item Database (three flasks),
    /// and opens the inventory (F6) and stats (F5) debug panels. Click items to equip them, click slots to take them
    /// off, cycle hands and spells with ‹ ›, and use flasks after taking damage with the button bottom left.
    /// The Ring of Vigour raises max health and lights a glow (an EquipEffect) while worn.
    /// </summary>
    [AddComponentMenu("Inventory/Samples/Inventory Sample Controller")]
    public class InventorySampleController : MonoBehaviour
    {
        [SerializeField] StatProfile profile;
        [SerializeField] ItemDatabase database;
        [SerializeField] PoolDefinition health;
        [SerializeField] ItemDefinition flask;
        [SerializeField] int flasks = 3;

        Inventory inventory;
        Equipment equipment;
        StatSheet sheet;
        string rightHand = "empty", leftHand = "empty";

        void Awake()
        {
            if (!TryGetComponent(out sheet)) sheet = gameObject.AddComponent<StatSheet>();
            if (sheet.Profile == null) sheet.Setup(profile);

            if (!TryGetComponent(out inventory)) inventory = gameObject.AddComponent<Inventory>();
            if (inventory.Database == null) inventory.Database = database;
            if (!TryGetComponent(out equipment)) equipment = gameObject.AddComponent<Equipment>();

            if (GetComponent<InventoryDebugPanel>() == null) gameObject.AddComponent<InventoryDebugPanel>();
            if (GetComponent<StatsDebugPanel>() == null) gameObject.AddComponent<StatsDebugPanel>();

            if (database != null)
            {
                foreach (ItemDefinition item in database.Items)
                {
                    if (item != null) inventory.Add(item, item == flask ? flasks : 1);
                }
            }

            // What your weapon code would listen to, to put the WeaponItem's Prefab in the hand.
            equipment.HeldChanged += (hand, stack) =>
            {
                string held = stack != null ? stack.Item.DisplayName : "empty";
                if (hand == EquipSlotType.RightHand) rightHand = held;
                else leftHand = held;
            };
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, Screen.height - 90f, 300f, 80f), GUI.skin.box);
            GUILayout.Label($"Right hand: {rightHand}    Left hand: {leftHand}");
            GUILayout.BeginHorizontal();
            if (sheet.TryGetPool(health, out ResourcePool pool) && GUILayout.Button("Take 25 damage")) pool.Spend(25f, false, "Button");
            if (GUILayout.Button("Use quick item")) equipment.UseQuickItem();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
