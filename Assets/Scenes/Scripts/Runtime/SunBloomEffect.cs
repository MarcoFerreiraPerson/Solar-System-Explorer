using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UrpBloom = UnityEngine.Rendering.Universal.Bloom;

namespace SolarSystemExplorer.Runtime
{
    public sealed class SunBloomEffect : MonoBehaviour
    {
        private const string BloomVolumeName = "Sun Bloom Volume";
        private static readonly Color SunColor = new Color(1f, 0.85f, 0.45f, 1f);
        private static readonly Color BloomTint = new Color(1f, 0.88f, 0.55f, 1f);

        private Volume bloomVolume;
        private VolumeProfile runtimeProfile;

        internal void Initialize(GameObject sun)
        {
            if (sun == null)
            {
                return;
            }

            ConfigureSunMaterial(sun);
            EnsureBloomVolume();
            EnablePostProcessing(Camera.main);
        }

        private void LateUpdate()
        {
            EnablePostProcessing(Camera.main);
        }

        private static void ConfigureSunMaterial(GameObject sun)
        {
            var renderer = sun.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    return;
                }

                material = new Material(shader)
                {
                    name = "Sun_Emissive_Runtime"
                };
                renderer.sharedMaterial = material;
            }

            Color hdrSunColor = SunColor * 3.6f;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", hdrSunColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", hdrSunColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", hdrSunColor);
                material.EnableKeyword("_EMISSION");
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void EnsureBloomVolume()
        {
            if (bloomVolume == null)
            {
                GameObject volumeObject = new GameObject(BloomVolumeName);
                volumeObject.transform.SetParent(transform, false);
                bloomVolume = volumeObject.AddComponent<Volume>();
            }

            if (runtimeProfile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                runtimeProfile.name = "Sun_Bloom_Runtime_Profile";
                ConfigureBloom(runtimeProfile.Add<UrpBloom>(true));
            }

            bloomVolume.isGlobal = true;
            bloomVolume.priority = 100f;
            bloomVolume.weight = 1f;
            bloomVolume.sharedProfile = runtimeProfile;
        }

        private static void ConfigureBloom(UrpBloom bloom)
        {
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.05f);
            bloom.scatter.Override(0.62f);
            bloom.clamp.Override(65472f);
            bloom.tint.Override(BloomTint);
            bloom.highQualityFiltering.Override(false);
            bloom.maxIterations.Override(6);
        }

        private static void EnablePostProcessing(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = true;
        }

        private void OnDestroy()
        {
            if (runtimeProfile != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeProfile);
                }
                else
                {
                    DestroyImmediate(runtimeProfile);
                }
                runtimeProfile = null;
            }
        }
    }
}
