using System.Collections.Generic;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	public class FluidDynamicBodyManager2D : MonoBehaviour
	{
		public bool autoFindBodies = true;
		public bool includeInactive;
		public FluidDynamicBody2D[] bodies = System.Array.Empty<FluidDynamicBody2D>();

		public int CollectBodyData(float baseCellSize, List<FluidDynamicBody2D.BodyData> dataResults, List<FluidDynamicBody2D> bodyResults)
		{
			if (autoFindBodies)
			{
				RefreshBodies();
			}

			dataResults.Clear();
			bodyResults.Clear();

			for (int i = 0; i < bodies.Length; i++)
			{
				FluidDynamicBody2D body = bodies[i];
				if (body == null)
				{
					continue;
				}

				if (!includeInactive && !body.isActiveAndEnabled)
				{
					continue;
				}

				if (body.TryBuildData(baseCellSize, out FluidDynamicBody2D.BodyData data))
				{
					dataResults.Add(data);
					bodyResults.Add(body);
				}
			}

			return dataResults.Count;
		}

		public void RefreshBodies()
		{
			bodies = FindObjectsOfType<FluidDynamicBody2D>(includeInactive);
		}

		void Reset()
		{
			RefreshBodies();
		}

		void OnValidate()
		{
			if (autoFindBodies)
			{
				RefreshBodies();
			}
		}
	}
}
