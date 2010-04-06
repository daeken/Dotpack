using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

/*
	0x5EADBEE* -- Variables replaced at packtime.
	0 -> Size of unpacked stage 2 assembly
	1 -> Size of unpacked stage 2 data
	2 -> Offset of stage 2 assembly in file
	3 -> Length of stage 2 assembly in file
	4 -> Length of stage 2 data in file
*/
namespace _ {
	static class _ {
		[STAThread]
		static void Main(string[] args) {
			byte[] a = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location); // Read all data from assembly
			byte[] b = new byte[0x5EADBEE0]; // Create stage2 decompression buffer
			int s = 0x5EADBEE2; // Get start of stage2 data
			int l = 0x5EADBEE3; // Get length (in file) of stage2 data
			new DeflateStream(
					new MemoryStream(a, s, l), 
					CompressionMode.Decompress
				).Read(b, 0, b.Length); // Inflate `l` bytes of stage2 data into `b`
			Assembly.Load(b).EntryPoint.Invoke(
					null, 
					new object[] {
							a, // Assembly data
							s+l, // Start of stage2 data in file
							0x5EADBEE4, // Length of stage2 data in file
							0x5EADBEE1, // Decompressed size of stage2 data
							args
						}
				); // Invoke entrypoint of stage2
		}
	}
}
