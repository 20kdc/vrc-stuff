# File Format

The file format used by this Udon script is designed to encode:

* A series of _atlases_
	* Represents a single compiled image file.
	* Contains a set of _shapes_
		* Each shape refers to a region on the atlas, and provides a fixed size in page-size units.
* A series of _pages_
	* Has a size and refers to a single _atlas_
	* Contains a list of _sprites,_ referring to _shapes_ in the referred-to _atlas_
		* Each sprite is a single-colour opaque object.
		* Sprites are drawn in the order they are provided.
		* Colour and translation is per-sprite, but there's no mechanism for scaling, rotation, etc.

It uses several different forms of unit:

* 'Reference space', which matches the incoming SVG files.
* '`int16` reference space', which is the result of:
	1. dividing a reference-space coordinate by the page size
	2. multiplying it by `32767.0`
	3. clamping
	4. converting to `int16`
* '`float` UV space', 0,1 being top-left and 1,0 being bottom-right.
	* The Y is inverted here because it'd need to be done in the Udon code otherwise, and if you've seen it, you know why I don't want to do that.
* '`uint16` UV space', which maps 0-65535 to 0-1.

## Versioning

Upper 8 bits of version are major. Current version is `0x0000`.

`kvbookLoader.uasm` will likely either accept versions 0x0000 through 0x00FF inc. or versions 0x0100 through 0x01FF inc.

loaders will support all sensible files of a given major version.

In practice the only reason the major version would change would be because of a change in sprite packing, likely replacing RGB565 with a palette table to allow for alpha trickery.

## Header

The binary file format starts with a header:

```
// kvtoolsLoader reads atlas_count and version fields as one int32, then ANDs/shifts accordingly.
uint16_t atlas_count;
uint16_t version;
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
