using UnityEngine;

namespace EchoShift
{
    /// <summary>Rotating laser arm. It only moves when time flows, and time only flows while the player makes sound.</summary>
    public sealed class ChronoSweeper : MonoBehaviour
    {
        public float Radius = 7.6f;
        public float Speed = 70f;
        float angle, beamHeight;
        Transform arm;

        public Vector3 ArmDirection => arm.forward;
        public float BeamHeight => beamHeight;

        public static ChronoSweeper Build(Transform parent, Vector3 pivot, float speed, float startAngle, int room, string id)
        {
            var root = new GameObject("Sweeper " + id).transform;
            root.SetParent(parent, false);
            root.position = pivot;
            var sw = root.gameObject.AddComponent<ChronoSweeper>();
            sw.Speed = speed;
            sw.angle = startAngle;
            sw.beamHeight = 0.95f;

            var dark = Mats.Solid(new Color(0.09f, 0.1f, 0.13f), 0.7f, 0.85f);
            var gold = Mats.Emissive(Mats.Amber, 5f);
            Prims.Make("Pylon", PrimitiveType.Cylinder, root, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 0.8f, 0.9f), dark, true);
            Prims.Make("Ring", PrimitiveType.Cylinder, root, new Vector3(0, 1.62f, 0), new Vector3(1.05f, 0.03f, 1.05f), gold, false, false);
            sw.arm = new GameObject("Arm").transform;
            sw.arm.SetParent(root, false);
            sw.arm.localPosition = new Vector3(0, sw.beamHeight, 0);
            var core = Mats.Emissive(new Color(1f, 0.7f, 0.3f), 9f);
            var glow = Mats.Hologram(Mats.Amber, 2.2f, 0.5f);
            Prims.Box("Core", sw.arm, new Vector3(0, 0, sw.Radius * 0.5f), new Vector3(0.02f, 0.02f, sw.Radius), core, false, false);
            Prims.Box("Glow", sw.arm, new Vector3(0, 0, sw.Radius * 0.5f), new Vector3(0.1f, 0.1f, sw.Radius), glow, false, false);
            Prims.Make("Tip", PrimitiveType.Sphere, sw.arm, new Vector3(0, 0, sw.Radius), Vector3.one * 0.18f, core, false, false);
            Sfx.Loop("laserHum", root.gameObject, 0.4f, true, 14f);

            var e = WorldEntity.Register(root.gameObject, id, "hazard", "time sweeper", room, "sweeper", "rotating laser", "laser", "timing");
            e.ColorName = "gold";
            return sw;
        }

        void Update()
        {
            angle += Speed * Time.deltaTime;
            arm.localRotation = Quaternion.Euler(0, angle, 0);
        }

        public bool Hits(Subject s)
        {
            var p = s.transform.position - transform.position;
            float feet = p.y, head = p.y + s.Height;
            if (beamHeight < feet || beamHeight > head) return false;
            var dir = arm.forward;
            var flat = new Vector2(p.x, p.z);
            var d = new Vector2(dir.x, dir.z).normalized;
            float along = Vector2.Dot(flat, d);
            if (along < 0f || along > Radius) return false;
            return (flat - d * along).magnitude < Subject.Radius + 0.06f;
        }
    }
}
