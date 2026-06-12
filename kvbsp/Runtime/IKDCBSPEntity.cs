using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * This is a MonoBehaviour put on the root of an entity prefab.
	 * They define everything about the entity prefab's compilation (apart from initial instantiation).
	 * There may only be one of these.
	 * Philosophically, these should be editor-only, but it's not a necessary guarantee.
	 * UdonSharp integration in particular 'prefers' these not to be editor-only.
	 */
	public interface IKDCBSPEntity {

		/// Entity compile.
		/// If the behaviour goes missing (becomes '== null' according to Unity), it is assumed that compilation of this entity has been denied.
		/// See KDCBSPDelmeEntity for what that looks like.
		/// Note that all targetnames are resolved before compilation, so you can find them in the import context.
		public void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName);

		/// This is called after ALL entities have been built.
		/// The MonoBehaviour may safely destroy itself without destroying the GameObject.
		public void EntityPostProcess(IKDCBSPImportContext importContext);

		/// If this entity would self-describe as a brush entity.
		/// This controls/will control FGD generation.
		public bool EntityFGDSolid {
			get;
		}

		/// This is run on the prefab during workspace build.
		public void EntityFGDAttributes(KDCBSPEntityDescriptor descriptor);
	}
}
