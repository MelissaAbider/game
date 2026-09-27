using System.Collections;
using UnityEngine;

namespace EchoShift
{
    /// <summary>Resonant glass wall. Only a real shout (measured on the microphone) shatters it.</summary>
    public sealed class SonicGlass : MonoBehaviour
    {
        public bool Intact = true;
        public float Resonance { get; private set; }
        float z, floorY, halfWidth, height;
        Material paneMat, edgeMat;
        Transform pane;
        BoxCollider blocker;
        float loudTime;

        public static SonicGlass Build(Transform parent, float z, float floorY, float halfWidth, float height, int room)
        {
            var root = new GameObject("SonicGlass").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(0, floorY, z);
            var g = root.gameObject.AddComponent<SonicGlass>();
            g.z = z;
            g.floorY = floorY;
            g.halfWidth = halfWidth;
            g.height = height;
            g.paneMat = Mats.Glass(Mats.Violet, 0.2f, 0.97f);
            g.edgeMat = Mats.Emissive(Mats.Violet, 3f);
            g.pane = Prims.Box("Pane", root, new Vector3(0, height * 0.5f, 0), new Vector3(halfWidth * 2f, height, 0.12f), g.paneMat, false, false).transform;
            Prims.Box("Top", root, new Vector3(0, height, 0), new Vector3(halfWidth * 2f, 0.12f, 0.3f), g.edgeMat, false, false);
            Prims.Box("Bottom", root, new Vector3(0, 0.03f, 0), new Vector3(halfWidth * 2f, 0.06f, 0.3f), g.edgeMat, false, false);
            g.blocker = root.gameObject.AddComponent<BoxCollider>();
            g.blocker.center = new Vector3(0, height * 0.5f, 0);
            g.blocker.size = new Vector3(halfWidth * 2f, height, 0.3f);
            WorldLabel.Create(root, new Vector3(0, 2.4f, -0.25f), "SHOUT TO SHATTER", 0.42f, Mats.Violet, 2f);

            var entity = WorldEntity.Register(root.gameObject, "sonic_glass", "barrier", "sonic glass", room, "glass", "wall", "window", "barrier", "shout");
            entity.ColorName = "violet";
            entity.IsActive = () => g.Intact;
            var barrier = Barrier.Add("sonic_glass", z, BarrierKind.Shout, "Sonic glass! Hold V and SHOUT anything, like \"BREAK THROUGH!\"");
            barrier.Blocking = () => g.Intact;
            return g;
        }

        void Update()
        {
            if (!Intact) return;
            var director = GameDirector.I;
            var voice = VoiceInput.I;
            if (!director || !director.Subject || !voice) return;
            float distance = Mathf.Abs(director.Subject.transform.position.z - z);
            Resonance = distance < 9f ? Mathf.Clamp01(voice.EffectiveLevel / voice.ShoutMin) : 0f;

            float jitter = Resonance * 0.03f;
            pane.localPosition = new Vector3(Random.Range(-jitter, jitter), height * 0.5f, Random.Range(-jitter, jitter));
            Mats.SetEmission(edgeMat, Color.Lerp(Mats.Violet, Color.white, Resonance), 3f + Resonance * 8f);

            loudTime = Resonance >= 1f ? loudTime + Time.unscaledDeltaTime : 0f;
            if (loudTime > 0.12f) Shatter();
        }

        public void Shatter()
        {
            if (!Intact) return;
            Intact = false;
            blocker.enabled = false;
            pane.gameObject.SetActive(false);
            Sfx.Play("shatter", 1f);
            StartCoroutine(Shards());
            GameDirector.I?.OnGlassShattered(transform.position + Vector3.up * 2f);
        }

        IEnumerator Shards()
        {
            var shardMat = Mats.Glass(Color.Lerp(Mats.Violet, Color.white, 0.4f), 0.55f, 0.98f);
            var glow = Mats.Emissive(Mats.Violet, 5f);
            var pieces = new Rigidbody[150];
            for (int i = 0; i < pieces.Length; i++)
            {
                var pos = new Vector3(Random.Range(-halfWidth, halfWidth), floorY + Random.Range(0.1f, height), z);
                var size = new Vector3(Random.Range(0.08f, 0.35f), Random.Range(0.08f, 0.45f), 0.03f);
                var go = Prims.Make("Shard", PrimitiveType.Cube, null, pos, size, i % 9 == 0 ? glow : shardMat, true, false);
                go.transform.rotation = Random.rotation;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.AddForce(new Vector3(Random.Range(-2f, 2f), Random.Range(0f, 3f), Random.Range(3f, 8f)), ForceMode.VelocityChange);
                rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.VelocityChange);
                pieces[i] = rb;
            }
            yield return new WaitForSeconds(6f);
            foreach (var p in pieces) if (p) Destroy(p.gameObject);
        }
    }
}
