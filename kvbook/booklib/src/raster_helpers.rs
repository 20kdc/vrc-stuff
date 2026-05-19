use geomlib::*;
use tiny_skia::Pixmap;

#[derive(Clone, Copy)]
pub enum RasterScaler {
    Skia,
    Internal,
}

pub fn raster_scale(
    src: &Raster<[u8; 4]>,
    scaled: V2<usize>,
    scaler: RasterScaler,
) -> Raster<[u8; 4]> {
    match scaler {
        RasterScaler::Skia => {
            let mut fc = tiny_skia::Pixmap::new(src.size().0 as u32, src.size().1 as u32).unwrap();
            for i in fc.pixels_mut().iter_mut().enumerate() {
                let p = src.data()[i.0];
                *i.1 = tiny_skia::ColorU8::from_rgba(p[0], p[1], p[2], p[3]).premultiply();
            }
            let mut res_scaled = Pixmap::new(scaled.0 as u32, scaled.1 as u32).unwrap();
            res_scaled.draw_pixmap(
                0,
                0,
                fc.as_ref(),
                &tiny_skia::PixmapPaint {
                    blend_mode: tiny_skia::BlendMode::Source,
                    quality: tiny_skia::FilterQuality::Bicubic,
                    ..Default::default()
                },
                tiny_skia::Transform::identity().pre_scale(
                    scaled.0 as f32 / fc.width() as f32,
                    scaled.1 as f32 / fc.height() as f32,
                ),
                None,
            );
            let fixit = res_scaled.take_demultiplied();
            let fixed_cap = (scaled.0 as usize) * (scaled.1 as usize);
            let mut fixed: Vec<[u8; 4]> = Vec::with_capacity(fixed_cap);
            for i in 0..fixed_cap {
                let b = i * 4;
                fixed.push([fixit[b + 0], fixit[b + 1], fixit[b + 2], fixit[b + 3]]);
            }
            Raster::new(fixed, V2(scaled.0 as usize, scaled.1 as usize))
        }
        RasterScaler::Internal => src.scale(scaled, &|v| {
            // A key problem here is that this doesn't account for low-alpha pixels. :(
            let mut zero: [f32; 4] = [0f32; 4];
            for v in v {
                zero[0] += (v.0[0] as f32) * v.1;
                zero[1] += (v.0[1] as f32) * v.1;
                zero[2] += (v.0[2] as f32) * v.1;
                zero[3] += (v.0[3] as f32) * v.1;
            }
            [
                zero[0].ceil() as u8,
                zero[1].ceil() as u8,
                zero[2].ceil() as u8,
                zero[3].ceil() as u8,
            ]
        }),
    }
}

/// Raster to RGBA PNG.
pub fn raster_png(raster: &Raster<[u8; 4]>) -> Vec<u8> {
    let raw_data: &[u8] = zerocopy::transmute_ref!(raster.data());
    let mut png_data = vec![];
    let mut encoder = png::Encoder::new(
        &mut png_data,
        raster.size().0 as u32,
        raster.size().1 as u32,
    );
    encoder.set_color(png::ColorType::Rgba);
    encoder.set_depth(png::BitDepth::Eight);
    let mut encoder = encoder.write_header().unwrap();
    encoder.write_image_data(&raw_data).unwrap();
    encoder.finish().unwrap();
    png_data
}
