using System.Collections.Generic;
using UnityEngine;

namespace InventorySystem
{
    /// <summary>
    /// Every item in the game, so saved items (stored by id) can be found again. One asset for the whole game.
    /// Right-click the asset's header (or the ⋮ menu) and pick "Collect Every Item In Project" to fill it.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item Database", fileName = "Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] List<ItemDefinition> items = new List<ItemDefinition>();

        Dictionary<string, ItemDefinition> byId;

        public IReadOnlyList<ItemDefinition> Items => items;

        public ItemDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (byId == null)
            {
                byId = new Dictionary<string, ItemDefinition>();
                foreach (ItemDefinition item in items)
                {
                    if (item != null) byId[item.Id] = item;
                }
            }

            return byId.TryGetValue(id, out ItemDefinition found) ? found : null;
        }

        void OnEnable() => byId = null;

        void OnValidate()
        {
            byId = null;

            var seen = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in items)
            {
                if (item == null) continue;
                if (seen.TryGetValue(item.Id, out ItemDefinition other) && other != item)
                    Debug.LogWarning($"Item Database: '{item.name}' and '{other.name}' share the id '{item.Id}'; saves can't tell them apart.", this);
                seen[item.Id] = item;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Collect Every Item In Project")]
        void CollectEveryItem()
        {
            UnityEditor.Undo.RecordObject(this, "Collect Every Item");
            items.Clear();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(ItemDefinition)))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item != null) items.Add(item);
            }
            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            UnityEditor.EditorUtility.SetDirty(this);
            OnValidate();
        }
#endif
    }
}
