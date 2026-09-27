using UnityEngine;

namespace EchoShift
{
    /// <summary>Ceiling acoustic sensor: while the subject is in its zone, any voice above WHISPER triggers the alarm.</summary>
    public sealed class Sentinel : MonoBehaviour
    {
        public float ZStart, ZEnd;
        public bool Bypassed { get; private set; }
        public bool Listening { get; private set; }
        public float Loudness { get; private set; }

        Transform eye;
        Light cone;
        Material eyeMat, stripMat;
        float loudTime, cooldown, t;

        public static Sentinel Build(Transform parent, float zStart, float zEnd, float floorY, float ceilingY, float halfWidth, int room)
        {
            var root = new GameObject("Sentinel").transform;
            root.SetParent(parent, false);
            float cz = (zStart + zEnd) * 0.5f;
            root.position = new Vector3(0, floorY, cz);
            var s = root.gameObject.AddComponent<Sentinel>();
            s.ZStart = zStart;
            s.ZEnd = zEnd;

            var dark = Mats.Solid(new Color(0.08f, 0.09f, 0.12f), 0.7f, 0.8f);
            float h = ceilingY - floorY;
            Prims.Make("Mount", PrimitiveType.Cylinder, root, new Vector3(0, h - 0.4f, 0), new Vector3(0.15f, 0.4f, 0.15f), dark, false);
            var head = Prims.Make("Head", PrimitiveType.Sphere, root, new Vector3(0, h - 0.95f, 0), new Vector3(0.9f, 0.6f, 0.9f), dark, false);
            s.eyeMat = Mats.Emissive(Mats.Amber, 6f);
            s.eye = Prims.Make("Eye", PrimitiveType.Sphere, head.transform, new Vector3(0, -0.2f, 0.35f), new Vector3(0.45f, 0.45f, 0.3f), s.eyeMat, false, false).transform;

            var lightGo = new GameObject("Cone");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0, h - 1.1f, 0);
            lightGo.transform.localRotation = Quaternion.Euler(90, 0, 0);
            s.cone = lightGo.AddComponent<Light>();
            s.cone.type = LightType.Spot;
            s.cone.spotAngle = 95f;
            s.cone.range = h + 2f;
            s.cone.intensity = 18f;
            s.cone.color = Mats.Amber;
            s.cone.shadows = LightShadows.None;

            s.stripMat = Mats.Emissive(Mats.Amber, 2.5f);
            foreach (var z in new[] { zStart, zEnd })
                Prims.Box("ZoneEdge", parent, new Vector3(0, floorY + 0.012f, z), new Vector3(halfWidth * 2f, 0.01f, 0.12f), s.stripMat, false, false);
            WorldLabel.Create(root, new Vector3(0, h - 1.9f, 0), "LISTENING · WHISPER ONLY", 0.3f, Mats.Amber, 2f, false, true);

            var entity = WorldEntity.Register(root.gameObject, "acoustic_sentinel", "sensor", "acoustic sentinel", room, "sentinel", "sensor", "microphone", "whisper");
            entity.ColorName = "amber";
            entity.IsActive = () => !s.Bypassed;
            var barrier = Barrier.Add("acoustic_sentinel", zStart, BarrierKind.Whisper, "Acoustic sentinel ahead. From now on, WHISPER your commands.");
            barrier.AnnounceOnly = true;
            barrier.Blocking = () => !s.Bypassed;
            return s;
        }

        void Update()
        {
            t += Time.deltaTime;
            eye.localRotation = Quaternion.Euler(0, Mathf.Sin(t * 0.9f) * 50f, 0);
            var director = GameDirector.I;
            var voice = VoiceInput.I;
            if (Bypassed || !director || !director.Subject || !voice) return;

            float z = director.Subject.transform.position.z;
            if (z > ZEnd + 0.4f)
            {
                Bypassed = true;
                Listening = false;
                SetColor(Mats.Green);
                Sfx.Play("unlock", 0.6f);
                director.OnSentinelBypassed();
                return;
            }
            Listening = z > ZStart - 2.5f && z < ZEnd;
            Loudness = Listening ? Mathf.Clamp01(voice.EffectiveLevel / voice.WhisperMax) : 0f;
            cooldown -= Time.unscaledDeltaTime;
            if (!Listening || cooldown > 0f) { loudTime = 0f; return; }

            loudTime = voice.EffectiveLevel > voice.WhisperMax ? loudTime + Time.unscaledDeltaTime : Mathf.Max(0f, loudTime - Time.unscaledDeltaTime);
            SetColor(Color.Lerp(Mats.Amber, Mats.Red, Mathf.Clamp01(loudTime / 0.3f)));
            if (loudTime > 0.3f)
            {
                loudTime = 0f;
                cooldown = 2.5f;
                SetColor(Mats.Red);
                Sfx.Play("alarm", 0.8f);
                director.Damage("Too loud! The sentinel heard you. Whisper.", new Vector3(0, director.Subject.transform.position.y, ZStart - 3.5f), true);
            }
        }

        void SetColor(Color c)
        {
            Mats.SetEmission(eyeMat, c, 6f);
            Mats.SetEmission(stripMat, c, 2.5f);
            cone.color = c;
        }
    }
}
