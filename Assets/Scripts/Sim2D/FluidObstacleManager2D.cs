using System.Collections.Generic;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	public class FluidObstacleManager2D : MonoBehaviour
	{
		public bool autoFindObstacles = true;
		public bool includeInactive;
		public FluidObstacle2D[] obstacles = System.Array.Empty<FluidObstacle2D>();

		public int CollectObstacleData(List<FluidObstacle2D.ObstacleData> results)
		{
			if (autoFindObstacles)
			{
				RefreshObstacles();
			}

			results.Clear();

			for (int i = 0; i < obstacles.Length; i++)
			{
				FluidObstacle2D obstacle = obstacles[i];
				if (obstacle == null)
				{
					continue;
				}

				if (!includeInactive && !obstacle.isActiveAndEnabled)
				{
					continue;
				}

				if (obstacle.TryBuildData(out FluidObstacle2D.ObstacleData data))
				{
					results.Add(data);
				}
			}

			return results.Count;
		}

		public void RefreshObstacles()
		{
			obstacles = FindObjectsOfType<FluidObstacle2D>(includeInactive);
		}

		void Reset()
		{
			RefreshObstacles();
		}

		void OnValidate()
		{
			if (autoFindObstacles)
			{
				RefreshObstacles();
			}
		}
	}
}
