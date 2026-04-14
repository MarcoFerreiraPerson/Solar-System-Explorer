using SolarSystemExplorer.Runtime.WebGL.CpuPlanetCompute;
using UnityEngine;

[CreateAssetMenu (menuName = "Celestial Body/Earth-Like/Earth Shape")]
public class EarthShape : CelestialBodyShape {

	[Header ("Continent settings")]
	public float oceanDepthMultiplier = 5;
	public float oceanFloorDepth = 1.5f;
	public float oceanFloorSmoothing = 0.5f;

	public float mountainBlend = 1.2f; // Determines how smoothly the base of mountains blends into the terrain

	[Header ("Noise settings")]
	public SimpleNoiseSettings continentNoise;
	public SimpleNoiseSettings maskNoise;

	public RidgeNoiseSettings ridgeNoise;
	public Vector4 testParams;

	public override bool SupportsCpuGeneration {
		get {
			return true;
		}
	}

	protected override void SetShapeData () {
		var prng = new PRNG (seed);
		continentNoise.SetComputeValues (heightMapCompute, prng, "_continents");
		ridgeNoise.SetComputeValues (heightMapCompute, prng, "_mountains");
		maskNoise.SetComputeValues (heightMapCompute, prng, "_mask");

		heightMapCompute.SetFloat ("oceanDepthMultiplier", oceanDepthMultiplier);
		heightMapCompute.SetFloat ("oceanFloorDepth", oceanFloorDepth);
		heightMapCompute.SetFloat ("oceanFloorSmoothing", oceanFloorSmoothing);
		heightMapCompute.SetFloat ("mountainBlend", mountainBlend);
		heightMapCompute.SetVector ("params", testParams);

		//

	}

	public override void CalculateHeightsCpu (Vector3[] vertices, float[] heights) {
		var kernel = new CpuEarthHeightKernel (this, seed);
		kernel.CalculateHeights (vertices, heights);
	}

	public override bool PerturbVerticesCpu (Vector3[] vertices, float maxPerturbStrength) {
		if (!perturbVertices) {
			return false;
		}

		var kernel = CpuPlanetKernelFactory.CreatePerturbKernel (perturbCompute);
		if (kernel == null) {
			return false;
		}

		kernel.Perturb (vertices, maxPerturbStrength);
		return true;
	}

}
