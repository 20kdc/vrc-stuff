# Rendering of SVG to SDF 'objects' for efficient display in VR

This is designed for reading well-typeset books in VR at the highest possible quality on standalone headsets.

Also for toki pona dictionary charts.

## Known Hard To Fix Issues

* Strokes and fills are confused. _Requires patching resvg, switching rasterizers, or imitating resvg to fix :(_
* Transparency is DOA.
* Raster images are nigh-impossible

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
