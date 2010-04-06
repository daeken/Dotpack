namespace Dotpack

import SevenZip
import SevenZip.Compression.LZMA
import Mono.Cecil
import System
import System.IO
import Ionic.Zlib

class Dotpack:
	def constructor(args as (string)):
		if args.Length != 2 and args.Length != 3:
			Usage()
			return
		
		if args.Length == 3:
			compression = args[2]
		else:
			compression = 'LZMA'
		
		infile, outfile = args
		kind, sizes as (int), stage2, data2 = CreateStage2(infile, compression)
		print 'Stage 2 unpacker: {0} -> {1}' % (sizes[2], sizes[3])
		print 'Real assembly: {0} -> {1}' % (sizes[0], sizes[1])
		s1size = CreateStage1(kind, sizes[2], stage2, sizes[0], data2, outfile)
		
		overhead = s1size + sizes[3]
		total = overhead + sizes[1]
		print 'Total size: {0} -> {1} ({2}%)' % (sizes[0], total, (cast(single, total) / sizes[0]) * 100)
		print 'Total unpacker overhead: {0} ({1}%)' % (overhead, (cast(single, overhead) / total) * 100)
	
	def ReplaceInt(data as (byte), orig as int, new as int):
		for i in range(data.Length-3):
			if (
					data[i] == (orig & 0xFF) and data[i+1] == ((orig >> 8) & 0xFF) and 
					data[i+2] == ((orig >> 16) & 0xFF) and data[i+3] == (orig >> 24)
				):
				data[i] = new & 0xFF
				data[i+1] = (new >> 8) & 0xFF
				data[i+2] = (new >> 16) & 0xFF
				data[i+3] = new >> 24
	
	def CreateStage1(kind as AssemblyKind, stage2Size as int, stage2 as (byte), data2Size as int, data2 as (byte), outfile as string):
		asm = AssemblyFactory.GetAssembly('Obj/Stage1.Deflate.exe')
		asm.Kind = kind
		#Mono.Cecil.Binary.PEOptionalHeader.NTSpecificFieldsHeader.DefaultFileAlignment = 0x200
		
		stage1 as (byte)
		AssemblyFactory.SaveAssembly(asm, stage1)
		size = stage1.Length
		while stage1[size-1] == 0:
			size -= 1
		print 'Stage 1 size: {0}' % (size, )
		
		ReplaceInt(stage1, 0x5EADBEE0, stage2Size)
		ReplaceInt(stage1, 0x5EADBEE1, data2Size)
		ReplaceInt(stage1, 0x5EADBEE2, size)
		ReplaceInt(stage1, 0x5EADBEE3, stage2.Length)
		ReplaceInt(stage1, 0x5EADBEE4, data2.Length)
		
		fp = File.Create(outfile)
		fp.Write(stage1, 0, size)
		fp.Flush()
		
		fp.Position = size
		bw = BinaryWriter(fp)
		bw.Write(stage2)
		bw.Write(data2)
		fp.Close()
		
		return size
	
	LZMAProperties = (
			1 << 27, 1, 4, 0, 2, 128, 'bt2', false
		)
	
	def CreateStage2(infile as string, compression as string) as List:
		asm = AssemblyFactory.GetAssembly(infile)
		kind = asm.Kind
		ep = asm.EntryPoint
		hasParams = ep.Parameters != null and ep.Parameters.Count != 0
		sizes = array [of int](4)
		fp = File.OpenRead(infile)
		data = array [of byte](fp.Length)
		fp.Read(data, 0, data.Length)
		if compression == 'Deflate':
			ms = MemoryStream()
			cs = DeflateStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression)
			cs.Write(data, 0, data.Length)
			cs.Close()
			cdata = ms.ToArray()
		elif compression == 'LZMA':
			msin = MemoryStream(data, 0, data.Length)
			msout = MemoryStream()
			encoder = Encoder()
			propIDs = (
					CoderPropID.DictionarySize,
					CoderPropID.PosStateBits,
					CoderPropID.LitContextBits,
					CoderPropID.LitPosBits,
					CoderPropID.Algorithm,
					CoderPropID.NumFastBytes,
					CoderPropID.MatchFinder,
					CoderPropID.EndMarker
				)
			
			encoder.SetCoderProperties(propIDs, LZMAProperties)
			encoder.Code(msin, msout, -1, -1, null)
			cdata = msout.ToArray()
		else:
			cdata = null
		sizes[0] = data.Length
		sizes[1] = cdata.Length
		
		if hasParams:
			p = '.Params'
		else:
			p = ''
		asm = AssemblyFactory.GetAssembly('Obj/Stage2.' + compression + p + '.dll')
		mod = asm.MainModule
		for type as TypeDefinition in mod.Types:
			if type.Name == '_':
				asm.EntryPoint = type.Methods[0]
		
		#Mono.Cecil.Binary.PEOptionalHeader.NTSpecificFieldsHeader.DefaultFileAlignment = 1
		binary as (byte)
		AssemblyFactory.SaveAssembly(asm, binary)
		if compression == 'LZMA':
			ReplaceInt(binary, 0x4AFEBAB0, LZMAProperties[0]) # Dictionary size
			ReplaceInt(binary, 0x4AFEBAB1, LZMAProperties[3]) # Literal position bits
			ReplaceInt(binary, 0x4AFEBAB2, LZMAProperties[2]) # Literal context bits
			ReplaceInt(binary, 0x4AFEBAB3, LZMAProperties[1]) # Position state bits
		
		ms = MemoryStream()
		cs = DeflateStream(ms, CompressionMode.Compress, CompressionLevel.BestCompression)
		cs.Write(binary, 0, binary.Length)
		cs.Close()
		bdata = ms.ToArray()
		sizes[2] = binary.Length
		sizes[3] = bdata.Length
		
		return [kind, sizes, bdata, cdata]
	
	def Usage():
		print 'dotpack.exe [infile] [outfile]'

Dotpack(argv)
