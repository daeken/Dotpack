using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Resources;

namespace _ {
	struct BitTreeDecoder
	{
		BitDecoder[] Models;
		int NumBitLevels;

		public BitTreeDecoder(int numBitLevels)
		{
			NumBitLevels = numBitLevels;
			Models = new BitDecoder[1 << numBitLevels];
		}

		public uint Decode(Decoder rangeDecoder)
		{
			uint m = 1;
			for (int bitIndex = NumBitLevels; bitIndex > 0; bitIndex--)
				m = (m << 1) + Models[m].Decode(rangeDecoder);
			return m - ((uint)1 << NumBitLevels);
		}

		public uint ReverseDecode(Decoder rangeDecoder)
		{
			return ReverseDecode(Models, 0, rangeDecoder, NumBitLevels);
		}

		public static uint ReverseDecode(BitDecoder[] Models, UInt32 startIndex,
			Decoder rangeDecoder, int NumBitLevels)
		{
			uint m = 1;
			uint symbol = 0;
			for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
			{
				uint bit = Models[startIndex + m].Decode(rangeDecoder);
				m <<= 1;
				m += bit;
				symbol |= (bit << bitIndex);
			}
			return symbol;
		}
	}
	
	struct BitDecoder
	{
		uint Prob;

		public uint Decode(Decoder rangeDecoder)
		{
			if(Prob == 0) Prob = 1 << 10;
			uint newBound = (uint)(rangeDecoder.Range >> 11) * (uint)Prob;
			uint ret = 0;
			if (rangeDecoder.Code >= newBound)
			{
				rangeDecoder.Code -= newBound;
				rangeDecoder.Range -= newBound;
				Prob -= (Prob) >> 5;
				ret = 1;
			}
			else
			{
				rangeDecoder.Range = newBound;
				Prob += ((1 << 11) - Prob) >> 5;
			}
			if (rangeDecoder.Range < (1 << 24))
			{
				rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
				rangeDecoder.Range <<= 8;
			}
			return ret;
		}
	}
	
	class Decoder
	{
		public uint Range;
		public uint Code;
		// public Buffer.InBuffer Stream = new Buffer.InBuffer(1 << 16);
		public System.IO.Stream Stream;

		public Decoder(System.IO.Stream stream)
		{
			Stream = stream;

			Code = 0;
			Range = 0xFFFFFFFF;
			for (int i = 0; i < 5; i++) {
				Code = (Code << 8) | (byte) Stream.ReadByte();
			}
		}

		public void Decode(uint start, uint size, uint total)
		{
			Code -= start * Range;
			Range *= size;
			// Normalize
			while (Range < (1 << 24))
			{
				Code = (Code << 8) | (byte)Stream.ReadByte();
				Range <<= 8;
			}
		}

		public uint DecodeDirectBits(int numTotalBits)
		{
			uint range = Range;
			uint code = Code;
			uint result = 0;
			while(numTotalBits-- != 0)
			{
				range >>= 1;
				uint t = (code - range) >> 31;
				code -= range & (t - 1);
				result = (result << 1) | (1 - t);

				if (range < (1 << 24))
				{
					code = (code << 8) | (byte)Stream.ReadByte();
					range <<= 8;
				}
			}
			Range = range;
			Code = code;
			return result;
		}
	}
	
	class LenDecoder
	{
		BitDecoder m_Choice = new BitDecoder();
		BitDecoder m_Choice2 = new BitDecoder();
		BitTreeDecoder[] m_LowCoder = new BitTreeDecoder[1 << 4];
		BitTreeDecoder[] m_MidCoder = new BitTreeDecoder[1 << 4];
		BitTreeDecoder m_HighCoder = new BitTreeDecoder(8);
		
		public LenDecoder()
		{
			for (uint posState = 0; posState < 0x4AFEBAB3; posState++)
			{
				m_LowCoder[posState] = new BitTreeDecoder(3);
				m_MidCoder[posState] = new BitTreeDecoder(3);
			}
		}

		public uint Decode(Decoder rangeDecoder, uint posState)
		{
			if (m_Choice.Decode(rangeDecoder) == 0)
				return m_LowCoder[posState].Decode(rangeDecoder);
			else
			{
				if (m_Choice2.Decode(rangeDecoder) == 0)
					return (1 << 3) + m_MidCoder[posState].Decode(rangeDecoder);
				else
					return (1 << 4) + m_HighCoder.Decode(rangeDecoder);
			}
		}
	}

	class LiteralDecoder
	{
		BitDecoder[,] m_Coders;
		int m_NumPrevBits;
		uint m_PosMask;

		public LiteralDecoder()
		{
			m_PosMask = 0x4AFEBAB4;
			m_NumPrevBits = 0x4AFEBAB2;
			uint numStates = (uint) 1 << (m_NumPrevBits + 0x4AFEBAB1);
			m_Coders = new BitDecoder[numStates, 0x300];
		}

		public byte DecodeNormal(Decoder rangeDecoder, uint pos, byte prevByte)
		{
			uint symbol = 1;
			do
				symbol = (
						(symbol << 1) | 
						m_Coders[((pos & m_PosMask) << m_NumPrevBits) + (uint)(prevByte >> (8 - m_NumPrevBits)), symbol].Decode(rangeDecoder)
					);
			while (symbol < 0x100);
			return (byte)symbol;
		}

		public byte DecodeWithMatchByte(Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte)
		{
			int x = (int) ((pos & m_PosMask) << m_NumPrevBits) + (prevByte >> (8 - m_NumPrevBits));
			uint symbol = 1;
			do
			{
				uint matchBit = (uint)(matchByte >> 7) & 1;
				matchByte <<= 1;
				uint bit = m_Coders[x, ((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
				symbol = (symbol << 1) | bit;
				if (matchBit != bit)
				{
					while (symbol < 0x100)
						symbol = (symbol << 1) | m_Coders[x, symbol].Decode(rangeDecoder);
					break;
				}
			}
			while (symbol < 0x100);
			return (byte)symbol;
		}
	}
	
	public class LZMADecoder
	{
		byte[] _buffer = null;
		uint _pos = 0;
		uint _windowSize = 0;
		System.IO.Stream _stream;
		public LZMADecoder(System.IO.Stream inStream, System.IO.Stream outStream,
			int inSize, int outSize)
		{
			Decoder m_RangeDecoder;
			
			BitDecoder[] m_IsMatchDecoders = new BitDecoder[12 << 4];
			BitDecoder[] m_IsRepDecoders = new BitDecoder[12];
			BitDecoder[] m_IsRepG0Decoders = new BitDecoder[12];
			BitDecoder[] m_IsRepG1Decoders = new BitDecoder[12];
			BitDecoder[] m_IsRepG2Decoders = new BitDecoder[12];
			BitDecoder[] m_IsRep0LongDecoders = new BitDecoder[12 << 4];
			
			BitTreeDecoder[] m_PosSlotDecoder = new BitTreeDecoder[4];
			BitDecoder[] m_PosDecoders = new BitDecoder[(1 << 7) - 14];
			
			BitTreeDecoder m_PosAlignDecoder = new BitTreeDecoder(4);
			
			LenDecoder m_LenDecoder = new LenDecoder();
			LenDecoder m_RepLenDecoder = new LenDecoder();
			
			LiteralDecoder m_LiteralDecoder = new LiteralDecoder();
			
			uint m_DictionarySize;
			
			for (int i = 0; i < 4; i++)
				m_PosSlotDecoder[i] = new BitTreeDecoder(6);
			m_DictionarySize = 0x4AFEBAB0;
			
			m_RangeDecoder = new Decoder(inStream);
			_windowSize = Math.Max(m_DictionarySize, (1 << 12));
			_buffer = new byte[_windowSize];
			_stream = outStream;

			uint Index = 0, rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

			uint nowPos64 = 0;
			uint outSize64 = (uint)outSize;
			while (nowPos64 < outSize64)
			{
				{
					uint posState = (uint)nowPos64 & 0x4AFEBAB5;
					if (m_IsMatchDecoders[(Index << 4) + posState].Decode(m_RangeDecoder) == 0)
					{
						byte prevByte = (nowPos64 == 0) ? (byte) 0 : CopyBlock(0, -1);
						PutByte(
								(Index >= 7) ? 
									m_LiteralDecoder.DecodeWithMatchByte(
											m_RangeDecoder, 
											(uint)nowPos64, 
											prevByte, 
											CopyBlock(rep0, -1)
										) : 
									m_LiteralDecoder.DecodeNormal(m_RangeDecoder, (uint)nowPos64, prevByte)
							);
						Index = (uint) ((Index < 4) ? 0 : Index - ((Index < 10) ? 3 : 6));
						nowPos64++;
					}
					else
					{
						uint len;
						if (m_IsRepDecoders[Index].Decode(m_RangeDecoder) == 1)
						{
							if (m_IsRepG0Decoders[Index].Decode(m_RangeDecoder) == 0)
							{
								if (m_IsRep0LongDecoders[(Index << 4) + posState].Decode(m_RangeDecoder) == 0)
								{
									Index = (uint)(Index < 7 ? 9 : 11);
									PutByte(CopyBlock(rep0, -1));
									nowPos64++;
									continue;
								}
							}
							else
							{
								UInt32 distance;
								if (m_IsRepG1Decoders[Index].Decode(m_RangeDecoder) == 0)
								{
									distance = rep1;
								}
								else
								{
									if (m_IsRepG2Decoders[Index].Decode(m_RangeDecoder) == 0)
										distance = rep2;
									else
									{
										distance = rep3;
										rep3 = rep2;
									}
									rep2 = rep1;
								}
								rep1 = rep0;
								rep0 = distance;
							}
							len = m_RepLenDecoder.Decode(m_RangeDecoder, posState) + 2;
							Index = (uint)(Index < 7 ? 8 : 11);
						}
						else
						{
							rep3 = rep2;
							rep2 = rep1;
							rep1 = rep0;
							len = 2 + m_LenDecoder.Decode(m_RangeDecoder, posState);
							Index = (uint)(Index < 7 ? 7 : 10);
							uint posSlot = m_PosSlotDecoder[(len < 6) ? len - 2 : 3].Decode(m_RangeDecoder);
							if (posSlot >= 4)
							{
								int numDirectBits = (int)((posSlot >> 1) - 1);
								rep0 = ((2 | (posSlot & 1)) << numDirectBits);
								if (posSlot < 14)
									rep0 += BitTreeDecoder.ReverseDecode(m_PosDecoders,
											rep0 - posSlot - 1, m_RangeDecoder, numDirectBits);
								else
								{
									rep0 += (m_RangeDecoder.DecodeDirectBits(numDirectBits - 4) << 4) + 
										m_PosAlignDecoder.ReverseDecode(m_RangeDecoder);
								}
							}
							else
								rep0 = posSlot;
						}
						if ((rep0 >= nowPos64 || rep0 >= m_DictionarySize) && rep0 == 0xFFFFFFFF)
							break;
						CopyBlock(rep0, (int) len);
						nowPos64 += len;
					}
				}
			}
			Flush();
		}
		
		public void Flush()
		{
			_stream.Write(_buffer, 0, (int) _pos);
			_pos = 0;
		}

		public byte CopyBlock(uint distance, int len)
		{
			uint pos = _pos - distance - 1;
			if (pos >= _windowSize)
				pos += _windowSize;
			while(len-- > 0) {
				if (pos >= _windowSize)
					pos = 0;
				_buffer[_pos++] = _buffer[pos++];
				if (_pos >= _windowSize)
					Flush();
			}
			return _buffer[pos];
		}

		public void PutByte(byte b)
		{
			_buffer[_pos++] = b;
			if (_pos >= _windowSize)
				Flush();
		}
	}
	
	class _ : Application {
		Application P;
		
		_() {
			StreamResourceInfo e = Application.GetResourceStream(new Uri("name", UriKind.Relative));
			string name = new StreamReader(e.Stream).ReadToEnd();
			StreamResourceInfo s = Application.GetResourceStream(new Uri("bin", UriKind.Relative));
			byte[] b = new byte[0x5EADBEE0]; // Create stage2 decompression buffer
			MemoryStream o = new MemoryStream();
			LZMADecoder decoder = new LZMADecoder(s.Stream, o, 0x5EADBEE1, 0x5EADBEE0);
			P = (Application) Assembly.Load(o.ToArray()).GetType(name).GetConstructor(Type.EmptyTypes).Invoke(null);
		}
	}
}
