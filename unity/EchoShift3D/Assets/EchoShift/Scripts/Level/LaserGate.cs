using UnityEngine;

namespace EchoShift
{
    /// <summary>Wall-to-wall laser. Floor beams are jumped over, chest-height beams crouched under.</summary>
    public sealed class LaserGate : MonoBehaviour
    {
        public string Id;
        public bool Overhead;
        public float Z;
        Bounds beam;
        Material beamMat, coreMat;
        float t;

        public static LaserGate Build(Transform parent, string id, string label, float z, float floorY, bool overhead, int room, float halfWidth)
        {
            var root = new GameObject("Laser " + id).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0, floorY, z);
            var gate = root.gameObject.AddComponent<LaserGate>();
            gate.Id = id;
            gate.Overhead = overhead;
            gate.Z = z;
            float y = overhead ? 1.28f : 0.38f;
            float width = halfWidth * 2f - 0.3f;

            var housing = Mats.Solid(new Color(0.1f, 0.12f, 0.15f), 0.6f, 0.8f);
            var lamp = Mats.Emissive(Mats.Red, 6f);
            foreach (var side in new[] { -1f, 1f })
            {
                float x = side * (halfWidth - 0.12f);
                Prims.Box("Emitter", root, new Vector3(x, y, 0), new Vector3(0.22f, 0.5f, 0.3f), housing, false);
                Prims.Box("Lens", root, new Vector3(x - side * 0.12f, y, 0), new Vector3(0.03f, 0.16f, 0.16f), lamp, false, false);
            }
            gate.coreMat = Mats.Emissive(new Color(1f, 0.55f, 0.6f), 9f);
            gate.beamMat = Mats.Hologram(Mats.Red, 2.4f, 0.55f);
            Prims.Box("Core", root, new Vector3(0, y, 0), new Vector3(width, 0.018f, 0.018f), gate.coreMat, false, false);
            Prims.Box("Glow", root, new Vector3(0, y, 0), new Vector3(width, 0.09f, 0.09f), gate.beamMat, false, false);

            // Hazard stripes on the floor.
            var stripe = Mats.Emissive(Mats.Red, 1.2f, new Color(0.2f, 0.02f, 0.05f));
            for (float x = -halfWidth + 0.6f; x < halfWidth - 0.4f; x += 0.9f)
            {
                var s = Prims.Box("Stripe", root, new Vector3(x, 0.012f, 0), new Vector3(0.45f, 0.01f, 0.5f), stripe, false, false);
                s.transform.localRotation = Quaternion.Euler(0, 35f, 0);
            }

            WorldLabel.Create(root, new Vector3(0, y + 0.55f, -0.2f), overhead ? "CROUCH" : "JUMP", 0.34f, Mats.Red, 2.2f);
            Sfx.Loop("laserHum", root.gameObject, 0.35f, true, 12f);

            gate.beam = new Bounds(new Vector3(0, floorY + y, z), new Vector3(width, 0.06f, 0.08f));
            var entity = WorldEntity.Register(root.gameObject, id, "hazard", label, room,
                overhead ? new[] { "overhead", "low beam", "chest height", "beam", "crouch", "laser" } : new[] { "laser", "floor laser", "beam", "jump" });
            entity.ColorName = "red";
            entity.ApproachDistance = 1.2f;

            var barrier = Barrier.Add(id, z, overhead ? BarrierKind.Crouch : BarrierKind.Jump,
                overhead ? "Chest-height beam. Say \"crouch under the beam\"." : "Laser ahead. Say \"jump over the laser\".");
            if (overhead) barrier.Blocking = () => !(GameDirector.I && GameDirector.I.Subject && GameDirector.I.Subject.Crouching);
            return gate;
        }

        public bool Hits(Bounds body) => beam.Intersects(body);

        public float BeamY => beam.center.y;

        void Update()
        {
            t += Time.deltaTime;
            float flicker = 1f + Mathf.Sin(t * 37f) * 0.08f + Mathf.Sin(t * 11f) * 0.06f;
            Mats.SetEmission(coreMat, new Color(1f, 0.55f, 0.6f), 9f * flicker);
        }
    }
}
