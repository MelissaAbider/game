using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EchoShift
{
    /// <summary>Builds geometry from Unity's built-in primitive meshes without leaving stray colliders behind.</summary>
    public static class Prims
    {
        static readonly Dictionary<PrimitiveType, Mesh> Cache = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh MeshOf(PrimitiveType type)
        {
            if (Cache.TryGetValue(type, out var mesh) && mesh) return mesh;
            var temp = GameObject.CreatePrimitive(type);
            mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            Cache[type] = mesh;
            return mesh;
        }

        public static GameObject Make(string name, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale,
            Material material, bool collider = false, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(type);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                switch (type)
                {
                    case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                    case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                    case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
                    default: go.AddComponent<MeshCollider>(); break;
                }
            }
            return go;
        }

        /// <summary>Axis-aligned box in world units (center + size), parented without inheriting scale surprises.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material material,
            bool collider = true, bool castShadows = true)
            => Make(name, PrimitiveType.Cube, parent, center, size, material, collider, castShadows);

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
