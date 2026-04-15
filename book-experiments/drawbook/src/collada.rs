use std::fmt::Write;

#[derive(Clone, Copy, Default)]
pub struct ColladaVertex {
    pub pos: (f32, f32, f32),
    pub normal: (f32, f32, f32),
    pub st: (f32, f32),
    pub colour: (f32, f32, f32),
}

#[derive(Clone, Default)]
pub struct ColladaGeometry {
    pub name: String,
    pub triangles: Vec<ColladaVertex>,
}

fn collada_write_float_array(
    target: &mut String,
    id: String,
    data: &[f32],
    fields: &[&'static str],
) {
    _ = writeln!(target, "   <source id=\"{}\">", id);
    _ = write!(
        target,
        "    <float_array id=\"{}-array\" count=\"{}\">",
        id,
        data.len()
    );
    for v in data.iter().enumerate() {
        if v.0 != 0 {
            _ = write!(target, " ");
        }
        _ = write!(target, "{}", v.1);
    }
    _ = writeln!(
        target,
        "</float_array><technique_common><accessor source=\"#{}-array\" count=\"{}\" stride=\"{}\">",
        id,
        data.len() / fields.len(),
        fields.len()
    );
    for field in fields {
        _ = writeln!(target, "     <param name=\"{}\" type=\"float\"/>", field);
    }
    _ = writeln!(target, "   </accessor></technique_common></source>");
}

/// Minimum-effort collada writer.
pub fn collada_write(geom: &[ColladaGeometry]) -> String {
    let mut target = String::new();
    _ = writeln!(
        target,
        r##"<?xml version="1.0" encoding="utf-8"?>
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
 <asset>
  <contributor>
   <authoring_tool>20kdc vrc-stuff drawbook</authoring_tool>
  </contributor>
  <unit name="mm" meter="0.001"/>
  <up_axis>Z_UP</up_axis>
 </asset>
 <library_images/>
 <library_effects>
  <effect id="Material-effect"><profile_COMMON><technique sid="common"><phong/></technique></profile_COMMON></effect>
 </library_effects>
 <library_materials>
  <material id="Material-material" name="Material"><instance_effect url="#Material-effect"/></material>
 </library_materials>
 <library_geometries>"##
    );
    let mut float_buf: Vec<f32> = Vec::new();
    for v in geom {
        _ = writeln!(
            target,
            "  <geometry id=\"{}-mesh\" name=\"{}\"><mesh>",
            v.name, v.name
        );
        float_buf.clear();
        for vtx in &v.triangles {
            float_buf.push(vtx.pos.0);
            float_buf.push(vtx.pos.1);
            float_buf.push(vtx.pos.2);
        }
        collada_write_float_array(
            &mut target,
            format!("{}-mesh-positions", v.name),
            &float_buf,
            &["X", "Y", "Z"],
        );
        float_buf.clear();
        for vtx in &v.triangles {
            float_buf.push(vtx.normal.0);
            float_buf.push(vtx.normal.1);
            float_buf.push(vtx.normal.2);
        }
        collada_write_float_array(
            &mut target,
            format!("{}-mesh-normals", v.name),
            &float_buf,
            &["X", "Y", "Z"],
        );
        float_buf.clear();
        for vtx in &v.triangles {
            float_buf.push(vtx.st.0);
            float_buf.push(vtx.st.1);
        }
        collada_write_float_array(
            &mut target,
            format!("{}-mesh-map-0", v.name),
            &float_buf,
            &["S", "T"],
        );
        float_buf.clear();
        // TexCoord1 is used for some control options in TextMeshPro, which we're not-so-secretly trying to support the shader of here.
        for _ in &v.triangles {
            float_buf.push(0.0f32);
            // input to the scale adjust line
            float_buf.push(1.0f32);
        }
        collada_write_float_array(
            &mut target,
            format!("{}-mesh-map-1", v.name),
            &float_buf,
            &["S", "T"],
        );
        float_buf.clear();
        for vtx in &v.triangles {
            float_buf.push(vtx.colour.0);
            float_buf.push(vtx.colour.1);
            float_buf.push(vtx.colour.2);
        }
        collada_write_float_array(
            &mut target,
            format!("{}-mesh-colors-Col", v.name),
            &float_buf,
            &["R", "G", "B"],
        );
        let mut indices = String::new();
        // collada requires each separate input to have its own index
        let attributes = 5;
        for i in 0..(v.triangles.len() * attributes) {
            if i == 0 {
                _ = writeln!(indices, "{}", i / attributes);
            } else {
                _ = writeln!(indices, " {}", i / attributes);
            }
        }
        _ = writeln!(
            target,
            r##"   <vertices id="{}-mesh-vertices">
    <input semantic="POSITION" source="#{}-mesh-positions"/>
   </vertices>
   <triangles count="{}">
    <input semantic="VERTEX" source="#{}-mesh-vertices" offset="0"/>
    <input semantic="NORMAL" source="#{}-mesh-normals" offset="1"/>
    <input semantic="TEXCOORD" source="#{}-mesh-map-0" offset="2" set="0"/>
    <input semantic="TEXCOORD" source="#{}-mesh-map-1" offset="3" set="1"/>
    <input semantic="COLOR" source="#{}-mesh-colors-Col" offset="4" set="0"/>
    <p>{}</p>
   </triangles>"##,
            v.name,
            v.name,
            v.triangles.len() / 3,
            v.name,
            v.name,
            v.name,
            v.name,
            v.name,
            indices
        );
        _ = writeln!(target, "  </mesh></geometry>");
    }
    _ = writeln!(
        target,
        " </library_geometries><library_controllers/><library_visual_scenes><visual_scene id=\"Scene\" name=\"Scene\">"
    );
    for v in geom {
        _ = writeln!(
            target,
            "  <node id=\"{}\" name=\"{}\" type=\"NODE\">",
            v.name, v.name
        );
        _ = writeln!(
            target,
            r##"   <translate sid="location">0 0 0</translate>
   <rotate sid="rotationZ">0 0 1 0</rotate>
   <rotate sid="rotationY">0 1 0 0</rotate>
   <rotate sid="rotationX">1 0 0 0</rotate>
   <scale sid="scale">1 1 1</scale>
   <instance_geometry url="#{}-mesh" name="{}">
    <bind_material><technique_common><instance_material symbol="Material-material" target="#Material-material"/></technique_common></bind_material>
   </instance_geometry>"##,
            v.name, v.name
        );
        _ = writeln!(target, "  </node>");
    }
    _ = writeln!(
        target,
        " </visual_scene></library_visual_scenes><scene><instance_visual_scene url=\"#Scene\"/></scene>"
    );
    _ = writeln!(target, "</COLLADA>");
    target
}
