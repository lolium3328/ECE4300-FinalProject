using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	/// <summary>
	/// 流体障碍物绑定器（Obstacle Binder）
	/// 作用：收集场景中所有障碍物的信息，并每帧更新 GPU 缓冲区，使流体模拟器能够感知并与障碍物交互。
	/// 这是连接"场景物体"（Unity Collider）和"GPU 流体模拟"的桥梁。
	/// </summary>
	[DefaultExecutionOrder(-100)] //确保脚本执行优先级
	public class FluidSim2DObstacleBinder : MonoBehaviour
	{
		const int updatePositionKernel = 7; // UpdatePosition 计算核的 ID，用于设置障碍物缓冲区绑定

		public FluidSim2D sim; 
		public bool useSceneObstacles = true; 
		public bool useLegacyObstacle = true;
		public FluidObstacleManager2D obstacleManager; 

		ComputeBuffer obstacleBuffer; 
		readonly List<FluidObstacle2D.ObstacleData> obstacleDataCache = new(); // 临时列表，每帧用于收集障碍物数据
		static readonly FluidObstacle2D.ObstacleData[] defaultObstacleData = new FluidObstacle2D.ObstacleData[1]; // 默认障碍物（当没有障碍物时的占位符）

		void Reset()
		{
			// 在编辑器中重置脚本时，自动寻找和缓存相关组件（避免手动拖拽）
			if (sim == null)
			{
				sim = GetComponent<FluidSim2D>(); // 获取同一 GameObject 上的 FluidSim2D
			}

			if (obstacleManager == null)
			{
				obstacleManager = FindObjectOfType<FluidObstacleManager2D>(); // 找场景中的障碍物管理器
			}
		}

		void OnEnable()
		{
			BindObstacles();
		}

		void Update()
		{
			BindObstacles();
		}

		void BindObstacles()
		{
			// 此方法是核心：将所有障碍物数据打包成 GPU 可以理解的格式，传给 ComputeShader

			// 安全检查：确保流体模拟器和其 ComputeShader 存在
			if (sim == null || sim.compute == null)
			{
				return;
			}

			// 1. 收集所有障碍物的数据（从场景或管理器）
			CollectObstacleData();

			// 2. 获取有效的障碍物数量，并确保 GPU 缓冲区足够大
			int obstacleCount = obstacleDataCache.Count;
			bool bufferRecreated = ComputeHelper.CreateStructuredBuffer<FluidObstacle2D.ObstacleData>(
				ref obstacleBuffer, 
				Mathf.Max(1, obstacleCount) // 至少保留 1 个槽位（GPU 不允许空缓冲区）
			);

			// 3. 把障碍物数据复制到 GPU 缓冲区
			if (obstacleCount > 0)
			{
				obstacleBuffer.SetData(obstacleDataCache); // 有真实障碍物，传实际数据
			}
			else
			{
				obstacleBuffer.SetData(defaultObstacleData); // 无障碍物，传占位符数据（防止 GPU 错误）
			}

			// 4. 如果缓冲区刚创建或重创，需要告诉 ComputeShader 这个新缓冲区
			if (bufferRecreated)
			{
				ComputeHelper.SetBuffer(sim.compute, obstacleBuffer, "Obstacles", updatePositionKernel);
				// 绑定到 updatePositionKernel（更新位置的计算核），这样粒子位置更新时能检查障碍物
			}

			// 5. 告诉 ComputeShader 有多少个有效的障碍物（它会在循环中用到）
			sim.compute.SetInt("obstacleCount", obstacleCount);
		}

		void CollectObstacleData()
		{
			// 此方法：遍历场景，找出所有需要的障碍物，并整理成列表

			// 清空缓存，准备放入新的障碍物数据
			obstacleDataCache.Clear();

			// ===== 情况 1：收集动态障碍物（FluidObstacle2D 组件）=====
			if (useSceneObstacles)
			{
				if (obstacleManager != null)
				{
					// 如果有管理器，通过管理器收集（更高效，可能有预优化）
					obstacleManager.CollectObstacleData(obstacleDataCache);
				}
				else
				{
					// 直接在场景中搜索所有 FluidObstacle2D 组件
					FluidObstacle2D[] sceneObstacles = FindObjectsOfType<FluidObstacle2D>(false);
					for (int i = 0; i < sceneObstacles.Length; i++)
					{
						if (sceneObstacles[i].TryBuildData(out FluidObstacle2D.ObstacleData data))
						{
							obstacleDataCache.Add(data); // 有效则加入列表
						}
					}
				}
			}

			// ===== 情况 2：收集旧式内置障碍物（向后兼容）=====
			// FluidSim2D 本身有 obstacleSize 和 obstacleCentre 参数，可以作为一个固定的矩形障碍物
			if (useLegacyObstacle && sim.obstacleSize.x > 0 && sim.obstacleSize.y > 0)
			{
				obstacleDataCache.Add(new FluidObstacle2D.ObstacleData
				{
					shapeType = (int)FluidObstacle2D.ObstacleShapeType.Box, // 矩形形状
					center = new Unity.Mathematics.float2(sim.obstacleCentre.x, sim.obstacleCentre.y), // 中心位置
					halfExtents = new Unity.Mathematics.float2(sim.obstacleSize.x * 0.5f, sim.obstacleSize.y * 0.5f), // 半宽、半高
					rotationRadians = 0, // 不旋转
					radius = 0, // 矩形无需圆形半径
					collisionDamping = sim.collisionDamping, // 使用模拟器的阻尼参数
				});
			}
		}

		void OnDestroy()
		{
			// 脚本销毁或游戏结束时，释放 GPU 缓冲区内存（重要，避免内存泄漏）
			ComputeHelper.Release(obstacleBuffer);
		}
	}
}
