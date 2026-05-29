#include <std/string.pat>
#include <std/core.pat>

struct CMFStr {
	char text[];
};

struct CMFKey {
	char key[];
	char value[];
};

struct CMFVertex {
	double x, y, z, u, v;
};

struct CMFPolygon {
	u32 material;
	double plane[4];
	u32 vertexCount;
	CMFVertex vertices[vertexCount];
};

struct CMFEntity {
	char classname[];
	u32 keyCount;
	CMFKey keys[keyCount];
	u32 polyCount;
	CMFPolygon polygons[polyCount];
};

struct CMF {
	char magic[3];
	// both versions 2 or 3 are supported by engine
	// version 3 seems like it was intended for a never-implemented feature which ultimately surfaced in Portal's integrated add-on level support
	u8 version;
	if (version == 3) {
		u32 previewW;
		u32 previewH;
		u32 previewBMP[previewW * previewH];
	}
	u32 enableBlock2;
	if (enableBlock2 == 1) {
		u32 entityCount;
		u32 materialCount;
		char fileStr[];
		CMFStr materials[materialCount];
		CMFEntity entities[entityCount];
	}
};

CMF root @ 0;
