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
	5 -> Start data shared between stage 1 and stage2
*/
namespace _ {
	static class _ {
		[STAThread]
		static void Main(string[] args) {
			byte[] a = File.ReadAllBytes(Assembly.GetExecutingAssembly().Location); // Read all data from assembly
			byte[] b = new byte[0x5EADBEE0]; // Create stage2 decompression buffer
			int s = 0x5EADBEE2; // Get start of stage2 data
			int l = 0x5EADBEE3; // Get length (in file) of stage2 data
			int o = 0x5EADBEE5; // Get overlapping start data
			Array.Copy(a, b, o);
			new DeflateStream(
					new MemoryStream(a, s, l), 
					CompressionMode.Decompress
				).Read(b, o, b.Length-o); // Inflate stage2 data after overlap
			
			Assembly.Load(
					(byte[]) Assembly.Load(b).EntryPoint.Invoke(
							null, 
							new object[] {
									a, // Assembly data
									s+l, // Start of stage2 data in file
									0x5EADBEE4, // Length of stage2 data in file
									0x5EADBEE1 // Decompressed size of stage2 data
								}
						)
				).EntryPoint.Invoke(
					null, 
#if WITHARGS
					new object[] {
							args
						}
#else
					null
#endif
				);
		}
	}
}
