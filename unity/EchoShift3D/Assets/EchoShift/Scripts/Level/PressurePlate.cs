using UnityEngine;

namespace EchoShift
{
    /// <summary>Holds its door open only while someone stands on it: the subject, or one of its echo clones.</summary>
    public sealed class PressurePlate : MonoBehaviour
    {
        public bool Pressed { get; private set; }
        Vector3 center;
        Vector2 half;
        Material glow;
        Transform top;
        bool was;

        public static PressurePlate Build(Transform parent, Vector3 center, Vector2 size, int room)
        {
            var root = new GameObject("PressurePlate").transform;
            root.SetParent(parent, false);
            root.position = center;
            var p = root.gameObject.AddComponent<PressurePlate>();
            p.center = center;
            p.half = size * 0.5f;
            var basePlate = Mats.Solid(new Color(0.1f, 0.11f, 0.14f), 0.7f, 0.8f);
            Prims.Box("Base", root, new Vector3(0, 0.03f, 0), new Vector3(size.x + 0.3f, 0.06f, size.y + 0.3f), basePlate, false);
            p.glow = Mats.Emissive(Mats.Green, 1.5f, new Color(0.05f, 0.1f, 0.08f));
            p.top = Prims.Box("Top", root, new Vector3(0, 0.09f, 0), new Vector3(size.x, 0.06f, size.y), p.glow, false).transform;
            WorldLabel.Create(root, new Vector3(0, 1.9f, 0), "PRESSURE PLATE", 0.3f, Mats.Green, 2f, false, true);
            var e = WorldEntity.Register(root.gameObject, "pressure_plate", "switch", "pressure plate", room, "plate", "pressure plate", "switch", "button", "pad");
            e.ColorName = "green";
            e.ApproachDistance = 0.15f;
            return p;
        }

        bool Contains(Vector3 p) => Mathf.Abs(p.x - center.x) < half.x && Mathf.Abs(p.z - center.z) < half.y && Mathf.Abs(p.y - center.y) < 1f;

        void Update()
        {
            var d = GameDirector.I;
            bool pressed = false;
            if (d && d.Subject && Contains(d.Subject.transform.position)) pressed = true;
            if (!pressed && d && d.Echoes) pressed = d.Echoes.AnyClone(Contains);
            Pressed = pressed;
            if (pressed == was) return;
            was = pressed;
            Mats.SetEmission(glow, pressed ? Color.white : Mats.Green, pressed ? 6f : 1.5f);
            top.localPosition = new Vector3(0, pressed ? 0.05f : 0.09f, 0);
            Sfx.PlayAt(pressed ? "blip" : "deny", transform.position, 0.6f);
        }
    }
}
