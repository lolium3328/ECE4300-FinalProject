using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	[DefaultExecutionOrder(100)]
	public class FluidForceGrid2D : MonoBehaviour
	{
		const int computeBodyForcesKernel = 0;

		[Header("References")]
		public FluidSim2D sim;
		public ComputeShader compute;
		public FluidDynamicBodyManager2D bodyManager;

		[Header("Sampling")]
		public bool autoFindBodies = true;
		public float gridCellSize = 0;
		public float pressureClamp = 1000;
		public int maxSamplesPerAxis = 32;

		[Header("Debug")]
		public bool applyForces = true;

		ComputeBuffer bodyBuffer;
		ComputeBuffer bodyForceBuffer;
		readonly List<FluidDynamicBody2D.BodyData> bodyDataCache = new();
		readonly List<FluidDynamicBody2D> bodyRefsCache = new();
		FluidDynamicBody2D.BodyForceData[] bodyForcesCache = System.Array.Empty<FluidDynamicBody2D.BodyForceData>();
		static readonly FluidDynamicBody2D.BodyData[] defaultBodyData = new FluidDynamicBody2D.BodyData[1];

		void Reset()
		{
			if (sim == null)
			{
				sim = GetComponent<FluidSim2D>();
			}

			if (bodyManager == null)
			{
				bodyManager = FindObjectOfType<FluidDynamicBodyManager2D>();
			}
		}

		void LateUpdate()
		{
			if (!CanRun())
			{
				return;
			}

			float cellSize = GetCellSize();
			CollectBodies(cellSize);
			UploadBuffers();
			Dispatch(cellSize);
			ReadBackAndApply();
		}

		bool CanRun()
		{
			return sim != null &&
				compute != null &&
				sim.positionBuffer != null &&
				sim.densityBuffer != null &&
				sim.spatialKeysBuffer != null &&
				sim.spatialOffsetsBuffer != null &&
				sim.numParticles > 0;
		}

		float GetCellSize()
		{
			return gridCellSize > 0 ? gridCellSize : Mathf.Max(0.001f, sim.smoothingRadius);
		}

		void CollectBodies(float cellSize)
		{
			bodyDataCache.Clear();
			bodyRefsCache.Clear();

			if (bodyManager != null)
			{
				bodyManager.CollectBodyData(cellSize, bodyDataCache, bodyRefsCache);
				return;
			}

			if (!autoFindBodies)
			{
				return;
			}

			FluidDynamicBody2D[] bodies = FindObjectsOfType<FluidDynamicBody2D>(false);
			for (int i = 0; i < bodies.Length; i++)
			{
				if (bodies[i].TryBuildData(cellSize, out FluidDynamicBody2D.BodyData data))
				{
					bodyDataCache.Add(data);
					bodyRefsCache.Add(bodies[i]);
				}
			}
		}

		void UploadBuffers()
		{
			int bodyCount = bodyDataCache.Count;
			ComputeHelper.CreateStructuredBuffer<FluidDynamicBody2D.BodyData>(ref bodyBuffer, Mathf.Max(1, bodyCount));
			ComputeHelper.CreateStructuredBuffer<FluidDynamicBody2D.BodyForceData>(ref bodyForceBuffer, Mathf.Max(1, bodyCount));

			if (bodyCount > 0)
			{
				bodyBuffer.SetData(bodyDataCache);
			}
			else
			{
				bodyBuffer.SetData(defaultBodyData);
			}

			int forceCount = Mathf.Max(1, bodyCount);
			if (bodyForcesCache.Length != forceCount)
			{
				bodyForcesCache = new FluidDynamicBody2D.BodyForceData[forceCount];
			}
			else
			{
				System.Array.Clear(bodyForcesCache, 0, bodyForcesCache.Length);
			}
			bodyForceBuffer.SetData(bodyForcesCache, 0, 0, forceCount);
		}

		void Dispatch(float cellSize)
		{
			int bodyCount = bodyDataCache.Count;

			ComputeHelper.SetBuffer(compute, sim.positionBuffer, "Positions", computeBodyForcesKernel);
			ComputeHelper.SetBuffer(compute, sim.densityBuffer, "Densities", computeBodyForcesKernel);
			ComputeHelper.SetBuffer(compute, sim.spatialKeysBuffer, "SpatialKeys", computeBodyForcesKernel);
			ComputeHelper.SetBuffer(compute, sim.spatialOffsetsBuffer, "SpatialOffsets", computeBodyForcesKernel);
			ComputeHelper.SetBuffer(compute, bodyBuffer, "Bodies", computeBodyForcesKernel);
			ComputeHelper.SetBuffer(compute, bodyForceBuffer, "BodyForces", computeBodyForcesKernel);

			compute.SetInt("numParticles", sim.numParticles);
			compute.SetInt("numBodies", bodyCount);
			compute.SetFloat("smoothingRadius", sim.smoothingRadius);
			compute.SetFloat("targetDensity", sim.targetDensity);
			compute.SetFloat("pressureMultiplier", sim.pressureMultiplier);
			compute.SetFloat("nearPressureMultiplier", sim.nearPressureMultiplier);
			compute.SetFloat("gridCellSize", cellSize);
			compute.SetFloat("pressureClamp", pressureClamp);
			compute.SetInt("maxSamplesPerAxis", Mathf.Max(4, maxSamplesPerAxis));

			ComputeHelper.Dispatch(compute, Mathf.Max(1, bodyCount), kernelIndex: computeBodyForcesKernel);
		}

		void ReadBackAndApply()
		{
			int bodyCount = bodyDataCache.Count;
			if (bodyCount == 0)
			{
				return;
			}

			if (bodyForcesCache.Length != bodyCount)
			{
				bodyForcesCache = new FluidDynamicBody2D.BodyForceData[bodyCount];
			}

			bodyForceBuffer.GetData(bodyForcesCache, 0, 0, bodyCount);

			if (!applyForces)
			{
				return;
			}

			for (int i = 0; i < bodyCount; i++)
			{
				FluidDynamicBody2D body = bodyRefsCache[i];
				if (body != null)
				{
					body.ApplyForce(bodyForcesCache[i].force, bodyForcesCache[i].torque);
				}
			}
		}

		void OnDestroy()
		{
			ComputeHelper.Release(bodyBuffer, bodyForceBuffer);
		}
	}
}
