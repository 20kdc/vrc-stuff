using UnityEngine;
using VRC.SDKBase;

namespace KDCVRCTools {
	/**
	 * The 'Save Probe To Material' component writes information about a ReflectionProbe into a material on build/play.
	 * The material must have _Cubemap and _CubemapOrigin properties, which are written to.
	 */
	[AddComponentMenu("KDCVRCTools/KDC Save Reflection Probe To Material")]
	[RequireComponent(typeof(ReflectionProbe))]
	public class KDCSaveProbeToMaterial : KDCHelperBase {
		[Tooltip("Material. Must have _Cubemap and _CubemapOrigin properties, which are written to.")]
		public LazyLoadReference<Material> material;
		[Tooltip("Projection mode. This sets _PSRPlane, if enabled.")]
		public PSRAxis projectionMode;
		[Tooltip("PSR projection offset (from probe's position). Auto-recalculated into origin/breadth.")]
		public float projectionOffsetNegative, projectionOffsetPositive;

		void OnDrawGizmosSelected() {
			if (projectionMode != PSRAxis.None) {
				Vector3 primary = new Vector3(1, 0, 0);
				Vector3 quadA = new Vector3(0, 1, 0);
				Vector3 quadB = new Vector3(0, 0, 1);
				if (projectionMode == PSRAxis.Y) {
					quadA = primary;
					primary = new Vector3(0, 1, 0);
				} else if (projectionMode == PSRAxis.Z) {
					quadB = primary;
					primary = new Vector3(0, 0, 1);
				}
				quadA *= 0.25f;
				quadB *= 0.25f;
				Gizmos.color = new Color(1, 1, 0, 0.75F);
				Vector3 negPlaneLocus = transform.position - (primary * projectionOffsetNegative);
				Vector3 posPlaneLocus = transform.position + (primary * projectionOffsetPositive);
				Gizmos.DrawLine(negPlaneLocus, posPlaneLocus);
				for (int i = 0; i < 2; i++) {
					Vector3 locus = i == 0 ? negPlaneLocus : posPlaneLocus;
					Vector3 quad1 = (locus - quadA) - quadB;
					Vector3 quad2 = (locus - quadA) + quadB;
					Vector3 quad3 = (locus + quadA) - quadB;
					Vector3 quad4 = (locus + quadA) + quadB;
					Gizmos.DrawLine(quad1, quad2);
					Gizmos.DrawLine(quad3, quad4);
					Gizmos.DrawLine(quad1, quad3);
					Gizmos.DrawLine(quad2, quad4);
				}
			}
		}

		public override bool OnPreprocess() {
			var c = GetComponent<ReflectionProbe>();
			var m = material.asset;
			if (m != null && c != null) {
				m.SetTexture("_Cubemap", c.bakedTexture);
				m.SetVector("_CubemapOrigin", transform.position);
				if (projectionMode != PSRAxis.None) {
					float position = transform.position.x;
					int mode = 0;
					if (projectionMode == PSRAxis.Y) {
						position = transform.position.y;
						mode = 1;
					} else if (projectionMode == PSRAxis.Z) {
						position = transform.position.z;
						mode = 2;
					}
					float vNeg = position - projectionOffsetNegative;
					float vPos = position + projectionOffsetPositive;
					float origin = (vNeg + vPos) / 2;
					float breadth = vPos - origin;
					m.SetInt("_PSRPlane", mode);
					m.SetFloat("_PSRPlaneOrigin", origin);
					m.SetFloat("_PSRPlaneBreadth", breadth);
				}
			}
			return base.OnPreprocess();
		}

		public enum PSRAxis {
			None, X, Y, Z
		}
	}
}
