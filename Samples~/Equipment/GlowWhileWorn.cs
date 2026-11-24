using System;
using System.Collections.Generic;
using InventorySystem;
using UnityEngine;

namespace InventorySystem.Samples
{
    /// <summary>
    /// An example EquipEffect: a point light on the wearer while the item is in effect. Pick it from an equipment
    /// item's Effects list (the Ring of Vigour uses it). The light is kept per stack, since the effect object itself
    /// is shared by everyone with the item.
    /// </summary>
    [Serializable]
    public class GlowWhileWorn : EquipEffect
    {
        public Color color = new Color(1f, 0.75f, 0.3f);
        public float range = 4f;
        public float intensity = 2f;

        static readonly Dictionary<(ItemStack, EquipSlotType?), Light> lights = new Dictionary<(ItemStack, EquipSlotType?), Light>();

        public override void Apply(in EquipContext context)
        {
            var go = new GameObject("Glow (" + context.Stack.Item.DisplayName + ")");
            go.transform.SetParent(context.Wearer.transform, false);
            go.transform.localPosition = Vector3.up;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            lights[(context.Stack, context.Hand)] = light;
        }

        public override void Remove(in EquipContext context)
        {
            var key = (context.Stack, context.Hand);
            if (!lights.TryGetValue(key, out Light light)) return;

            lights.Remove(key);
            if (light != null) UnityEngine.Object.Destroy(light.gameObject);
        }
    }
}
