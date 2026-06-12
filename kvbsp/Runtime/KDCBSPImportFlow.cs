using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * KDCBSPImportFlow contains almsot all of the import flow.
	 */
	public static class KDCBSPImportFlow {
		public static GameObject BuildMap(IKDCBSPImportContext importContext) {
			var data = importContext.BSP;

			List<KDCBSPAbstractEntity> postProcessThese = new();

			GameObject mapGO = CreateEntity(importContext, data.worldspawn, "worldspawn", "worldspawn ", null, postProcessThese);
			if (mapGO == null)
				throw new Exception("worldspawn being gone means something has gone horribly wrong, so rather than risking import corruption we choose to bail here");

			Dictionary<string, int> entCounters = new();
			foreach (var entity in data.entities) {
				string classname = entity["classname"];
				if (classname == "")
					classname = "func_unknown";
				if (entity == data.worldspawn)
					continue;
				// use per-classname counters to increase resilience
				if (!entCounters.ContainsKey(classname))
					entCounters[classname] = 0;
				int eid = entCounters[classname];
				entCounters[classname] = eid + 1;
				// ...
				CreateEntity(importContext, entity, classname, classname + " " + eid, mapGO, postProcessThese);
			}

			// entity tree is complete, postprocess/link
			foreach (var c in postProcessThese) {
				c.EntityPostProcess(importContext);
				UnityEngine.Object.DestroyImmediate((UnityEngine.Object) c);
			}
			return mapGO;
		}

		// -- Primary Entity Converter --

		/// Creates and returns an entity.
		public static GameObject InstantiateEntity(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string classname, string uniqueName, GameObject parent) {
			var worldScale = importContext.WorldScale;

			// Create the entity prefab.
			var prefab = importContext.LookupEntity(classname);
			GameObject entGO;
			if (prefab == null) {
				entGO = new GameObject("MissingFallbackPrefab");
				if (parent != null)
					entGO.transform.parent = parent.transform;
			} else if (parent != null) {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab, KDCBSPUtilities.TransformPosition(entity.origin, worldScale), Quaternion.identity, parent.transform);
			} else {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab);
			}

			// Name it.
			string targetname = entity["targetname"];
			if (targetname != "")
				entGO.name = targetname;
			else
				entGO.name = uniqueName;

			return entGO;
		}

		/// Creates and returns an entity.
		public static GameObject CreateEntity(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string classname, string uniqueName, GameObject parent, List<KDCBSPAbstractEntity> postProcessThese) {
			var entGO = InstantiateEntity(importContext, entity, classname, uniqueName, parent);

			// Find the entity parameterizer.
			KDCBSPAbstractEntity entityDef = null;
			{
				var entityDefArray = entGO.GetComponents<KDCBSPAbstractEntity>();
				if (entityDefArray.Length > 0) {
					if (entityDefArray.Length > 1)
						Debug.LogWarning("More than one KDCBSPAbstractEntity component found in " + classname + ". The first will be picked.");
					entityDef = entityDefArray[0];
				}
				// add default entity component
				if (entityDef == null)
					entityDef = entGO.AddComponent<KDCBSPEntity>();
			}

			var assetPrefix = uniqueName + " ";

			entityDef.EntityCompile(importContext, entity, uniqueName);
			if (entityDef == null)
				return null;
			postProcessThese.Add(entityDef);
			return entGO;
		}
	}
}
