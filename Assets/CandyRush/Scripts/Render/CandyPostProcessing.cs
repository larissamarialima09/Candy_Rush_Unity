using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

namespace CandyRush.Integrated
{
    /// <summary>Built-in Post Processing Stack v2. One global volume, one layer per player camera.</summary>
    [DisallowMultipleComponent]
    public sealed class CandyPostProcessing : MonoBehaviour
    {
        public PostProcessProfile desktopProfile, mobileProfile;
        public PostProcessVolume Volume { get; private set; }
        PostProcessProfile activeProfile;
        int quality = -1;

        void Awake()
        {
            desktopProfile = Resources.Load<PostProcessProfile>("Candy/Visuals/DesktopPostProcessing");
            mobileProfile = Resources.Load<PostProcessProfile>("Candy/Visuals/MobilePostProcessing");
            var volumeObject = new GameObject("Candy Rush Post Processing");
            volumeObject.transform.SetParent(transform, false);
            volumeObject.layer = 0;
            Volume = volumeObject.AddComponent<PostProcessVolume>();
            Volume.isGlobal = true; Volume.priority = 0; Volume.weight = 1;
            ApplyQuality();
            var reflections = Resources.Load<Cubemap>("Candy/Visuals/CandyReflections");
            if (reflections != null)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflection = reflections;
                RenderSettings.reflectionIntensity = .8f;
            }
        }

        void Update() { if (quality != QualitySettings.GetQualityLevel()) ApplyQuality(); }
        void ApplyQuality()
        {
            quality = QualitySettings.GetQualityLevel();
            bool lightweight = Application.isMobilePlatform || quality < 3;
            var source = lightweight ? mobileProfile : desktopProfile;
            if (source == null)
            {
                Debug.LogError("Candy Rush visual profiles missing. Run CandyRush > Visuals > Create missing assets.", this);
                return;
            }
            if (activeProfile != null)
            {
                foreach (var setting in activeProfile.settings) VisualMaterialLibrary.Dispose(setting);
                VisualMaterialLibrary.Dispose(activeProfile);
            }
            activeProfile = Instantiate(source);
            activeProfile.settings.Clear();
            foreach (var setting in source.settings) activeProfile.settings.Add(Instantiate(setting));
            if (activeProfile.TryGetSettings<Bloom>(out var bloom)) bloom.enabled.Override(quality >= 2);
            Volume.sharedProfile = activeProfile;
            foreach (var layer in FindObjectsByType<PostProcessLayer>(FindObjectsSortMode.None)) ConfigureAntialiasing(layer, lightweight);
        }

        public static void ConfigureCamera(Camera camera)
        {
            var resources = Resources.Load<PostProcessResources>("Candy/Visuals/PostProcessResources");
            if (resources == null)
            {
                Debug.LogError("Candy Rush PostProcessResources missing. Run CandyRush > Visuals > Create missing assets.");
                return;
            }
            camera.allowHDR = true;
            var layer = camera.GetComponent<PostProcessLayer>();
            if (layer == null) layer = camera.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(resources);
            layer.volumeLayer = 1 << 0; layer.volumeTrigger = camera.transform;
            layer.fog.enabled = false; // Candy shaders already apply distance fog.
            layer.finalBlitToCameraTarget = true;
            ConfigureAntialiasing(layer, Application.isMobilePlatform || QualitySettings.GetQualityLevel() < 3);
        }

        static void ConfigureAntialiasing(PostProcessLayer layer, bool lightweight)
        {
            layer.antialiasingMode = lightweight ? PostProcessLayer.Antialiasing.FastApproximateAntialiasing
                : PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
            layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.Medium;
            layer.fastApproximateAntialiasing.fastMode = lightweight;
        }

        public static PostProcessProfile CreateProfile(bool mobile)
        {
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true); bloom.intensity.Override(mobile ? .18f : .3f);
            bloom.threshold.Override(1.15f); bloom.softKnee.Override(.6f);
            bloom.diffusion.Override(mobile ? 3 : 5); bloom.fastMode.Override(mobile);
            var grading = profile.AddSettings<ColorGrading>();
            grading.enabled.Override(true); grading.gradingMode.Override(GradingMode.HighDefinitionRange);
            grading.tonemapper.Override(Tonemapper.ACES); grading.postExposure.Override(.15f);
            grading.saturation.Override(4); grading.contrast.Override(5);
            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true); vignette.intensity.Override(.12f); vignette.smoothness.Override(.4f);
            return profile;
        }

        void OnDestroy()
        {
            if (activeProfile == null) return;
            foreach (var setting in activeProfile.settings) VisualMaterialLibrary.Dispose(setting);
            VisualMaterialLibrary.Dispose(activeProfile);
        }
    }
}
