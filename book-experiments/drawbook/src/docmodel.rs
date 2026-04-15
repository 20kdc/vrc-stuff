use crate::collada::*;
use crate::geom::V2;

/// A 'sprite' is a single renderable entity in the book.
#[derive(Clone)]
pub struct DBSprite {
    /// Shape ID.
    pub shape: usize,
    /// Sprite position.
    /// This is in reference units in this struct, but in the file this is stored as a mapped -1 to 1 range scaled by [DBPage] `size`.
    /// See [DBBook::emit_qv2].
    pub top_left: V2<f32>,
    /// Colour.
    pub colour: [u8; 3],
}

/// A page of the book.
#[derive(Clone)]
pub struct DBPage {
    /// Page size in 'reference units'.
    /// Reference units are basically 'whatever came in'.
    /// Importantly, shape sizes are defined relative to them.
    pub size: V2<f32>,
    pub sprites: Vec<DBSprite>,
}

#[derive(Clone)]
pub struct DBShapeAtlased {
    pub atlas: u8,
    /// UVs. These are specified in top-left-relative pixels in this struct, but as real UVs in the file.
    pub uv_tl: V2<f32>,
    /// UVs. These are specified in top-left-relative pixels in this struct, but as real UVs in the file.
    pub uv_br: V2<f32>,
    /// Size in reference units.
    pub size: V2<f32>,
}

/// Proper atlased book structure.
#[derive(Clone, Default)]
pub struct DBBook {
    /// Atlas sizes.
    pub atlases: Vec<V2<u16>>,
    pub shapes: Vec<DBShapeAtlased>,
    pub pages: Vec<DBPage>,
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
        let b = Self::emit_uf(v.1);
        [a[0], a[1], b[0], b[1]]
    }
    /// Writes book contents to a blob.
    pub fn emit(&self) -> Vec<u8> {
        let mut lumps: Vec<Vec<u8>> = Vec::new();
        // build atlases lump
        let mut atlases_lump: Vec<u8> = Vec::new();
        for atlas_size in &self.atlases {
            atlases_lump.extend_from_slice(&atlas_size.0.to_le_bytes());
            atlases_lump.extend_from_slice(&atlas_size.1.to_le_bytes());
        }
        lumps.push(atlases_lump);
        // build shapes lump
        let mut shapes_lump: Vec<u8> = Vec::new();
        for shape in &self.shapes {
            shapes_lump.extend_from_slice(&[shape.atlas]);
            let atlas_size = self.atlases[shape.atlas as usize];
            let atlas_size = V2(atlas_size.0 as f32, atlas_size.1 as f32);
            shapes_lump.extend_from_slice(&Self::emit_uv2(shape.uv_tl / atlas_size));
            shapes_lump.extend_from_slice(&Self::emit_uv2(shape.uv_br / atlas_size));
            shapes_lump.extend_from_slice(&shape.size.0.to_le_bytes());
            shapes_lump.extend_from_slice(&shape.size.1.to_le_bytes());
        }
        lumps.push(shapes_lump);
        // build page lumps
        for page in &self.pages {
            let mut page_lump: Vec<u8> = Vec::new();
            page_lump.extend_from_slice(&page.size.0.to_le_bytes());
            page_lump.extend_from_slice(&page.size.1.to_le_bytes());
            for sprite in &page.sprites {
                page_lump.extend_from_slice(&(sprite.shape as u16).to_le_bytes());
                page_lump.extend_from_slice(&Self::emit_qv2(sprite.top_left / page.size));
                // colour to RGB565
                let r = sprite.colour[0] as u32;
                let g = sprite.colour[1] as u32;
                let b = sprite.colour[2] as u32;
                let rgb565 = ((r << 8) & 0xF800) | ((g << 3) & 0x07E0) | ((b >> 3) & 0x001F);
                page_lump.extend_from_slice(&(rgb565 as u16).to_le_bytes());
            }
            lumps.push(page_lump);
        }
        // write out header/lumps
        let mut out: Vec<u8> = Vec::new();
        out.extend_from_slice(&(lumps.len() as u32).to_le_bytes());
        let mut lpos = 4 + (lumps.len() * 8);
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

    fn dae_transform(x: f32, y: f32) -> (f32, f32, f32) {
        (-x, y, 0f32)
    }

    fn dae_transform_st(&self, atlas_size: V2<u16>, x: f32, y: f32) -> (f32, f32) {
        (x / (atlas_size.0 as f32), y / (atlas_size.1 as f32))
    }

    /// Writes book contents to a .dae file.
    pub fn emit_dae(&self) -> String {
        let mut pages_geom: Vec<ColladaGeometry> = Vec::new();
        for v in self.pages.iter().enumerate() {
            let mut geom = ColladaGeometry::default();
            for sprite in &v.1.sprites {
                let colour = (
                    sprite.colour[0] as f32 / 255.0f32,
                    sprite.colour[1] as f32 / 255.0f32,
                    sprite.colour[2] as f32 / 255.0f32,
                );
                let shape = &self.shapes[sprite.shape];
                let atlas_size = self.atlases[shape.atlas as usize];
                let bottom_right = sprite.top_left + shape.size;
                // AB
                // CD
                let vtxa = ColladaVertex {
                    pos: Self::dae_transform(sprite.top_left.0, sprite.top_left.1),
                    normal: (0f32, 0f32, 1f32),
                    st: self.dae_transform_st(atlas_size, shape.uv_tl.0, shape.uv_tl.1),
                    colour,
                };
                let vtxb = ColladaVertex {
                    pos: Self::dae_transform(bottom_right.0, sprite.top_left.1),
                    normal: (0f32, 0f32, 1f32),
                    st: self.dae_transform_st(atlas_size, shape.uv_br.0, shape.uv_tl.1),
                    colour,
                };
                let vtxc = ColladaVertex {
                    pos: Self::dae_transform(sprite.top_left.0, bottom_right.1),
                    normal: (0f32, 0f32, 1f32),
                    st: self.dae_transform_st(atlas_size, shape.uv_tl.0, shape.uv_br.1),
                    colour,
                };
                let vtxd = ColladaVertex {
                    pos: Self::dae_transform(bottom_right.0, bottom_right.1),
                    normal: (0f32, 0f32, 1f32),
                    st: self.dae_transform_st(atlas_size, shape.uv_br.0, shape.uv_br.1),
                    colour,
                };
                // AB
                // CD
                geom.triangles.push(vtxa);
                geom.triangles.push(vtxb);
                geom.triangles.push(vtxc);
                geom.triangles.push(vtxc);
                geom.triangles.push(vtxb);
                geom.triangles.push(vtxd);
            }
            geom.name = format!("p{}", v.0);
            pages_geom.push(geom);
        }
        collada_write(&pages_geom)
    }
}
