use crate::*;
use std::collections::BTreeSet;
use std::sync::Arc;

#[derive(Clone)]
pub struct AtlasBuilder {
    pub planner: AtlasPage,
    /// Maps global shape IDs to atlas shape IDs.
    pub shape_map: Vec<Option<usize>>,
    /// Inverse of `shape_map`.
    pub inv_shape_map: Vec<usize>,
    /// Placed shapes.
    pub placements: Vec<DBAtlasedShape>,
}

impl AtlasBuilder {
    pub fn new(sz: V2<usize>, shape_count: usize) -> Self {
        let mut shape_map = Vec::with_capacity(shape_count);
        for _ in 0..shape_count {
            shape_map.push(None);
        }
        let mut atlas: AtlasPage = AtlasPage::new(sz);
        atlas.delete_under = V2(4, 4);
        atlas.mark(Rect {
            tl: V2(0, 0),
            br: V2(5, 5),
        });
        Self {
            planner: atlas,
            shape_map,
            inv_shape_map: Vec::new(),
            placements: Vec::new(),
        }
    }
    /// Consumes the AtlasBuilder, returning a DBAtlas.
    pub fn complete(self) -> DBAtlas {
        DBAtlas {
            size: V2(self.planner.size.0 as u16, self.planner.size.1 as u16),
            shapes: self.placements,
        }
    }
    pub fn watermark(&self) -> usize {
        self.placements.len()
    }
    /// Reverts most data in the atlas info (except effects on the AtlasPage) to the given watermark.
    /// This should really only be done once, and is used to 'cancel' a page.
    pub fn revert_to_watermark(&mut self, watermark: usize) {
        self.placements.truncate(watermark);
        for v in &self.inv_shape_map[watermark..] {
            self.shape_map[*v] = None;
        }
        self.inv_shape_map.truncate(watermark);
    }

    /// Inserts a shape into the atlas.
    /// Will not resize the atlas.
    pub fn try_add_shape(
        &mut self,
        global_id: usize,
        shape: &Arc<DBRenderedShape>,
        sdf: &Option<Pixmap>,
        atlas_perfchop: usize,
    ) -> bool {
        // Convert from render units into reference units.
        let size = V2(shape.size().0 as f32, shape.size().1 as f32)
            / V2(shape.render_mul(), shape.render_mul());
        if let Some(sdf) = sdf {
            let pt = self
                .planner
                .place(V2(sdf.width() as usize + 2, sdf.height() as usize + 2));
            if let Some(pt) = pt {
                if self.planner.free.len() >= atlas_perfchop {
                    progress::alert("--atlas-perfchop freelist limit reached");
                    self.planner.perf_chop();
                }
                self.placements.push(DBAtlasedShape {
                    uv_tl: V2(pt.0 as f32, pt.1 as f32) + V2(1f32, 1f32),
                    uv_br: V2(
                        (pt.0 + sdf.width() as usize) as f32,
                        (pt.1 + sdf.height() as usize) as f32,
                    ) + V2(1f32, 1f32),
                    size,
                });
            } else {
                // failed to place, so can't continue
                return false;
            }
        } else {
            self.placements.push(DBAtlasedShape {
                // set to the 'rectangle' texture
                uv_tl: V2(2f32, 2f32),
                uv_br: V2(3f32, 3f32),
                size,
            });
        }
        let local_id = self.inv_shape_map.len();
        self.inv_shape_map.push(global_id);
        self.shape_map[global_id] = Some(local_id);
        true
    }

    /// Atlases a page.
    /// If the atlas is enlarged to above max_size, returns false and the (empty) page.
    /// Otherwise returns true and the complete translated page.
    /// Option is not used for the return because of the case in which max_size is None and thus a None return is impossible.
    pub fn atlas_page(
        &mut self,
        src_page: &DBPage,
        shape_lookup: &DBShapeLookup,
        sdf_shapes: &[Option<Pixmap>],
        max_size: Option<usize>,
        atlas_perfchop: usize,
    ) -> (bool, DBPage) {
        let mut page = DBPage {
            size: src_page.size,
            sprites: Vec::with_capacity(src_page.sprites.len()),
        };

        // sortable list of shapes that must be added
        let mut shapes_to_add: Vec<usize> = {
            let mut shapes_to_add = BTreeSet::new();
            for src_sprite in &src_page.sprites {
                if self.shape_map[src_sprite.shape].is_none() {
                    shapes_to_add.insert(src_sprite.shape);
                }
            }
            shapes_to_add.iter().map(|v| *v).collect()
        };

        // determine encounter order, place descending
        shapes_to_add.sort_by(|v1, v2| {
            let v1r: &Option<Pixmap> = &sdf_shapes[*v1];
            let v2r: &Option<Pixmap> = &sdf_shapes[*v2];
            let v1s = v1r
                .as_ref()
                .map(|v| (v.width(), v.height()))
                .unwrap_or((0, 0));
            let v2s = v2r
                .as_ref()
                .map(|v| (v.width(), v.height()))
                .unwrap_or((0, 0));
            (v2s.0 * v2s.1).cmp(&(v1s.0 * v1s.1))
        });

        for shape_idx in shapes_to_add {
            while !self.try_add_shape(
                shape_idx,
                &shape_lookup.shapes[shape_idx],
                &sdf_shapes[shape_idx],
                atlas_perfchop,
            ) {
                if let Some(max_size) = max_size {
                    let sz = self.planner.enlarge_size();
                    if sz.0 > max_size || sz.1 > max_size {
                        return (false, page);
                    }
                }
                self.planner.enlarge();
                assert!(
                    self.planner.size.0 < 65536 && self.planner.size.1 < 65536,
                    "atlas size out of range"
                );
            }
        }

        for sprite in &src_page.sprites {
            page.sprites.push(DBSprite {
                shape: self.shape_map[sprite.shape].unwrap(),
                top_left: sprite.top_left,
                colour: sprite.colour,
            });
        }

        (true, page)
    }
}
