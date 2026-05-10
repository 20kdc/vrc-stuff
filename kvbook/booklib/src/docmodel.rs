use crate::collada::*;
use crate::geom::V2;
use std::collections::HashMap;

/// This is the 'generic' sprite structure.
/// It's used for all instances of sprites after renders have passed deduplication.
#[derive(Clone)]
pub struct DBSprite {
    /// Shape ID in global table or atlas.
    pub shape: usize,
    /// Sprite position.
    /// This is in reference units in this struct, but in the file this is stored as a mapped -1 to 1 range scaled by [DBPage] `size`.
    /// See [DBBook::emit_qv2].
    pub top_left: V2<f32>,
    /// Colour.
    pub colour: [u8; 4],
}

/// This is the 'generic' page structure.
/// It's used for all instances of pages after renders have passed deduplication.
#[derive(Clone)]
pub struct DBPage {
    /// Page size in 'reference units'.
    /// Reference units are basically 'whatever came in'.
    /// Importantly, shape sizes are defined relative to them.
    pub size: V2<f32>,
    pub sprites: Vec<DBSprite>,
}

#[derive(Clone)]
pub struct DBAtlasedShape {
    /// UVs. These are specified in top-left-relative pixels in this struct, but as real UVs in the file.
    pub uv_tl: V2<f32>,
    /// UVs. These are specified in top-left-relative pixels in this struct, but as real UVs in the file.
    pub uv_br: V2<f32>,
    /// Size in reference units.
    pub size: V2<f32>,
}

/// An individual atlas.
#[derive(Clone)]
pub struct DBAtlas {
    pub size: V2<u16>,
    pub shapes: Vec<DBAtlasedShape>,
}

/// Proper atlased book structure.
#[derive(Clone)]
pub struct DBBook {
    pub metadata: json::object::Object,
    /// Atlas sizes.
    pub atlases: Vec<DBAtlas>,
    /// Pages (as atlas, page).
    pub pages: Vec<(u8, DBPage)>,
}

impl Default for DBBook {
    fn default() -> Self {
        Self {
            metadata: json::object::Object::new(),
            atlases: Vec::new(),
            pages: Vec::new(),
        }
    }
}

impl DBBook {
    // -1 to 1
    pub fn emit_qf(v: f32) -> [u8; 2] {
        (((v * 32767f32).round() as i32).clamp(-32768, 32767) as i16).to_le_bytes()
    }
    pub fn emit_qv2(v: V2<f32>) -> [u8; 4] {
        let a = Self::emit_qf(v.0);
        let b = Self::emit_qf(v.1);
        [a[0], a[1], b[0], b[1]]
    }
    // 0 to 1
    pub fn emit_uf(v: f32) -> [u8; 2] {
        (((v * 65535f32).round() as i32).clamp(0, 65535) as u16).to_le_bytes()
    }
    pub fn emit_uv2(v: V2<f32>) -> [u8; 4] {
        let a = Self::emit_uf(v.0);
        let b = Self::emit_uf(1f32 - v.1);
        [a[0], a[1], b[0], b[1]]
    }
    /// Writes book contents to a blob.
    pub fn emit(&self) -> Vec<u8> {
        let mut lumps: Vec<Vec<u8>> = Vec::new();
        // metadata lump
        {
            let mut metadata_lump: Vec<u8> = Vec::new();
            json::JsonValue::Object(self.metadata.clone())
                .write(&mut metadata_lump)
                .unwrap();
            lumps.push(metadata_lump);
        }
        // palette lump placeholder
        const LUMP_PALETTE: usize = 1;
        lumps.push(Vec::new());
        // build atlases lump
        // this is meant to be 'scalable but unobtrusive'
        for atlas in &self.atlases {
            let mut atlas_lump: Vec<u8> = Vec::new();
            for shape in &atlas.shapes {
                let atlas_size = V2(atlas.size.0 as f32, atlas.size.1 as f32);
                atlas_lump.extend_from_slice(&Self::emit_uv2(shape.uv_tl / atlas_size));
                atlas_lump.extend_from_slice(&Self::emit_uv2(shape.uv_br / atlas_size));
                atlas_lump.extend_from_slice(&shape.size.0.to_le_bytes());
                atlas_lump.extend_from_slice(&shape.size.1.to_le_bytes());
            }
            lumps.push(atlas_lump);
        }
        // do palette deduplication inline
        let mut palette_map: HashMap<[u8; 4], usize> = HashMap::new();
        // build page lumps
        for (atlas, page) in &self.pages {
            let mut page_lump: Vec<u8> = Vec::new();
            page_lump.extend_from_slice(&[*atlas]);
            page_lump.extend_from_slice(&page.size.0.to_le_bytes());
            page_lump.extend_from_slice(&page.size.1.to_le_bytes());
            for sprite in &page.sprites {
                page_lump.extend_from_slice(&(sprite.shape as u16).to_le_bytes());
                page_lump.extend_from_slice(&Self::emit_qv2(sprite.top_left / page.size));
                let index = if let Some(index) = palette_map.get(&sprite.colour) {
                    *index
                } else {
                    let v = lumps[LUMP_PALETTE].len() / 4;
                    lumps[LUMP_PALETTE].extend_from_slice(&sprite.colour);
                    palette_map.insert(sprite.colour, v);
                    v
                };
                page_lump.extend_from_slice(&(index as u16).to_le_bytes());
            }
            lumps.push(page_lump);
        }
        // build atlas size lump
        let mut asize_lump: Vec<u8> = Vec::new();
        for atlas in &self.atlases {
            asize_lump.extend_from_slice(&atlas.size.0.to_le_bytes());
            asize_lump.extend_from_slice(&atlas.size.1.to_le_bytes());
        }
        lumps.push(asize_lump);
        // write out header/lumps
        let mut out: Vec<u8> = Vec::new();
        out.extend_from_slice(&(self.atlases.len() as u16).to_le_bytes());
        // version number
        out.extend_from_slice(&(0x0100 as u16).to_le_bytes());
        out.extend_from_slice(&(self.pages.len() as u32).to_le_bytes());
        let mut lpos = 8 + (lumps.len() * 8);
        for lump in &lumps {
            out.extend_from_slice(&(lpos as u32).to_le_bytes());
            out.extend_from_slice(&(lump.len() as u32).to_le_bytes());
            lpos += lump.len();
        }
        // write out lump data
        for lump in &lumps {
            out.extend_from_slice(lump);
        }
        out
    }

    fn dae_transform(page: &DBPage, mut x: f32, mut y: f32) -> (f32, f32, f32) {
        x -= page.size.0 / 2f32;
        y -= page.size.1 / 2f32;
        (-x, y, 0f32)
    }

    fn dae_transform_st(&self, atlas_size: V2<u16>, x: f32, y: f32) -> (f32, f32) {
        (
            x / (atlas_size.0 as f32),
            1f32 - (y / (atlas_size.1 as f32)),
        )
    }

    /// DAE file seems to expect a different colourspace.
    fn dae_transform_col(v: u8) -> f32 {
        f32::powf(v as f32 / 255.0f32, 2.2f32)
    }

    /// Writes book contents to .dae geometry.
    pub fn page_dae(&self, page_index: usize) -> ColladaGeometry {
        let (atlas_id, page) = &self.pages[page_index];
        let atlas = &self.atlases[*atlas_id as usize];
        let atlas_size = atlas.size;
        let mut geom = ColladaGeometry::default();
        for sprite in &page.sprites {
            let colour = (
                Self::dae_transform_col(sprite.colour[0]),
                Self::dae_transform_col(sprite.colour[1]),
                Self::dae_transform_col(sprite.colour[2]),
            );
            let shape = &atlas.shapes[sprite.shape];
            let bottom_right = sprite.top_left + shape.size;
            // AB
            // CD
            let vtxa = ColladaVertex {
                pos: Self::dae_transform(page, sprite.top_left.0, sprite.top_left.1),
                normal: (0f32, 0f32, 1f32),
                st: self.dae_transform_st(atlas_size, shape.uv_tl.0, shape.uv_tl.1),
                colour,
            };
            let vtxb = ColladaVertex {
                pos: Self::dae_transform(page, bottom_right.0, sprite.top_left.1),
                normal: (0f32, 0f32, 1f32),
                st: self.dae_transform_st(atlas_size, shape.uv_br.0, shape.uv_tl.1),
                colour,
            };
            let vtxc = ColladaVertex {
                pos: Self::dae_transform(page, sprite.top_left.0, bottom_right.1),
                normal: (0f32, 0f32, 1f32),
                st: self.dae_transform_st(atlas_size, shape.uv_tl.0, shape.uv_br.1),
                colour,
            };
            let vtxd = ColladaVertex {
                pos: Self::dae_transform(page, bottom_right.0, bottom_right.1),
                normal: (0f32, 0f32, 1f32),
                st: self.dae_transform_st(atlas_size, shape.uv_br.0, shape.uv_br.1),
                colour,
            };
            // AB
            // CD
            geom.triangles.push(vtxc);
            geom.triangles.push(vtxb);
            geom.triangles.push(vtxa);
            geom.triangles.push(vtxd);
            geom.triangles.push(vtxb);
            geom.triangles.push(vtxc);
        }
        geom.name = format!("p{}", page_index);
        geom.material_name = format!("atlas.{}", atlas_id);
        geom
    }
}
