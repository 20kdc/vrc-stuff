/* QUAKE 2 BSP FORMAT (partial) */

struct Lump<T> {
    u32 ofs;
    u32 len;
    T data[len / sizeof(T)] @ ofs;
};

struct Model {
    float minX, minY, minZ, maxX, maxY, maxZ;
    float originX, originY, originZ;
    s32 node;
    u32 firstFace, numFaces;
};

struct Edge {
    u16 a, b;
};

struct Vertex {
    float x, y, z;
};

struct Face {
    u16 plane;
    s16 side;
    u32 firstEdge;
    u16 numEdges;
    s16 texinfo;
    s8 styles[4];
    s32 lightofs;
};

struct Plane {
    float nX;
    float nY;
    float nZ;
    float d;
    u32 type; // optimization, ignore
};

struct Brush {
    u32 firstSide;
    u32 numSides;
    u32 contents;
};

struct BrushSide {
    u16 plane;
    s16 texinfo;
};

struct BSPHead {
    char magic[4];
    u32 version;
    Lump<u8> lump0; // ent
    Lump<Plane> planes1;
    Lump<Vertex> vertices2;
    Lump<u8> lump3;
    Lump<u8> lump4;
    Lump<u8> texinfo5;
    Lump<Face> faces6;
    Lump<u8> lump7;
    Lump<u8> lump8;
    Lump<u8> lump9;
    Lump<u8> lump10;
    Lump<Edge> edges11;
    Lump<s32> surfedges12;
    Lump<Model> models13;
    Lump<Brush> brushes14;
    Lump<BrushSide> brushsides15;
    Lump<u8> lump16;
    Lump<u8> lump17;
    Lump<u8> lump18;
};

BSPHead file @ 0;
