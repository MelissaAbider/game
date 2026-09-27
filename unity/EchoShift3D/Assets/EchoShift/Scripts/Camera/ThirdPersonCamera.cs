using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Over-the-shoulder follow camera on unscaled time (stays smooth while voice-time freezes the world).
    /// Right mouse drag or Q/E to orbit, wheel to zoom. Auto-realigns behind the subject when it moves.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;
        public bool Manual; // cinematics drive the transform directly

        float yaw, pitch = 16f, distance = 5.6f;
        float idle;
        float shake;
        Vector3 velocity;
        Camera cam;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
        }

        // ── Side-scrolling mode ─────────────────────────────────────────────
        public bool Side { get; private set; }
        public const float SideSize = 3.8f;      // orthographic half-height (metres)
        public const float SideFloorOffset = 1.52f; // camera height above the walking line (floor sits at ~30% of the screen)
        float minZ, maxZ, lead, sideY, sideFacing = 1f;

        public void EnableSide(float minZ, float maxZ)
        {
            Side = true;
            this.minZ = minZ;
            this.maxZ = maxZ;
            cam.orthographic = true;
            cam.orthographicSize = SideSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
        }

        public static Quaternion SideRotation => Quaternion.Euler(0f, -90f, 0f);

        public Vector3 SidePosition(float z, float floorY) => new Vector3(20f, floorY + SideFloorOffset, Mathf.Clamp(z, minZ, maxZ));

        void SideFollow(float dt, bool snap)
        {
            var subject = Target.GetComponent<Subject>();
            var fwd = Target.forward;
            if (Mathf.Abs(fwd.z) > 0.3f) sideFacing = Mathf.Sign(fwd.z);
            lead = snap ? sideFacing * 2.5f : Mathf.MoveTowards(lead, sideFacing * 2.5f, dt * 3f);
            // Follow the ground height quickly, but ignore the bounce of a jump.
            bool grounded = !subject || subject.Grounded;
            float y = Target.position.y;
            sideY = snap ? y : Mathf.Lerp(sideY, grounded || y < sideY ? y : sideY, 1f - Mathf.Exp(-dt * 5f));
            var desired = SidePosition(Target.position.z + lead, sideY);
            transform.position = snap ? desired : Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.14f, Mathf.Infinity, dt);
            transform.rotation = SideRotation;
            if (shake > 0f)
            {
                var r = Random.insideUnitCircle * shake * 0.2f;
                transform.position += new Vector3(0f, r.y, r.x);
                shake = Mathf.Max(0f, shake - dt * 2.5f);
            }
        }

        public void SnapBehind()
        {
            if (!Target) return;
            if (Side) { velocity = Vector3.zero; SideFollow(0f, true); return; }
            yaw = Target.eulerAngles.y;
            transform.position = Desired(out _);
            transform.LookAt(Focus());
            velocity = Vector3.zero;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        Vector3 Focus() => Target.position + Vector3.up * 1.35f + Target.forward * 0.8f;

        Vector3 Desired(out Vector3 pivot)
        {
            pivot = Target.position + Vector3.up * 1.6f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var offset = rot * new Vector3(0.75f, 0.35f, -distance);
            var desired = pivot + offset;
            var dir = desired - pivot;
            if (Physics.SphereCast(pivot, 0.25f, dir.normalized, out var hit, dir.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                desired = pivot + dir.normalized * Mathf.Max(0.6f, hit.distance - 0.1f);
            return desired;
        }

        void LateUpdate()
        {
            if (Manual || !Target) return;
            float dt = Time.unscaledDeltaTime;
            if (Side) { SideFollow(dt, false); return; }

            bool input = false;
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * 3.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.4f, -5f, 55f);
                input = true;
            }
            if (Input.GetKey(KeyCode.Q)) { yaw -= 90f * dt; input = true; }
            if (Input.GetKey(KeyCode.E)) { yaw += 90f * dt; input = true; }
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * 0.5f, 2.8f, 10f);

            idle = input ? 0f : idle + dt;
            var subject = Target.GetComponent<Subject>();
            if (idle > 1.5f && subject && subject.PlanarSpeed > 0.5f)
                yaw = Mathf.LerpAngle(yaw, Target.eulerAngles.y, dt * 1.6f);

            var desired = Desired(out _);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.18f, Mathf.Infinity, dt);
            var look = Quaternion.LookRotation(Focus() - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-dt * 10f));

            if (shake > 0f)
            {
                transform.position += Random.insideUnitSphere * shake * 0.25f;
                shake = Mathf.Max(0f, shake - dt * 2.5f);
            }
        }
    }
}
