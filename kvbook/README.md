# Rendering of SVG to SDF 'objects' for efficient display in VR

This is designed for reading well-typeset books in VR at the highest possible quality on standalone headsets.

Also for toki pona dictionary charts.

## Principles

There are some key insights that make this all possible.

1. Most content we care about is made of up solid colours.
2. Except for fill/stroke, which is broken right now, this content is one colour per object.
3. We can render objects individually unless something fancy is happening, and we don't care about fancy, we can't render fancy anyway.
4. The same font paths rasterize to the same results, which allows us to deduplicate and atlas the results. This last insight is ultimately what made using SDF here practical.

There are a number of theoretical improvements and optimizations here. These have been omitted because they either:

* Add too much compiler load
* Add too much viewer load
* Make SVG processing too hard
* Require expanding the extremely size-bound sprite structure
* Exceed the capabilities of the TextMeshPro Mobile shader for a single mesh
* Would generate an unholy number of draw calls (i.e. multi-atlas per page)

## Known Hard To Fix Issues

* Strokes and fills are confused. _Requires patching resvg, switching rasterizers, or imitating resvg to fix :(_
* Transparency is DOA.
* Fullcolour raster images are nigh-impossible without doing one of:
	* Mapping the image to a palette of layers (bad and not really fullcolour)
	* Tracking which of either the image or anything occluded by the image 'survives' (crazy and hard)
