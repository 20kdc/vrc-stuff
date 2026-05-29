#include <std/string.pat>
#include <std/core.pat>

struct OREFileHeader<auto basis> {
    char name[];
    u32 offset;
    u32 length;
    u8 data[length] @ basis + offset;
};

using OREDir;

struct OREDirRef<auto basis> {
    char name[];
    u32 ofs;
    OREDir dir @ (basis + ofs);
};

struct OREDirContent {
    u32 basis = $;
    u32 fileCount;
    OREFileHeader<basis> files[fileCount];
};

struct OREDir {
    u32 basis = $;
    u32 filesOfs;
    u32 dirCount;
    OREDirRef<basis> dirs[dirCount];
    OREDirContent root @ (basis + filesOfs);
};

OREDir root @ 0;
