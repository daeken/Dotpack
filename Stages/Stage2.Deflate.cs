using System.IO;
using System.IO.Compression;

namespace _ {
	static class _ {
		static byte[] S2Main(byte[] a, int s, int l, int d) {
			byte[] b = new byte[d];
			new DeflateStream(
					new MemoryStream(a, s, l), 
					CompressionMode.Decompress
				).Read(b, 0, b.Length);
			return b;
		}
	}
}
