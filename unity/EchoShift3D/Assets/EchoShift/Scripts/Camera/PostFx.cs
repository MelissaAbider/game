using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EchoShift
{
    /// <summary>Global URP volume: neon bloom, ACES, grain, and the desaturated "frozen time" look.</summary>
    public static class PostFx
    {
        static ColorAdjustments color;
        static ChromaticAberration chroma;
        static LensDistortion lens;
        static Vignette vignette;
        static float frozen, pulse;
        static Color pulseColor = Color.black;

        public static void Setup(Camera camera)
        {
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var go = new GameObject("PostFX Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 1.35f;
            bloom.threshold.value = 0.95f;
            bloom.scatter.value = 0.72f;
            bloom.highQualityFiltering.value = true;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.value = 0.35f;
            color.contrast.value = 14f;
            color.saturation.value = 6f;
            color.colorFilter.value = Color.white;

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.34f;
            vignette.smoothness.value = 0.45f;
            vignette.color.value = Color.black;

            chroma = profile.Add<ChromaticAberration>(true);
            chroma.intensity.value = 0.07f;

            lens = profile.Add<LensDistortion>(true);
            lens.intensity.value = 0f;

            var grain = profile.Add<FilmGrain>(true);
            grain.type.value = FilmGrainLookup.Thin1;
            grain.intensity.value = 0.18f;
            grain.response.value = 0.8f;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.015f, 0.025f);
        }

        /// <summary>0 = normal, 1 = time frozen (desaturated, blue, aberrated).</summary>
        public static void SetFrozen(float amount)
        {
            frozen = Mathf.Clamp01(amount);
            Apply();
        }

        /// <summary>Full-screen colored vignette flash (damage, rewind, shatter). Call Tick every frame.</summary>
        public static void Pulse(Color c, float strength)
        {
            pulseColor = c;
            pulse = Mathf.Max(pulse, strength);
        }

        public static void Tick(float unscaledDt)
        {
            if (pulse <= 0f) return;
            pulse = Mathf.Max(0f, pulse - unscaledDt * 1.8f);
            Apply();
        }

        static void Apply()
        {
            if (color == null) return;
            color.saturation.value = Mathf.Lerp(6f, -88f, frozen);
            color.colorFilter.value = Color.Lerp(Color.white, new Color(0.72f, 0.88f, 1.15f), frozen);
            chroma.intensity.value = Mathf.Lerp(0.07f, 0.6f, Mathf.Max(frozen, pulse * 0.6f));
            lens.intensity.value = Mathf.Lerp(0f, -0.22f, frozen);
            vignette.color.value = Color.Lerp(Color.black, pulseColor, Mathf.Clamp01(pulse));
            vignette.intensity.value = Mathf.Lerp(0.34f, 0.55f, Mathf.Max(frozen * 0.6f, pulse));
        }
    }
}
