using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/// Kind of similar to KDCBSPBrushEntitySettings.
	[System.Serializable]
	public class KDCBSPEntityHeader {
		// -- Box --

		[Tooltip("If this entity has an in-editor bounding box.")]
		[SerializeField]
		public bool hasBox = false;

		[Tooltip("Map editor box bounds. These are in Quake units and coordinates.")]
		[SerializeField]
		public Vector3 boxMin = new Vector3(-16, -16, -16), boxMax = new Vector3(16, 16, 16);

		// -- Colour --

		[Tooltip("Map editor colour enabled?")]
		[SerializeField]
		public bool hasColour = false;

		[Tooltip("Map editor colour.")]
		[SerializeField]
		public Color32 colour = new Color32(255, 255, 255, 255);

		// -- Model --

		[Tooltip("Map editor model.")]
		[SerializeField]
		public string model = "";

		// -- Universal --

		[Tooltip("If this entity is a brush entity.")]
		[SerializeField]
		public bool isBrushEntity = false;

		[Tooltip("Map editor description.")]
		[SerializeField]
		public string description = "An undescribed entity.";

		public void CopyTo(KDCBSPEntityDescriptor descriptor) {
			if (hasBox)
				descriptor.Box(boxMin.x, boxMin.y, boxMin.z, boxMax.x, boxMax.y, boxMax.z);
			if (hasColour)
				descriptor.Colour(colour.r, colour.g, colour.b);
			if (model != "")
				descriptor.Model(model);

			descriptor.isSolid = isBrushEntity;
			descriptor.description = description;
		}
	}
}
