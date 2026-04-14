using UnityEngine;

namespace SolarSystemExplorer.Runtime.WebGL.CpuPlanetCompute
{
    // CPU equivalent of the GPU height kernels declared per-body-shape.
    // Implementations reproduce the corresponding *.compute file so WebGL
    // (which has no compute shader support) can generate terrain heights.
    //
    // Input: vertices on the unit sphere (length == numVertices).
    // Output: heights[i] is the displacement to apply to vertices[i].
    public interface ICpuHeightKernel
    {
        void CalculateHeights(Vector3[] vertices, float[] heights);
    }

    // CPU equivalent of PerturbPoints[Platelike].compute.
    // Reads vertices and writes perturbed positions back to the same array.
    public interface ICpuPerturbKernel
    {
        void Perturb(Vector3[] vertices, float perturbStrength);
    }

    // CPU equivalent of the Shading data compute kernels (Earth/Moon shading).
    // Writes per-vertex biome / shading data (RGBA) into shadingData.
    public interface ICpuShadingDataKernel
    {
        void CalculateShadingData(Vector3[] vertices, Vector4[] shadingData);
    }
}
