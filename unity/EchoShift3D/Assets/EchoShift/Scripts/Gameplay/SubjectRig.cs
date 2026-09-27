using UnityEngine;

namespace EchoShift
{
    /// <summary>
    /// Subject 731: a sleek lab android built from primitives with a fully procedural animation
    /// (walk, run, crouch, jump, idle breathing, listening, interacting). Echo clones reuse it as a hologram.
    /// </summary>
    public sealed class SubjectRig : MonoBehaviour
    {
        Transform hips, torso, neck, head, lShoulder, rShoulder, lElbow, rElbow, lHip, rHip, lKnee, rKnee;
        Material glowMat;
        Color glowColor;
        float phase, idleTime, crouchBlend, airBlend, reach, listenBlend;
        float stepTimer;

        public bool Listening { get; set; }
        public bool Reaching { get; set; }
        /// <summary>Comic "what are you doing?" pose: hands on knees, elbows out, head tilted up at the camera.</summary>
        public bool Confused { get; set; }
        float confusedBlend;
        public bool Footsteps { get; set; } = true;

        public static SubjectRig Build(Transform parent, bool ghost, Color? accent = null)
        {
            var root = new GameObject(ghost ? "EchoCloneRig" : "SubjectRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<SubjectRig>();
            rig.Construct(ghost, accent ?? Mats.Cyan);
            return rig;
        }

        // Palette of the Subject 731 key art (home page): glossy white suit, graphite straps and pads,
        // black visor with angry cyan eyes, cyan trim lines, pink rim piping, red status lights.
        Material shell, grey, visor, trim, rim, red;

        void Construct(bool ghost, Color accent)
        {
            glowColor = accent;
            var pink = new Color(1f, 0.42f, 0.82f);
            // A touch of self-illumination keeps the suit reading white even in the dark, red-lit chambers.
            shell = ghost ? Mats.Hologram(accent, 1.2f, 0.35f) : Mats.Emissive(new Color(0.85f, 0.9f, 1f), 0.5f, new Color(0.96f, 0.97f, 1f));
            if (!ghost) { shell.SetFloat("_Smoothness", 0.62f); shell.SetFloat("_Metallic", 0f); }
            grey = ghost ? Mats.Hologram(accent, 0.6f, 0.25f) : Mats.Emissive(new Color(0.5f, 0.55f, 0.62f), 0.12f, new Color(0.42f, 0.45f, 0.5f));
            if (!ghost) grey.SetFloat("_Smoothness", 0.55f);
            visor = ghost ? Mats.Hologram(accent, 0.4f, 0.3f) : Mats.Solid(new Color(0.015f, 0.02f, 0.03f), 0.97f, 0.4f);
            trim = ghost ? Mats.Hologram(accent, 2f, 0.7f) : Mats.Emissive(Mats.Cyan, 2.4f, Mats.Cyan);
            rim = ghost ? Mats.Hologram(accent, 1.6f, 0.5f) : Mats.Emissive(pink, 1.6f, pink);
            red = ghost ? Mats.Hologram(accent, 1.6f, 0.5f) : Mats.Emissive(Mats.Red, 2.2f, Mats.Red);
            glowMat = ghost ? Mats.Hologram(accent, 3f, 0.9f) : Mats.Emissive(accent, 4f);
            bool shadows = !ghost;

            hips = Pivot("Hips", transform, new Vector3(0, 0.95f, 0));
            Part("Pelvis", PrimitiveType.Capsule, hips, Vector3.zero, new Vector3(0.34f, 0.13f, 0.24f), shell, shadows);
            // Belt with the red status lights and buckle.
            Part("Belt", PrimitiveType.Cylinder, hips, new Vector3(0, 0.06f, 0), new Vector3(0.36f, 0.035f, 0.26f), grey, shadows);
            Part("Buckle", PrimitiveType.Cube, hips, new Vector3(0, 0.06f, 0.13f), new Vector3(0.1f, 0.05f, 0.02f), grey, false);
            Part("BeltLightL", PrimitiveType.Cube, hips, new Vector3(-0.025f, 0.06f, 0.142f), new Vector3(0.025f, 0.022f, 0.01f), red, false);
            Part("BeltLightR", PrimitiveType.Cube, hips, new Vector3(0.025f, 0.06f, 0.142f), new Vector3(0.025f, 0.022f, 0.01f), red, false);
            Part("BeltSideL", PrimitiveType.Cube, hips, new Vector3(-0.17f, 0.06f, 0.05f), new Vector3(0.02f, 0.045f, 0.06f), red, false);
            Part("BeltSideR", PrimitiveType.Cube, hips, new Vector3(0.17f, 0.06f, 0.05f), new Vector3(0.02f, 0.045f, 0.06f), red, false);

            torso = Pivot("Torso", hips, new Vector3(0, 0.06f, 0));
            Part("Chest", PrimitiveType.Capsule, torso, new Vector3(0, 0.33f, 0), new Vector3(0.48f, 0.3f, 0.3f), shell, shadows);
            Part("Abdomen", PrimitiveType.Capsule, torso, new Vector3(0, 0.12f, 0.005f), new Vector3(0.36f, 0.15f, 0.26f), shell, shadows);
            // Cyan trim: chest plate outline (V) and side seams, pink rim piping along the flanks.
            Trim(torso, new Vector3(-0.1f, 0.3f, 0.148f), new Vector3(0.012f, 0.26f, 0.012f), -18f, trim);
            Trim(torso, new Vector3(0.1f, 0.3f, 0.148f), new Vector3(0.012f, 0.26f, 0.012f), 18f, trim);
            Trim(torso, new Vector3(0, 0.17f, 0.132f), new Vector3(0.15f, 0.012f, 0.012f), 0f, trim);
            Trim(torso, new Vector3(-0.235f, 0.3f, 0.02f), new Vector3(0.012f, 0.3f, 0.02f), 0f, rim);
            Trim(torso, new Vector3(0.235f, 0.3f, 0.02f), new Vector3(0.012f, 0.3f, 0.02f), 0f, rim);
            // Harness straps over the shoulders with red lights (like the key art).
            foreach (float side in new[] { -1f, 1f })
            {
                Part("Strap", PrimitiveType.Cube, torso, new Vector3(side * 0.14f, 0.4f, 0.142f), new Vector3(0.055f, 0.22f, 0.025f), grey, false);
                Part("StrapLight", PrimitiveType.Cube, torso, new Vector3(side * 0.14f, 0.44f, 0.157f), new Vector3(0.024f, 0.036f, 0.01f), red, false);
            }
            // Life-support backpack (what the third-person camera sees most) with hose to the helmet.
            Part("Pack", PrimitiveType.Capsule, torso, new Vector3(0, 0.35f, -0.19f), new Vector3(0.32f, 0.2f, 0.16f), grey, shadows);
            Part("PackPanel", PrimitiveType.Cube, torso, new Vector3(0, 0.36f, -0.268f), new Vector3(0.18f, 0.2f, 0.012f), shell, false);
            Trim(torso, new Vector3(-0.1f, 0.36f, -0.272f), new Vector3(0.018f, 0.2f, 0.01f), 0f, trim);
            Trim(torso, new Vector3(0.1f, 0.36f, -0.272f), new Vector3(0.018f, 0.2f, 0.01f), 0f, trim);
            Part("PackLight", PrimitiveType.Cube, torso, new Vector3(0, 0.49f, -0.272f), new Vector3(0.06f, 0.02f, 0.01f), red, false);
            var hose = Part("Hose", PrimitiveType.Cylinder, torso, new Vector3(0.13f, 0.56f, -0.14f), new Vector3(0.03f, 0.1f, 0.03f), grey, false);
            hose.transform.localRotation = Quaternion.Euler(-35f, 0f, -20f);
            Part("Collar", PrimitiveType.Cylinder, torso, new Vector3(0, 0.6f, 0), new Vector3(0.22f, 0.03f, 0.2f), grey, shadows);
            Part("CollarTrim", PrimitiveType.Cylinder, torso, new Vector3(0, 0.625f, 0), new Vector3(0.2f, 0.008f, 0.18f), trim, false);

            // Big round helmet, black visor with a cyan rim and two angry glowing eyes, ear pods.
            neck = Pivot("Neck", torso, new Vector3(0, 0.62f, 0));
            head = Pivot("Head", neck, new Vector3(0, 0.05f, 0));
            Part("Helmet", PrimitiveType.Sphere, head, new Vector3(0, 0.19f, 0), new Vector3(0.42f, 0.42f, 0.42f), shell, shadows);
            Part("VisorRim", PrimitiveType.Sphere, head, new Vector3(0, 0.175f, 0.085f), new Vector3(0.34f, 0.245f, 0.28f), trim, false);
            Part("Visor", PrimitiveType.Sphere, head, new Vector3(0, 0.175f, 0.094f), new Vector3(0.32f, 0.225f, 0.28f), visor, false);
            foreach (float side in new[] { -1f, 1f })
            {
                var eye = Part("Eye", PrimitiveType.Cube, head, new Vector3(side * 0.055f, 0.185f, 0.232f), new Vector3(0.065f, 0.024f, 0.02f), glowMat, false);
                eye.transform.localRotation = Quaternion.Euler(0f, side * 14f, side * 16f); // inner ends lower: the angry look
                var pod = Part("EarPod", PrimitiveType.Cylinder, head, new Vector3(side * 0.21f, 0.18f, 0f), new Vector3(0.1f, 0.022f, 0.1f), grey, false);
                pod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var podRing = Part("EarRing", PrimitiveType.Cylinder, head, new Vector3(side * 0.223f, 0.18f, 0f), new Vector3(0.065f, 0.005f, 0.065f), trim, false);
                podRing.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Trim(head, new Vector3(0, 0.39f, -0.02f), new Vector3(0.012f, 0.012f, 0.24f), 0f, trim); // crest line

            lShoulder = Pivot("ShoulderL", torso, new Vector3(-0.27f, 0.5f, 0));
            rShoulder = Pivot("ShoulderR", torso, new Vector3(0.27f, 0.5f, 0));
            BuildArm(lShoulder, out lElbow, -1f, shadows);
            BuildArm(rShoulder, out rElbow, 1f, shadows);

            lHip = Pivot("HipL", hips, new Vector3(-0.11f, -0.02f, 0));
            rHip = Pivot("HipR", hips, new Vector3(0.11f, -0.02f, 0));
            BuildLeg(lHip, out lKnee, shadows);
            BuildLeg(rHip, out rKnee, shadows);
        }

        void Trim(Transform parent, Vector3 pos, Vector3 size, float roll, Material mat)
            => Part("Trim", PrimitiveType.Cube, parent, pos, size, mat, false).transform.localRotation = Quaternion.Euler(0f, 0f, roll);

        void BuildArm(Transform shoulder, out Transform elbow, float side, bool shadows)
        {
            Part("ShoulderPad", PrimitiveType.Sphere, shoulder, Vector3.zero, new Vector3(0.2f, 0.17f, 0.2f), shell, shadows);
            Part("ShoulderPatch", PrimitiveType.Cube, shoulder, new Vector3(side * 0.085f, 0.02f, 0.02f), new Vector3(0.02f, 0.05f, 0.08f), trim, false);
            Part("UpperArm", PrimitiveType.Capsule, shoulder, new Vector3(0, -0.16f, 0), new Vector3(0.145f, 0.15f, 0.145f), shell, shadows);
            Trim(shoulder, new Vector3(side * 0.07f, -0.16f, 0), new Vector3(0.008f, 0.2f, 0.03f), 0f, rim);
            elbow = Pivot("Elbow", shoulder, new Vector3(0, -0.31f, 0));
            Part("ElbowBand", PrimitiveType.Cylinder, elbow, new Vector3(0, -0.01f, 0), new Vector3(0.15f, 0.03f, 0.15f), grey, false);
            Part("Forearm", PrimitiveType.Capsule, elbow, new Vector3(0, -0.15f, 0), new Vector3(0.145f, 0.15f, 0.145f), shell, shadows);
            Part("Wrist", PrimitiveType.Cylinder, elbow, new Vector3(0, -0.26f, 0), new Vector3(0.15f, 0.022f, 0.15f), trim, false);
            Part("Hand", PrimitiveType.Sphere, elbow, new Vector3(0, -0.33f, 0), new Vector3(0.12f, 0.13f, 0.1f), shell, shadows);
        }

        void BuildLeg(Transform hip, out Transform knee, bool shadows)
        {
            Part("Thigh", PrimitiveType.Capsule, hip, new Vector3(0, -0.22f, 0), new Vector3(0.19f, 0.22f, 0.19f), shell, shadows);
            Trim(hip, new Vector3(0, -0.22f, 0.097f), new Vector3(0.012f, 0.3f, 0.01f), 0f, trim);
            knee = Pivot("Knee", hip, new Vector3(0, -0.45f, 0));
            Part("KneePad", PrimitiveType.Sphere, knee, new Vector3(0, 0, 0.06f), new Vector3(0.14f, 0.14f, 0.08f), grey, false);
            Part("KneeRing", PrimitiveType.Sphere, knee, new Vector3(0, 0, 0.054f), new Vector3(0.158f, 0.158f, 0.07f), trim, false);
            Part("Shin", PrimitiveType.Capsule, knee, new Vector3(0, -0.21f, 0), new Vector3(0.16f, 0.21f, 0.16f), shell, shadows);
            Trim(knee, new Vector3(0, -0.22f, 0.081f), new Vector3(0.01f, 0.24f, 0.01f), 0f, rim);
            Part("Ankle", PrimitiveType.Cylinder, knee, new Vector3(0, -0.38f, 0), new Vector3(0.165f, 0.022f, 0.165f), trim, false);
            Part("Boot", PrimitiveType.Capsule, knee, new Vector3(0, -0.44f, 0.04f), new Vector3(0.17f, 0.08f, 0.17f), shell, shadows).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part("Sole", PrimitiveType.Cube, knee, new Vector3(0, -0.485f, 0.04f), new Vector3(0.16f, 0.02f, 0.3f), trim, false);
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static GameObject Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool shadows)
            => Prims.Make(name, type, parent, pos, scale, mat, false, shadows);

        public void SetGlow(Color color, float intensity)
        {
            glowColor = color;
            if (glowMat.HasProperty("_EmissionColor")) Mats.SetEmission(glowMat, color, intensity);
            else glowMat.SetColor("_BaseColor", color * intensity);
        }

        /// <summary>Advance the procedural animation. dt is scaled time so frozen time freezes the pose too.</summary>
        public void Drive(float planarSpeed, bool grounded, bool crouching, float verticalVelocity, float dt)
        {
            if (dt <= 0f) return;
            float moveAmt = Mathf.Clamp01(planarSpeed / 2.4f);
            float runAmt = Mathf.Clamp01((planarSpeed - 2.6f) / 1.6f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, crouching ? 1f : 0f, dt * 6f);
            airBlend = Mathf.MoveTowards(airBlend, grounded ? 0f : 1f, dt * 8f);
            reach = Mathf.MoveTowards(reach, Reaching ? 1f : 0f, dt * 5f);
            listenBlend = Mathf.MoveTowards(listenBlend, Listening ? 1f : 0f, dt * 4f);
            idleTime += dt;

            float stride = Mathf.Lerp(7.5f, 10.5f, runAmt);
            phase += dt * stride * Mathf.Max(moveAmt, 0.001f) * (crouching ? 0.7f : 1f);
            float s = Mathf.Sin(phase);
            float c = Mathf.Cos(phase);
            float swing = Mathf.Lerp(32f, 48f, runAmt) * moveAmt * (1f - airBlend);

            float bob = Mathf.Abs(s) * 0.045f * moveAmt;
            float breathe = Mathf.Sin(idleTime * 2.1f) * 0.008f * (1f - moveAmt);
            hips.localPosition = new Vector3(0, 0.95f - crouchBlend * 0.38f + bob + breathe, 0);
            hips.localRotation = Quaternion.Euler(0, s * 6f * moveAmt, 0);

            float lean = moveAmt * Mathf.Lerp(5f, 14f, runAmt) + crouchBlend * 24f - airBlend * 6f;
            torso.localRotation = Quaternion.Euler(lean, -s * 8f * moveAmt, 0);

            float look = Mathf.Sin(idleTime * 0.6f) * 12f * (1f - moveAmt) * (1f - listenBlend);
            neck.localRotation = Quaternion.Euler(-lean * 0.6f - listenBlend * 8f, look, listenBlend * 14f);

            // Legs: hips swing opposite, knees fold on the recovery phase.
            float crouchHip = -crouchBlend * 78f, crouchKnee = crouchBlend * 118f;
            float airHip = -airBlend * 55f, airKnee = airBlend * 85f;
            lHip.localRotation = Quaternion.Euler(s * swing + crouchHip + airHip, 0, 0);
            rHip.localRotation = Quaternion.Euler(-s * swing + crouchHip + airHip * 0.6f, 0, 0);
            lKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * swing * 1.4f + crouchKnee + airKnee, 0, 0);
            rKnee.localRotation = Quaternion.Euler(Mathf.Max(0f, -c) * swing * 1.4f + crouchKnee + airKnee * 0.7f, 0, 0);

            // Arms counter-swing; the right arm reaches forward when interacting.
            float armSwing = swing * 0.8f;
            float airArm = -airBlend * 70f;
            lShoulder.localRotation = Quaternion.Euler(-s * armSwing + airArm, 0, -6f - airBlend * 20f);
            rShoulder.localRotation = Quaternion.Euler(Mathf.Lerp(s * armSwing + airArm, -80f, reach), 0, 6f + airBlend * 20f);
            lElbow.localRotation = Quaternion.Euler(-(18f + moveAmt * 30f + crouchBlend * 30f), 0, 0);
            rElbow.localRotation = Quaternion.Euler(-Mathf.Lerp(18f + moveAmt * 30f + crouchBlend * 30f, 10f, reach), 0, 0);

            confusedBlend = Mathf.MoveTowards(confusedBlend, Confused ? 1f : 0f, dt * 5f);
            if (confusedBlend > 0.001f)
            {
                float k = Tween.OutBack(confusedBlend);
                float tsk = Mathf.Sin(idleTime * 4.5f) * 9f; // slow disapproving head shake
                hips.localPosition = Vector3.LerpUnclamped(hips.localPosition, new Vector3(0, 0.8f, -0.05f), k);
                hips.localRotation = Quaternion.SlerpUnclamped(hips.localRotation, Quaternion.identity, k);
                torso.localRotation = Quaternion.SlerpUnclamped(torso.localRotation, Quaternion.Euler(34f, 0, 0), k);
                neck.localRotation = Quaternion.SlerpUnclamped(neck.localRotation, Quaternion.Euler(-30f, tsk, 17f), k);
                lHip.localRotation = Quaternion.SlerpUnclamped(lHip.localRotation, Quaternion.Euler(-30f, 0, -5f), k);
                rHip.localRotation = Quaternion.SlerpUnclamped(rHip.localRotation, Quaternion.Euler(-30f, 0, 5f), k);
                lKnee.localRotation = Quaternion.SlerpUnclamped(lKnee.localRotation, Quaternion.Euler(44f, 0, 0), k);
                rKnee.localRotation = Quaternion.SlerpUnclamped(rKnee.localRotation, Quaternion.Euler(44f, 0, 0), k);
                // Arms reach down to the knees with the elbows flared out: the "triangle" arms.
                lShoulder.localRotation = Quaternion.SlerpUnclamped(lShoulder.localRotation, Quaternion.Euler(-34f, 0, -40f), k);
                rShoulder.localRotation = Quaternion.SlerpUnclamped(rShoulder.localRotation, Quaternion.Euler(-34f, 0, 40f), k);
                lElbow.localRotation = Quaternion.SlerpUnclamped(lElbow.localRotation, Quaternion.Euler(-62f, 0, 22f), k);
                rElbow.localRotation = Quaternion.SlerpUnclamped(rElbow.localRotation, Quaternion.Euler(-62f, 0, -22f), k);
            }

            float pulse = 3.2f + Mathf.Sin(idleTime * (Listening ? 9f : 2.5f)) * (Listening ? 1.6f : 0.6f);
            if (Confused) pulse = 4.5f + Mathf.Sin(idleTime * 14f) * 2f;
            if (glowMat.HasProperty("_EmissionColor")) Mats.SetEmission(glowMat, glowColor, pulse);

            if (Footsteps && grounded && moveAmt > 0.3f)
            {
                stepTimer -= dt * stride * moveAmt;
                if (stepTimer <= 0f)
                {
                    stepTimer = Mathf.PI;
                    Sfx.PlayAt("step", transform.position, 0.35f);
                }
            }
        }
    }
}
