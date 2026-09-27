using UnityEngine;

namespace EchoShift
{
    /// <summary>Exit ring. Dormant until the core terminal is unlocked; stepping in ends the sector.</summary>
    public sealed class ExitPortal : MonoBehaviour
    {
        public bool Active { get; private set; }
        Transform ring;
        Material ringMat, coreMat;
        Light glow;
        ParticleSystem swirl;

        public static ExitPortal Build(Transform parent, Vector3 position, int room)
        {
            var root = new GameObject("ExitPortal").transform;
            root.SetParent(parent, false);
            root.position = position;
            var p = root.gameObject.AddComponent<ExitPortal>();
            p.ringMat = Mats.Emissive(new Color(0.3f, 0.35f, 0.4f), 1f);
            p.ring = new GameObject("Ring").transform;
            p.ring.SetParent(root, false);
            p.ring.localPosition = new Vector3(0, 1.8f, 0);
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2f;
                var seg = Prims.Box("Seg", p.ring, new Vector3(Mathf.Cos(a) * 1.55f, Mathf.Sin(a) * 1.55f, 0), new Vector3(0.28f, 0.14f, 0.22f), p.ringMat, false, false);
                seg.transform.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg + 90f);
            }
            p.coreMat = Mats.Hologram(Mats.Green, 0.2f, 0.1f);
            Prims.Make("Core", PrimitiveType.Cylinder, root, new Vector3(0, 1.8f, 0), new Vector3(2.9f, 0.01f, 2.9f), p.coreMat, false, false)
                .transform.localRotation = Quaternion.Euler(90, 0, 0);

            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0, 1.8f, -0.6f);
            p.glow = lightGo.AddComponent<Light>();
            p.glow.type = LightType.Point;
            p.glow.range = 9f;
            p.glow.intensity = 0.5f;
            p.glow.color = Mats.Green;

            var psGo = new GameObject("Swirl");
            psGo.transform.SetParent(root, false);
            psGo.transform.localPosition = new Vector3(0, 1.8f, 0);
            p.swirl = psGo.AddComponent<ParticleSystem>();
            var main = p.swirl.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 0.2f;
            main.startSize = 0.12f;
            main.startColor = Mats.Green;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = p.swirl.emission;
            emission.rateOverTime = 0f;
            var shape = p.swirl.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.4f;
            var vel = p.swirl.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalZ = 2.5f;
            vel.radial = -0.6f;
            psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mats.Particles(Mats.Green * 2f);

            WorldLabel.Create(root, new Vector3(0, 4f, 0), "EXIT", 0.6f, Mats.Green, 2.4f, true);
            var e = WorldEntity.Register(root.gameObject, "exit_portal", "door", "exit portal", room, "exit", "portal", "way out", "door");
            e.ColorName = "green";
            e.ApproachDistance = 0.2f;
            var barrier = Barrier.Add("exit_portal", position.z - 1.2f, BarrierKind.VoiceKey, "The portal is dormant. Read the voice key on the core terminal.");
            barrier.Blocking = () => !p.Active;
            return p;
        }

        public void Activate()
        {
            Active = true;
            Mats.SetEmission(ringMat, Mats.Green, 6f);
            coreMat.SetColor("_BaseColor", new Color(Mats.Green.r, Mats.Green.g, Mats.Green.b, 0.35f) * 1.5f);
            glow.intensity = 6f;
            var emission = swirl.emission;
            emission.rateOverTime = 80f;
        }

        void Update()
        {
            ring.Rotate(0, 0, (Active ? 60f : 8f) * Time.deltaTime, Space.Self);
        }

        public bool Contains(Vector3 p) => Active && Mathf.Abs(p.z - transform.position.z) < 0.8f && Mathf.Abs(p.x - transform.position.x) < 1.4f;
    }
}
