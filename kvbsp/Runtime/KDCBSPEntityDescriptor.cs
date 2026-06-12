using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/// This is used to accumulate knowledge about an entity.
	/// Note that anything that an empty IKDCBSPEntity implementation would 'naturally still support' is out of scope for this class.
	/// That means:
	/// 1. Any BSP-compiler-level fields
	/// 2. auto-origin
	/// 3. targetname
	public class KDCBSPEntityDescriptor {
		public bool isSolid = false;

		public static void ExtractAll(SortedDictionary<string, GameObject> entities, out string fgdText, out string entText) {
			fgdText = "";
			entText = "<?xml version=\"1.0\"?>\n<classes>\n";

			foreach (var (key, value) in entities) {
				var desc = KDCBSPEntityDescriptor.ExtractFrom(value);
				if (desc != null) {
					fgdText += "\n" + desc.RenderFGD(key) + "\n";
					entText += "\n" + desc.RenderENT(key) + "\n";
				}
			}

			entText += "</classes>\n";
		}

		public static KDCBSPEntityDescriptor ExtractFrom(GameObject go) {
			var ent = go.GetComponent<IKDCBSPEntity>();
			if (ent != null) {
				KDCBSPEntityDescriptor desc = new();
				desc.isSolid = ent.EntityFGDSolid;
				ent.EntityFGDAttributes(desc);
				return desc;
			} else {
				return null;
			}
		}

		// -- Renderer --

		public string RenderFGD(string classname) {
			string total = isSolid ? "@SolidClass " : "@PointClass ";
			if (hasBox)
				total += $"size({box.min.x} {box.min.y} {box.min.z}, {box.max.x} {box.max.y} {box.max.z}) ";
			if (hasColour)
				total += $"color({colourR} {colourG} {colourB}) ";
			if (hasModel)
				total += $"model(\"{model}\") ";
			total += $"= {classname} : \"{description}\" [\n";

			// built-in or compiler attributes
			total += "\ttargetname(target_source) : \"Name\"\n";
			if (isSolid) {
				total += "\t_mirrorinside(integer) : \"Creates mirrored polygons on the inside. See ericw-tools docs. For Unity use, leave unset or zero.\" : 0\n";
				total += "\t_kdcbsp_autoorigin(integer) : \"If set to 1, KDCBSP will automatically set origin to the centre of the AABB.\" : 0\n";
			}

			foreach (var kvp in attributes) {
				total += "\t" + kvp.Value.RenderFGD(kvp.Key) + "\n";
			}
			total += "]\n";
			return total;
		}

		public string RenderENT(string classname) {
			string total = isSolid ? $"<group name=\"{classname}\" " : $"<point name=\"{classname}\" ";
			if (hasBox)
				total += $"box=\"{box.min.x} {box.min.y} {box.min.z} {box.max.x} {box.max.y} {box.max.z}\" ";
			if (hasColour)
				total += $"color=\"{colourR / 255f} {colourG / 255f} {colourB / 255f}\" ";
			if (hasModel)
				total += $"model=\"{model}\" ";
			total += $">\n{description}\n";

			// built-in or compiler attributes
			total += "\t<targetname key=\"targetname\" name=\"Name\"></targetname>\n";
			if (isSolid) {
				total += "\t<boolean key=\"_kdcbsp_autoorigin\" name=\"Centred Origin\" value=\"0\">When set to 1, the Unity-side importer will move this entity's origin to the AABB centre.</boolean>\n";
			}

			foreach (var kvp in attributes) {
				total += "\t" + kvp.Value.RenderENT(kvp.Key) + "\n";
			}
			total += isSolid ? "</group>\n" : "</point>\n";
			return total;
		}

		// -- Box --

		public bool hasBox;
		public AABB3d box;

		public void Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) {
			box = new AABB3d(new Vector3d(minX, minY, minZ), new Vector3d(maxX, maxY, maxZ));
		}

		// -- Colour --

		public bool hasColour;
		public byte colourR, colourG, colourB;

		public void Color(byte colourR, byte colourG, byte colourB) {
			hasColour = true;
			this.colourR = colourR;
			this.colourG = colourG;
			this.colourB = colourB;
		}

		public void Colour(byte colourR, byte colourG, byte colourB) {
			hasColour = true;
			this.colourR = colourR;
			this.colourG = colourG;
			this.colourB = colourB;
		}

		// -- Model --

		public bool hasModel;
		public string model;

		public void Model(string model) {
			hasModel = true;
			this.model = model;
		}

		// -- Description --

		public string description = "An undescribed entity.";

		// -- Attributes --

		public SortedDictionary<string, AttrVal> attributes = new();

		public T Key<T>(string key, string def, T a) where T : AttrVal {
			a.friendly = key;
			a.defValue = def;
			attributes[key] = a;
			return a;
		}

		public AttrValFloat FloatKey(string key, string def) => Key(key, def, new AttrValFloat());
		public AttrValInteger IntegerKey(string key, string def) => Key(key, def, new AttrValInteger());
		public AttrValString StringKey(string key, string def) => Key(key, def, new AttrValString());
		public AttrValTarget TargetKey(string key, string def) => Key(key, def, new AttrValTarget());
		public AttrValVector3d Vector3dKey(string key, string def) => Key(key, def, new AttrValVector3d());
		public AttrValChoices ChoicesKey(string key, string def) => Key(key, def, new AttrValChoices());

		// -- Attributes impl --

		public abstract class AttrVal {
			public string friendly = "";
			public string description = "";
			public string defValue = "";

			public AttrVal Friendly(string val) {
				friendly = val;
				return this;
			}

			public AttrVal Default(string val) {
				defValue = val;
				return this;
			}

			public AttrVal Desc(string val) {
				description = val;
				return this;
			}

			public abstract string FGDTypeName { get; }
			public abstract string ENTTypeName { get; }

			public virtual string ENTDescriptionSuffix {
				get => "";
			}

			public virtual string RenderFGD(string key) => $"{key}({FGDTypeName}) : \"{description}\" : \"{defValue}\"";
			public virtual string RenderENT(string key) => $"<{ENTTypeName} key=\"{key}\" name=\"{friendly}\" value=\"{defValue}\">{description}{ENTDescriptionSuffix}</{ENTTypeName}>";
		}

		public class AttrValFloat : AttrVal {
			public override string FGDTypeName => "float";
			public override string ENTTypeName => "real";
		}

		public class AttrValInteger : AttrVal {
			public override string FGDTypeName => "integer";
			public override string ENTTypeName => "integer";
		}

		public class AttrValString : AttrVal {
			public override string FGDTypeName => "string";
			public override string ENTTypeName => "string";
		}

		public class AttrValTarget : AttrVal {
			public override string FGDTypeName => "target_destination";
			public override string ENTTypeName => "target";
		}

		public class AttrValVector3d : AttrVal {
			public override string FGDTypeName => "string";
			public override string ENTTypeName => "real3";
		}

		public class AttrValChoices : AttrVal {
			public override string FGDTypeName => "string";
			public override string ENTTypeName => "string";

			public string fgdChoices, entChoices;

			public override string ENTDescriptionSuffix => entChoices;

			public override string RenderFGD(string key) => $"{base.RenderFGD(key)} = [ {fgdChoices} ]";

			public AttrValChoices Choice(string key, string name) {
				fgdChoices += $" \"{key}\" : \"{name}\" ";
				entChoices += $"\n\"{key}\": {name}";
				return this;
			}
		}
	}
}
