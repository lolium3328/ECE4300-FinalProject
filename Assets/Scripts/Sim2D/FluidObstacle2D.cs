using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	[DisallowMultipleComponent]
	public class FluidObstacle2D : MonoBehaviour
	{
		public enum ObstacleShapeType
		{
			Box = 0,
			Circle = 1,
		}

		[Header("Fluid Obstacle")]
		public bool affectFluid = true;
		[Range(0, 1)] public float collisionDamping = 0.95f;
		public bool autoDetectCollider = true;

		[Header("Optional References")]
		public Collider2D sourceCollider;
		public SpriteRenderer spriteRenderer;

		public bool HasSupportedCollider =>
			TryGetSupportedCollider(out _);

		public bool TryBuildData(out ObstacleData data)
		{
			data = default;

			if (!affectFluid)
			{
				return false;
			}

			if (!TryGetSupportedCollider(out Collider2D collider))
			{
				return false;
			}

			if (collider is BoxCollider2D box)
			{
				BuildBoxData(box, out data);
				return true;
			}

			if (collider is CircleCollider2D circle)
			{
				BuildCircleData(circle, out data);
				return true;
			}

			return false;
		}

		void Reset()
		{
			CacheReferences();
		}

		void OnValidate()
		{
			CacheReferences();
		}

		void CacheReferences()
		{
			if (autoDetectCollider || sourceCollider == null)
			{
				sourceCollider = GetComponent<Collider2D>();
			}

			if (spriteRenderer == null)
			{
				spriteRenderer = GetComponent<SpriteRenderer>();
			}
		}

		bool TryGetSupportedCollider(out Collider2D collider)
		{
			collider = sourceCollider;

			if (collider == null && autoDetectCollider)
			{
				collider = GetComponent<Collider2D>();
				sourceCollider = collider;
			}

			return collider is BoxCollider2D || collider is CircleCollider2D;
		}

		void BuildBoxData(BoxCollider2D box, out ObstacleData data)
		{
			Transform t = box.transform;
			Vector3 centerWorld = t.TransformPoint(box.offset);
			Vector3 axisXWorld = t.TransformVector(new Vector3(box.size.x * 0.5f, 0));
			Vector3 axisYWorld = t.TransformVector(new Vector3(0, box.size.y * 0.5f));
			float2 center = new float2(centerWorld.x, centerWorld.y);
			float2 axisX = new float2(axisXWorld.x, axisXWorld.y);
			float2 axisY = new float2(axisYWorld.x, axisYWorld.y);

			data = new ObstacleData
			{
				shapeType = (int)ObstacleShapeType.Box,
				center = center,
				halfExtents = new float2(math.length(axisX), math.length(axisY)),
				rotationRadians = Mathf.Atan2(axisX.y, axisX.x),
				radius = 0,
				collisionDamping = collisionDamping,
			};
		}

		void BuildCircleData(CircleCollider2D circle, out ObstacleData data)
		{
			Transform t = circle.transform;
			Vector3 centerWorld = t.TransformPoint(circle.offset);
			float2 center = new float2(centerWorld.x, centerWorld.y);
			Vector3 lossyScale = t.lossyScale;
			float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));

			data = new ObstacleData
			{
				shapeType = (int)ObstacleShapeType.Circle,
				center = center,
				halfExtents = new float2(0, 0),
				rotationRadians = 0,
				radius = circle.radius * maxScale,
				collisionDamping = collisionDamping,
			};
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct ObstacleData
		{
			public int shapeType;
			public float2 center;
			public float2 halfExtents;
			public float rotationRadians;
			public float radius;
			public float collisionDamping;
		}
	}
}
