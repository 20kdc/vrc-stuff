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

		private struct EntRecord {
			public ECLBSPFile.Entity entity;
			public GameObject gameObject;
			public IKDCBSPEntity entityDef;
			public string uniqueName;
		}

		public static GameObject BuildMap(IKDCBSPImportContext importContext) {
			var data = importContext.BSP;

			// pass 1: create unparameterized objects

			List<EntRecord> processList = new();

			var mapEntRecord = InstantiateEntity(importContext, data.worldspawn, "worldspawn", "worldspawn ", null);
			processList.Add(mapEntRecord);

			var mapGO = mapEntRecord.gameObject;

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
				processList.Add(InstantiateEntity(importContext, entity, classname, classname + " " + eid, mapGO));
			}

			// pass 2: run main entity compile

			foreach (var c in processList) {
				c.entityDef.EntityCompile(importContext, c.entity, c.uniqueName);
				if (mapGO == null)
					throw new Exception("worldspawn being gone means something has gone horribly wrong, so rather than risking import corruption we choose to bail here");
			}

			// pass 3: postprocess

			foreach (var c in processList)
				if (c.entityDef != null)
					c.entityDef.EntityPostProcess(importContext);
			return mapGO;
		}

		/// Creates and returns an entity.
		private static EntRecord InstantiateEntity(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string classname, string uniqueName, GameObject parent) {
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

			IKDCBSPEntity entityDef = null;
			{
				var entityDefArray = entGO.GetComponents<IKDCBSPEntity>();
				if (entityDefArray.Length > 0) {
					if (entityDefArray.Length > 1)
						Debug.LogWarning("More than one IKDCBSPEntity component found in " + classname + ". The first will be picked.");
					entityDef = entityDefArray[0];
				}
				// add default entity component
				if (entityDef == null)
					entityDef = entGO.AddComponent<KDCBSPEntity>();
			}

			// Name it.
			string targetname = entity["targetname"];
			if (targetname != "") {
				int existing = importContext.AttachTargetname(targetname, entityDef);
				if (existing != 0)
					entGO.name = targetname + " " + existing;
				else
					entGO.name = targetname;
			} else {
				entGO.name = "unnamed " + uniqueName;
			}

			return new EntRecord {
				entity = entity,
				gameObject = entGO,
				entityDef = entityDef,
				uniqueName = uniqueName
			};
		}
	}
}
