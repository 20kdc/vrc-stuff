using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP {
	public static class KDCBSPPK3Writer {
		public static byte[] MakePK3(SortedDictionary<string, byte[]> files) {
			// pk3 is a fancy term for zip
			var memStream = new MemoryStream();
			var zip = new ZipArchive(memStream, ZipArchiveMode.Create, true, new UTF8Encoding(false));
			foreach (var (key, value) in files) {
				var entry = zip.CreateEntry(key);
				using (Stream stream = entry.Open()) {
					stream.Write(value, 0, value.Length);
				}
			}
			zip.Dispose();
			return memStream.ToArray();
		}
	}
}
