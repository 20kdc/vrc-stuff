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

## Binary File Format

The binary file format uses several different forms of unit:

* 'Reference space', which matches the incoming SVG file.
* '`int16` reference space', which is the result of:
	1. dividing a reference-space coordinate by the page size
	2. multiplying it by `32767.0`
	3. clamping
	4. converting to `int16`
* '`float` UV space', 0,0 being top-left and 1,1 being bottom-right.
* '`uint16` UV space', which maps 0-65535 to 0-1.

The binary file format starts with a header:

```
uint32_t atlas_count;
uint32_t page_count;
lump_t lumps[atlas_count + page_count + 1];
```

`lump_t` is a simple offset/length pair:

```
uint32_t offset;
uint32_t length;
```

The lumps represented here are divided into three kinds: Atlas shape array lumps, page lumps, and the atlas sizes lump.

Atlas shape array lumps contain 'shapes'. These contain regions on the actual atlas texture, along with a reference size to display at.

```
struct {
	uint16_t u, v; // uint16 UV space
	float w, h; // Reference space width/height
} shapes[...];
```

Page lumps contain a short header giving the reference size of the page (seemingly _theoretically_ in millimetres for PDF content, but anything goes) followed by 'sprites'.

```
uint8_t atlas;
float width, height; // defines the page size, which is also the extent of reference space, and therefore the extent of quantized reference space
struct {
	uint16_t shape; // shape ID in atlas
	int16_t x, y; // position in int16 reference space
	uint16_t rgb565; // RGB565 colour
} sprites[...];
```

Note that the size of the sprite structure is the primary driver of the binary format's file size.

The atlas size lump contains the texture sizes of the previous atlas lumps. There may be some obscure reason you need this, but it's put at the back of the lumps for a reason.

```
struct {
	uint16_t w, h;
} atlas_sizes[...];
```
