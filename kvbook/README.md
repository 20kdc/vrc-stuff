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

## Structure

* `geomlib`: Prelude, contains various useful mathematical tricks
* `booklib`: Core processing and writer library.
* `inputlib`: Handles poking `mupdf` to get SVG files.
* `drawbook`: Core book converter.
* `svgseparator`: Preprocesses an SVG with `usvg`, then splits into a set of simple 'one shape per file' objects for SDF rendition. Has an associated test program for diagnostics.
* `testbook`: Godot 3.x project to render a converted book.
