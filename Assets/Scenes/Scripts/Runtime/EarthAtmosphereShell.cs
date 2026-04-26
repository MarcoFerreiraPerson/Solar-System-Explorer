using UnityEngine;
using UnityEngine.Rendering;

namespace SolarSystemExplorer.Runtime
{
    public sealed class EarthAtmosphereShell : MonoBehaviour
    {
        private const string AtmosphereObjectName = "AtmosphereShell";
        private const string AtmosphereShaderResource = "Shaders/AtmosphereRimURP";
        private const string AtmosphereShaderName = "Custom/URP/AtmosphereRim";
        private const float AtmosphereScale = 1.08f;

        private static readonly Color AtmosphereColor = new Color(0.34f, 0.69f, 1f, 1f);
        private static readonly Color AtmosphereSunTint = new Color(0.95f, 0.97f, 1f, 1f);

        private Material runtimeMaterial;
        private Renderer atmosphereRenderer;

        internal void Build(float baseRadius)
        {
            if (baseRadius <= 0f)
            {
                return;
            }

            GameObject atmosphereObject = GetOrCreateAtmosphereObject();
            atmosphereObject.transform.localPosition = Vector3.zero;
            atmosphereObject.transform.localRotation = Quaternion.identity;
            atmosphereObject.transform.localScale = Vector3.one * (baseRadius * AtmosphereScale * 2f);
            atmosphereObject.layer = gameObject.layer;

            atmosphereRenderer = atmosphereObject.GetComponent<Renderer>();
            if (atmosphereRenderer == null)
            {
                atmosphereRenderer = atmosphereObject.AddComponent<MeshRenderer>();
            }

            Shader atmosphereShader = Resources.Load<Shader>(AtmosphereShaderResource);
            if (atmosphereShader == null)
            {
                atmosphereShader = Shader.Find(AtmosphereShaderName);
            }

            if (atmosphereShader == null)
            {
                Debug.LogWarning("EarthAtmosphereShell: AtmosphereRimURP shader not found; Earth atmosphere was not created.");
                atmosphereObject.SetActive(false);
                return;
            }

            DestroyRuntimeMaterial();
            runtimeMaterial = new Material(atmosphereShader)
            {
                name = "Earth_Atmosphere_Runtime"
            };

            ApplyMaterialSettings(runtimeMaterial);
            UpdateSunDirection();

            atmosphereRenderer.sharedMaterial = runtimeMaterial;
            atmosphereRenderer.shadowCastingMode = ShadowCastingMode.Off;
            atmosphereRenderer.receiveShadows = false;
            atmosphereObject.SetActive(true);
        }

        private void LateUpdate()
        {
            UpdateSunDirection();
        }

        private GameObject GetOrCreateAtmosphereObject()
        {
            Transform existing = transform.Find(AtmosphereObjectName);
            GameObject atmosphereObject = existing != null
                ? existing.gameObject
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);

            atmosphereObject.name = AtmosphereObjectName;
            atmosphereObject.transform.SetParent(transform, false);
            RemoveColliders(atmosphereObject);

            return atmosphereObject;
        }

        private static void ApplyMaterialSettings(Material material)
        {
            SetColor(material, "_BaseColor", AtmosphereColor);
            SetColor(material, "_SunTint", AtmosphereSunTint);
            SetFloat(material, "_Intensity", 1.85f);
            SetFloat(material, "_RimPower", 3.2f);
            SetFloat(material, "_Alpha", 0.5f);
            SetFloat(material, "_DayStrength", 1.3f);
            SetFloat(material, "_NightStrength", 0.07f);
            SetFloat(material, "_TerminatorSharpness", 2.8f);
            SetFloat(material, "_NightRimFloor", 0.03f);
            SetFloat(material, "_NightAlphaFloor", 0.02f);
            SetFloat(material, "_SunScatterPower", 8f);
        }

        private void UpdateSunDirection()
        {
            if (runtimeMaterial == null || !runtimeMaterial.HasProperty("_SunDirection"))
            {
                return;
            }

            Vector3 sunDirection = RenderSettings.sun != null
                ? -RenderSettings.sun.transform.forward
                : Vector3.forward;
            runtimeMaterial.SetVector("_SunDirection", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
        }

        private static void RemoveColliders(GameObject target)
        {
            var colliders = target.GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                DestroyObject(colliders[i]);
            }
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private void DestroyRuntimeMaterial()
        {
            DestroyObject(runtimeMaterial);
            runtimeMaterial = null;
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeMaterial();
        }
    }
}
