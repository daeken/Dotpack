using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace _ {
	static class _ {
		static void S2Main(byte[] a, int s, int l, int d, string[] args) {
			byte[] b = new byte[d];
			new DeflateStream(
					new MemoryStream(a, s, l), 
					CompressionMode.Decompress
				).Read(b, 0, b.Length);
			Assembly.Load(b).EntryPoint.Invoke(
					null, 
					new object[] {
#if WITHARGS
							args
#endif
						}
				);
		}
	}
}
