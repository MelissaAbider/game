using UnityEngine;

namespace EchoShift
{
    /// <summary>The character the player commands by voice. Physics runs on scaled time (voice-time can freeze it).</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Subject : MonoBehaviour
    {
        public const float StandHeight = 1.8f;
        public const float CrouchHeight = 1.05f;
        public const float Radius = 0.32f;
        public const float WalkSpeed = 2.3f;
        public const float RunSpeed = 4.2f;
        public const float SneakSpeed = 1.2f;

        public CharacterController Controller { get; private set; }
        public SubjectRig Rig { get; private set; }
        public bool Crouching { get; private set; }
        public bool Grounded => Controller.isGrounded;
        public float PlanarSpeed { get; private set; }
        public float VerticalVelocity => verticalVelocity;
        public bool Shielded { get; set; }

        Vector3 desired;
        float verticalVelocity;
        float targetYaw;
        GameObject shieldBubble;

        public float Height => Crouching ? CrouchHeight : StandHeight;
        public Vector3 Head => transform.position + Vector3.up * (Height - 0.15f);

        /// <summary>Axis-aligned bounds of the body, used for precise hazard checks.</summary>
        public Bounds HitBounds => new Bounds(transform.position + Vector3.up * (Height * 0.5f), new Vector3(Radius * 2f, Height, Radius * 2f));

        void Awake()
        {
            Controller = GetComponent<CharacterController>();
            Controller.height = StandHeight;
            Controller.radius = Radius;
            Controller.center = new Vector3(0, StandHeight * 0.5f, 0);
            Controller.stepOffset = 0.36f;
            Controller.slopeLimit = 50f;
            Controller.skinWidth = 0.04f;
            Controller.minMoveDistance = 0f;
            Rig = SubjectRig.Build(transform, false);
            targetYaw = transform.eulerAngles.y;
            gameObject.layer = 2; // Ignore Raycast: the camera never collides with its own target
        }

        /// <summary>True during the "what are you doing?" reaction: the subject ignores movement orders.</summary>
        public bool Reacting { get; private set; }

        public void SetConfused(bool on, Vector3 faceTowards)
        {
            desired = Vector3.zero;
            if (on)
            {
                Stand();
                FaceDirection(faceTowards);
            }
            Reacting = on;
            Rig.Confused = on;
        }

        public void Move(Vector3 direction, float speed)
        {
            if (Reacting) { desired = Vector3.zero; return; }
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) { desired = Vector3.zero; return; }
            direction.Normalize();
            desired = direction * speed;
            FaceDirection(direction);
        }

        public void Stop() => desired = Vector3.zero;

        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-4f) targetYaw = Quaternion.LookRotation(direction).eulerAngles.y;
        }

        public void FaceYaw(float yaw) => targetYaw = yaw;
        public float Yaw => transform.eulerAngles.y;
        public bool Facing(float yaw) => Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) < 4f;

        public bool Jump()
        {
            if (Reacting || !Grounded) return false;
            Stand();
            verticalVelocity = 7.2f;
            Sfx.PlayAt("jump", transform.position, 0.6f);
            return true;
        }

        public void Crouch()
        {
            if (Reacting || Crouching) return;
            Crouching = true;
            Controller.height = CrouchHeight;
            Controller.center = new Vector3(0, CrouchHeight * 0.5f, 0);
        }

        public void Stand()
        {
            if (!Crouching) return;
            Crouching = false;
            Controller.height = StandHeight;
            Controller.center = new Vector3(0, StandHeight * 0.5f, 0);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            Controller.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Controller.enabled = true;
            targetYaw = yaw;
            verticalVelocity = 0f;
            desired = Vector3.zero;
            Stand();
        }

        public void SetShield(bool on)
        {
            Shielded = on;
            if (on && !shieldBubble)
            {
                shieldBubble = Prims.Make("Shield", PrimitiveType.Sphere, transform, new Vector3(0, 0.95f, 0), new Vector3(1.5f, 2.1f, 1.5f),
                    Mats.Hologram(Mats.Green, 0.8f, 0.18f), false, false);
            }
            if (shieldBubble) shieldBubble.SetActive(on);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (Controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity -= 22f * dt;
            var motion = desired;
            motion.y = verticalVelocity;
            Controller.Move(motion * dt);

            var v = Controller.velocity;
            PlanarSpeed = new Vector2(v.x, v.z).magnitude;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0, targetYaw, 0), 520f * dt);
            Rig.Drive(PlanarSpeed, Controller.isGrounded, Crouching, verticalVelocity, dt);
            if (shieldBubble && shieldBubble.activeSelf) shieldBubble.transform.Rotate(0, 40f * dt, 0);
        }
    }
}
