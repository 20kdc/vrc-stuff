using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// 'TrenchBroom Simulator'
	/// ( design maps! play deathmatch with your friends! get rekt. coming: as soon as you install tb and hl :3 )
	/// This code simulates the transforms TrenchBroom performs when exporting a map, among other things.
	/// Basically, maps saved directly from TrenchBroom are written to **mostly** work in standard map compilers.
	/// This prevents the 'RMF problem' that plagues i.e. Hammer.
	/// However, maps handled this way have:
	/// * A massive quantity of `func_group` entities
	/// * Things marked 'omit from export' not omitted from export
	public static class TrenchBroom {
		/// More or less simulates a TrenchBroom export.
		public static void FullSimulateExport(List<EntityParsed> entities) {
			HandleOmitFromExport(entities);
			RemoveTBMetadata(entities);
		}

		/// Deletes entities on layers marked omit-from-export.
		public static void HandleOmitFromExport(List<EntityParsed> entities) {
			// Worldspawn is used for the 'Default Layer'.
			EntityParsed worldspawn = EntityParsed.EnsureWorldspawn(entities);
			// pass 1: build ID table
			Dictionary<string, EntityParsed> tbID = new();
			foreach (var ent in entities) {
				string entTBID = ent.pairs["_tb_id"];
				if (entTBID != "")
					tbID[entTBID] = ent;
			}
			// pass 2: build group/layer parent table
			Dictionary<EntityParsed, EntityParsed> parentage = new();
			foreach (var ent in entities) {
				// Worldspawn never has a parent.
				if (ent == worldspawn)
					continue;
				// Layers never have parents in our parentage table.
				// This is because otherwise worldspawn's omit flag would affect other layers.
				if (ent.pairs["_tb_type"] == "_tb_layer")
					continue;
				if (tbID.TryGetValue(ent.pairs["_tb_layer"], out var par))
					parentage[ent] = par;
				else if (tbID.TryGetValue(ent.pairs["_tb_group"], out par))
					parentage[ent] = par;
				else
					parentage[ent] = worldspawn;
			}
			// pass 3: omit?
			List<EntityParsed> omit = new();
			foreach (var ent in entities) {
				var cursor = ent;
				for (int pass = 0; pass < 32; pass++) {
					// if we found a node marked omit, stop
					if (cursor.pairs.GetBool("_tb_layer_omit_from_export", false)) {
						// proved omittable
						omit.Add(ent);
						break;
					}
					// if there's no parent, proved not omittable
					if (!parentage.TryGetValue(cursor, out var next))
						break;
					cursor = next;
				}
			}
			// pass 4: sweep
			foreach (var ent in omit)
				entities.Remove(ent);
		}

		/// Removes all TB metadata, including TB dummy groups.
		/// This also includes removing "_tb_" keys.
		/// Merges as-necessary into worldspawn.
		public static void RemoveTBMetadata(List<EntityParsed> entities) {
			EntityParsed worldspawn = EntityParsed.EnsureWorldspawn(entities);
			List<EntityParsed> attic = new();
			foreach (EntityParsed ent in entities) {
				// A group that was solely created by TrenchBroom:
				// MUST be func_group
				if (ent.pairs["classname"] != "func_group")
					continue;
				// Has a "_tb_type" of "_tb_group" or "_tb_layer"
				//  (this is to stop *user-provided* func_groups being eaten)
				string entType = ent.pairs["_tb_type"];
				if (entType != "_tb_group" && entType != "_tb_layer")
					continue;
				// Has no other non-TB keys
				// (think "_chop" etc.)
				bool foundNonTB = false;
				for (int pairIdx = 0; pairIdx < ent.pairs.Count;) {
					var pair = ent.pairs[pairIdx];
					if (pair.Item1 == "classname") {
						pairIdx++;
						continue;
					}
					if (!pair.Item1.StartsWith("_tb_")) {
						pairIdx++;
						foundNonTB = true;
						continue;
					}
					// _tb_ metadata, clean it up while we're here.
					ent.pairs.RemoveAt(pairIdx);
				}
				if (foundNonTB)
					continue;
				attic.Add(ent);
			}
			foreach (EntityParsed ep in attic) {
				// transfer brushes to worldspawn, then get rid of the entity
				foreach (var brush in ep.brushes)
					worldspawn.brushes.Add(brush);
				ep.brushes.Clear();
				entities.Remove(ep);
			}
		}
	}
}
