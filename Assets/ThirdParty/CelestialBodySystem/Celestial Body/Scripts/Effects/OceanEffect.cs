using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanEffect : PostProcessingEffect {

	Light light;

	public void Update (CelestialBodyGenerator generator, Shader shader) {
		if (material == null || material.shader != shader) {
			material = new Material (shader);
		}

		if (light == null) {
			// Originally: FindObjectOfType<SunShadowCaster>().GetComponent<Light>()
			// SunShadowCaster is in the original game layer, which we didn't import.
			// RenderSettings.sun is set by our SolarSystemBootstrap and points at
			// the directional sun light, which is what this effect wants.
			light = RenderSettings.sun;
		}

		Vector3 centre = generator.transform.position;
		float radius = generator.GetOceanRadius ();
		material.SetVector ("oceanCentre", centre);
		material.SetFloat ("oceanRadius", radius);

		material.SetFloat ("planetScale", generator.BodyScale);
		material.SetVector ("dirToSun", -light.transform.forward);
		generator.body.shading.SetOceanProperties (material);
	}

	public override Material GetMaterial () {
		return material;
	}

}
