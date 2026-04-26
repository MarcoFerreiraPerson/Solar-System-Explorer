using UnityEngine;

public abstract class CelestialBodyShape : ScriptableObject {

	public bool randomize;
	public int seed;
	public ComputeShader heightMapCompute;

	public bool perturbVertices;
	public ComputeShader perturbCompute;
	[Range (0, 1)]
	public float perturbStrength = 0.7f;

	public event System.Action OnSettingChanged;

	ComputeBuffer heightBuffer;

	public virtual bool SupportsCpuGeneration {
		get {
			return false;
		}
	}

	public virtual float[] CalculateHeights (ComputeBuffer vertexBuffer) {
		//Debug.Log (System.Environment.StackTrace);
		// Set data
		SetShapeData ();
		heightMapCompute.SetInt ("numVertices", vertexBuffer.count);
		heightMapCompute.SetBuffer (0, "vertices", vertexBuffer);
		ComputeHelper.CreateAndSetBuffer<float> (ref heightBuffer, vertexBuffer.count, heightMapCompute, "heights");

		// Run
		ComputeHelper.Run (heightMapCompute, vertexBuffer.count);

		// Get heights
		var heights = new float[vertexBuffer.count];
		heightBuffer.GetData (heights);
		return heights;
	}

	public virtual float[] CalculateHeightsCpu (Vector3[] vertices) {
		var heights = new float[vertices.Length];
		CalculateHeightsCpu (vertices, heights);
		return heights;
	}

	public virtual void CalculateHeightsCpu (Vector3[] vertices, float[] heights) {
		for (int i = 0; i < heights.Length; i++) {
			heights[i] = 1;
		}
	}

	public virtual bool PerturbVerticesCpu (Vector3[] vertices, float maxPerturbStrength) {
		return false;
	}

	public virtual void ReleaseBuffers () {
		ComputeHelper.Release (heightBuffer);
	}

	protected virtual void SetShapeData () {

	}

	protected virtual void OnValidate () {
		if (OnSettingChanged != null) {
			OnSettingChanged ();
		}
	}

}
