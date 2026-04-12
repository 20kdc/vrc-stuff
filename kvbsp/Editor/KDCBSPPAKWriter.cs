using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP {
	public static class KDCBSPPAKWriter {
		public static byte[] MakePAK(SortedDictionary<string, byte[]> files) {
			// https://quakewiki.org/wiki/.pak
			int headSize = 12;
			int ftSize = 0;
			int dataSize = 0;
			foreach (var (key, value) in files) {
				ftSize += 64;
				dataSize += value.Length;
			}
			// it's important to remember these are zero-initialized. we make use of this
			byte[] all = new byte[headSize + ftSize + dataSize];
			// now known! write header struct
			all[0] = (byte) 'P';
			all[1] = (byte) 'A';
			all[2] = (byte) 'C';
			all[3] = (byte) 'K';
			all[4] = (byte) 12;
			BitConverter.GetBytes((int) ftSize).CopyTo(all, 8);
			// write files
			int ftPos = headSize;
			int dataPos = headSize + ftSize;
			var enc = new UTF8Encoding(false);
			foreach (var (key, value) in files) {
				byte[] filename = enc.GetBytes(key);
				if (filename.Length > 56)
					throw new Exception("Filename too long for .pak: " + key);
				filename.CopyTo(all, ftPos);
				BitConverter.GetBytes((int) dataPos).CopyTo(all, ftPos + 56);
				BitConverter.GetBytes((int) value.Length).CopyTo(all, ftPos + 60);
				value.CopyTo(all, dataPos);
				ftPos += 64;
				dataPos += value.Length;
			}
			return all;
		}
	}
}
