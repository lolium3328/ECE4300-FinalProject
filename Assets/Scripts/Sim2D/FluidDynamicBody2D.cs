using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace Seb.Fluid2D.Simulation
{
	[DisallowMultipleComponent]
	public class FluidDynamicBody2D : MonoBehaviour
	{
		public enum BodyShapeType
		{
			Box = 0,
			Circle = 1,
		}

		[Header("Body")]
		public bool affectedByFluid = true;
		public bool autoDetectCollider = true;
		public Collider2D sourceCollider;
		public Rigidbody2D targetBody;

		[Header("Force Settings")]
		public float forceScale = 1;
		public float torqueScale = 1;
		public float sampleSpacingMultiplier = 1;
		public float boundaryBandMultiplier = 1.5f;
		public bool wakeOnForce = true;

		public bool TryBuildData(float baseCellSize, out BodyData data)
		{
			data = default;

			if (!affectedByFluid || !TryGetSupportedCollider(out Collider2D collider))
			{
				return false;
			}

			float sampleSpacing = Mathf.Max(0.001f, baseCellSize * Mathf.Max(0.25f, sampleSpacingMultiplier));
			float boundaryBand = Mathf.Max(sampleSpacing, baseCellSize * Mathf.Max(0.25f, boundaryBandMultiplier));

			if (collider is BoxCollider2D box)
			{
				BuildBoxData(box, sampleSpacing, boundaryBand, out data);
				return true;
			}

			if (collider is CircleCollider2D circle)
			{
				BuildCircleData(circle, sampleSpacing, boundaryBand, out data);
				return true;
			}

			return false;
		}

		public void ApplyForce(float2 force, float torque)
		{
			if (targetBody == null)
			{
				return;
			}

			if (wakeOnForce)
			{
				targetBody.WakeUp();
			}

			targetBody.AddForce(new Vector2(force.x, force.y), ForceMode2D.Force);
			targetBody.AddTorque(torque, ForceMode2D.Force);
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

			if (targetBody == null)
			{
				targetBody = GetComponent<Rigidbody2D>();
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

		void BuildBoxData(BoxCollider2D box, float sampleSpacing, float boundaryBand, out BodyData data)
		{
			Transform t = box.transform;
			Vector3 centerWorld = t.TransformPoint(box.offset);
			Vector3 axisXWorld = t.TransformVector(new Vector3(box.size.x * 0.5f, 0));
			Vector3 axisYWorld = t.TransformVector(new Vector3(0, box.size.y * 0.5f));
			float2 center = new float2(centerWorld.x, centerWorld.y);
			float2 axisX = new float2(axisXWorld.x, axisXWorld.y);
			float2 axisY = new float2(axisYWorld.x, axisYWorld.y);

			data = new BodyData
			{
				shapeType = (int)BodyShapeType.Box,
				center = center,
				halfExtents = new float2(math.length(axisX), math.length(axisY)),
				rotationRadians = Mathf.Atan2(axisX.y, axisX.x),
				radius = 0,
				sampleSpacing = sampleSpacing,
				boundaryBand = boundaryBand,
				forceScale = forceScale,
				torqueScale = torqueScale,
			};
		}

		void BuildCircleData(CircleCollider2D circle, float sampleSpacing, float boundaryBand, out BodyData data)
		{
			Transform t = circle.transform;
			Vector3 centerWorld = t.TransformPoint(circle.offset);
			float2 center = new float2(centerWorld.x, centerWorld.y);
			Vector3 lossyScale = t.lossyScale;
			float maxScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));

			data = new BodyData
			{
				shapeType = (int)BodyShapeType.Circle,
				center = center,
				halfExtents = new float2(0, 0),
				rotationRadians = 0,
				radius = circle.radius * maxScale,
				sampleSpacing = sampleSpacing,
				boundaryBand = boundaryBand,
				forceScale = forceScale,
				torqueScale = torqueScale,
			};
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BodyData
		{
			public int shapeType;
			public float2 center;
			public float2 halfExtents;
			public float rotationRadians;
			public float radius;
			public float sampleSpacing;
			public float boundaryBand;
			public float forceScale;
			public float torqueScale;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BodyForceData
		{
			public float2 force;
			public float torque;
			public float padding;
		}
	}
}
