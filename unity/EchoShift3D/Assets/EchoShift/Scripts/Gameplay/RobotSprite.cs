using System;
using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Side-view body of Subject 731 (and echo clones): the Nano Banana hero render in side poses — idle, two run
    /// frames, jump, crouch and the confused coach stance — chosen from the physics state every frame.
    /// The procedural 3D rig keeps running underneath (footsteps, timing) but is hidden.
    /// </summary>
    public sealed class RobotSprite : MonoBehaviour
    {
        static Texture2D idle, runA, runB, jump, crouch, confused;

        Transform owner, quad, rig;
        Material mat;
        Func<(float speed, bool grounded, bool crouching, bool confused)> state;
        float stride, airTime, facing = 1f, bobTime;
        Vector3 lastPos;

        public static bool Ready => Load() && idle;

        static bool Load()
        {
            if (idle) return true;
            idle = HeroArt.Cutout("Game/robot_idle");
            runA = HeroArt.Cutout("Game/robot_run_a") ?? idle;
            runB = HeroArt.Cutout("Game/robot_run_b") ?? runA;
            jump = HeroArt.Cutout("Game/robot_jump") ?? runA;
            crouch = HeroArt.Cutout("Game/robot_crouch") ?? idle;
            confused = HeroArt.Cutout("Game/robot_confused") ?? idle;
            return idle;
        }

        public static RobotSprite Attach(Transform owner, Transform rigToHide, Color tint, Func<(float, bool, bool, bool)> state)
        {
            if (!Load()) return null;
            if (rigToHide)
                foreach (var r in rigToHide.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var go = new GameObject("RobotSprite");
            go.transform.SetParent(owner, false);
            var s = go.AddComponent<RobotSprite>();
            s.owner = owner;
            s.rig = rigToHide;
            s.state = state;
            s.mat = Mats.Hologram(Color.white, 1f, tint.a, false);
            s.mat.SetColor("_BaseColor", tint);
            s.quad = Prims.Make("Body", PrimitiveType.Quad, go.transform, Vector3.zero, Vector3.one, s.mat, false, false).transform;
            s.lastPos = owner.position;
            return s;
        }

        void LateUpdate()
        {
            // The hidden rig still drives visibility and the materialize animation (its Y scale).
            bool visible = !rig || rig.gameObject.activeInHierarchy;
            if (quad.gameObject.activeSelf != visible) quad.gameObject.SetActive(visible);
            if (!visible) return;
            float grow = rig ? rig.localScale.y : 1f;
            var (speed, grounded, crouching, isConfused) = state();
            float dt = Time.deltaTime;
            var pos = owner.position;
            stride += Mathf.Abs(pos.z - lastPos.z) + Mathf.Abs(pos.x - lastPos.x) * 0.5f;
            lastPos = pos;
            airTime = grounded ? 0f : airTime + dt;
            var fwd = owner.forward;
            if (Mathf.Abs(fwd.z) > 0.3f) facing = Mathf.Sign(fwd.z);

            Texture2D tex;
            float height;
            if (isConfused) { tex = confused; height = 1.55f; }
            else if (airTime > 0.08f) { tex = jump; height = 1.75f; }
            else if (crouching) { tex = crouch; height = 1.2f; }
            else if (speed > 0.35f) { tex = (int)(stride / 0.6f) % 2 == 0 ? runA : runB; height = 1.8f; }
            else { tex = idle; height = 1.85f; }

            height *= grow;
            bobTime += Time.unscaledDeltaTime;
            float bob = tex == idle ? Mathf.Sin(bobTime * 2.2f) * 0.012f : 0f;
            float width = height * tex.width / tex.height;
            if (mat.GetTexture("_BaseMap") != tex) mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", new Vector2(facing, 1f));
            mat.SetTextureOffset("_BaseMap", new Vector2(facing < 0f ? 1f : 0f, 0f));

            // Always face the side camera (+X), whatever way the body is turned in 3D.
            quad.SetPositionAndRotation(pos + new Vector3(0f, height * 0.5f - 0.03f + bob, 0f), Quaternion.Euler(0f, -90f, 0f));
            quad.localScale = new Vector3(width, height, 1f);
        }
    }
}
