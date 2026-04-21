# File Format

The file format used by this Udon script is designed to encode:

* A metadata JSON object
* A palette, consisting of 4-byte RGBA (non-premultiplied, non-linear) colours
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

Upper 8 bits of version are major. Current version is `0x0100`.

`kvbookLoader.uasm` will accept versions 0x0100 through 0x01FF inc.

Loaders will support all sensible files of a given major version. With this said, it is unlikely for the major version to change in future, as there are no longer any format changes worth making.

## Header

The binary file format starts with a header:

```
// kvtoolsLoader reads atlas_count and version fields as one int32, then ANDs/shifts accordingly.
uint16_t atlas_count;
uint16_t version;
uint32_t page_count;
lump_t metadata;
lump_t palette;
lump_t atlases[atlas_count];
lump_t pages[page_count];
```

`lump_t` is a simple offset/length pair:

```
uint32_t offset;
uint32_t length;
```

The metadata lump is a JSON UTF-8 string, not null-terminated.

It is guaranteed to parse as a JSON object, but is considered application-specific data.

The palette lump is pretty simple:

```
struct {
	uint8_t r, g, b, a;
} palette[...];
```

Each atlas lump contains 'shapes'. These contain regions on the actual atlas texture, along with a reference size to display at.

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
	uint16_t colour; // colour index in palette
} sprites[...];
```

Note that the size of the sprite structure is the primary driver of the binary format's file size.
