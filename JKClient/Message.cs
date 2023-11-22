using System;
using System.Runtime.InteropServices;

namespace JKClient {
	internal sealed class Message {
		private const int FloatIntBits = 13;
		private const int FloatIntBias = (1<<(Message.FloatIntBits-1));
		private int bit = 0;
		private int bitSaved = 0;
		private bool oobSaved = false;
		private int readCountSaved = 0;
		public bool Overflowed { get; private set; }
		public bool OOB { get; private set; }
		public byte []Data { get; init; }
		public int MaxSize { get; init; }
		public int CurSize { get; set; } = 0;
		public int ReadCount { get; private set; } = 0;
		public int Bit {
			get => this.bit;
			private set => this.bit = value;
		}
		public Message() {}
		public Message(byte []data, int length, bool oob = false) {
			this.Data = data;
			this.MaxSize = length;
			this.OOB = oob;
		}
		public void Bitstream() {
			this.OOB = false;
		}
		public void SaveState() {
			this.bitSaved = this.bit;
			this.oobSaved = this.OOB;
			this.readCountSaved = this.ReadCount;
		}
		public void RestoreState() {
			this.bit = this.bitSaved;
			this.OOB = this.oobSaved;
			this.ReadCount = this.readCountSaved;
		}
		public void Reset() {
			Common.MemSet(this.Data, 0);
		}
		public unsafe void WriteBits(int value, int bits) {
			if (this.MaxSize - this.CurSize < 4) {
				this.Overflowed = true;
				return;
			}
			if (bits == 0 || bits < -31 || bits > 32) {
				throw new JKClientException($"WriteBits: bad bits {bits}");
			}
			if (bits < 0) {
				bits = -bits;
			}
			if (this.OOB) {
				if (bits == 8) {
					this.Data[this.CurSize] = (byte)value;
					this.CurSize += 1;
					this.bit += 8;
				} else if (bits == 16) {
					byte []temp = BitConverter.GetBytes((short)value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 2);
					this.CurSize += 2;
					this.bit += 16;
				} else if (bits == 32) {
					byte []temp = BitConverter.GetBytes(value);
					Array.Copy(temp, 0, this.Data, this.CurSize, 4);
					this.CurSize += 4;
					this.bit += 32;
				}
			} else {
				value &= (int)(0xffffffff>>(32-bits));
				if ((bits&7) != 0) {
					int nbits = bits&7;
					for (int i = 0; i < nbits; i++) {
						Huffman.Fast.PutBit((value&1), this.Data, ref this.bit);
						value >>= 1;
					}
					bits -= nbits;
				}
				if (bits != 0) {
					for (int i = 0; i < bits; i+=8) {
						Huffman.Fast.OffsetTransmit((value&0xff), this.Data, ref this.bit, Message.hDataEncoderTable);
						value >>= 8;
					}
				}
				this.CurSize = (this.bit>>3)+1;
			}
		}
		public void WriteByte(int c) {
			this.WriteBits(c, 8);
		}
		public unsafe void WriteData(byte []data, int length) {
			fixed (byte *d = data) {
				this.WriteData(d, length);
			}
		}
		public unsafe void WriteData(byte *data, int length) {
			for (int i = 0; i < length; i++) {
				this.WriteByte(data[i]);
			}
		}
		public void WriteShort(int c) {
			this.WriteBits(c, 16);
		}
		public void WriteLong(int c) {
			this.WriteBits(c, 32);
		}
		public unsafe void WriteString(sbyte []s) {
			if (s == null || s.Length <= 0) {
				this.WriteByte(0);
			} else {
				int l = Common.StrLen(s);
				if (l >= Common.MaxStringChars) {
					this.WriteByte(0);
					return;
				}
				byte []b = new byte[l+1];
				fixed (sbyte *ss = s) {
					Marshal.Copy((IntPtr)ss, b, 0, l);
				}
				this.WriteData(b, l+1);
			}
		}
		public void WriteDeltaUsercmdKey(int key, ref UserCommand from, ref UserCommand to) {
			if (to.ServerTime - from.ServerTime < 256) {
				this.WriteBits(1, 1);
				this.WriteBits(to.ServerTime - from.ServerTime, 8);
			} else {
				this.WriteBits(0, 1);
				this.WriteBits(to.ServerTime, 32);
			}
			this.WriteBits(0, 1);
		}
		public void BeginReading(bool oob = false) {
			this.ReadCount = 0;
			this.bit = 0;
			this.OOB = oob;
		}
		public unsafe int ReadBits(int bits) {
			int value = 0;
			bool sgn;
			if (bits < 0) {
				bits = -bits;
				sgn = true;
			} else {
				sgn = false;
			}
			if (this.OOB) {
				if (bits == 8) {
					value = this.Data[this.ReadCount];
					this.ReadCount += 1;
					this.bit += 8;
				} else if (bits == 16) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(short*)b;
					}
					this.ReadCount += 2;
					this.bit += 16;
				} else if (bits == 32) {
					fixed (byte *b = &this.Data[this.ReadCount]) {
						value = *(int*)b;
					}
					this.ReadCount += 4;
					this.bit += 32;
				}
			} else {
				int nbits = bits&7;
				if (nbits != 0) {
					value = Huffman.Fast.GetAllBits(this.Data, this.bit)&((1<<nbits)-1);
					this.bit += nbits;
					bits -= nbits;
				}
				if (bits != 0) {
					int get = 0;
					for (int i = 0; i < bits; i+=8) {
						Huffman.Fast.OffsetReceive(ref get, this.Data, ref this.bit, Message.hDataDecoderTable);
						value |= ((get&0xff)<<(i+nbits));
					}
				}
				this.ReadCount = (this.bit>>3)+1;
			}
			if (sgn) {
				if ((value & (1 << (bits - 1))) != 0) {
					value |= -1 ^ ((1 << bits) - 1);
				}
			}
			return value;
		}
		public int ReadByte() {
			int c = (byte)this.ReadBits(8);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadShort() {
			int c = (short)this.ReadBits(16);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public int ReadLong() {
			int c = this.ReadBits(32);
			if (this.ReadCount > this.CurSize) {
				c = -1;
			}
			return c;
		}
		public sbyte []ReadString() {
			sbyte []str = new sbyte[Common.MaxStringChars];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0) {
					break;
				}
				if (c == '%') {
					c = '.';
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.MaxStringChars-1);
			if (l <= sizeof(sbyte)*Common.MaxStringChars) {
				str[l] = 0;
			} else {
				str[sizeof(sbyte)*Common.MaxStringChars-1] = 0;
			}
			return str;
		}
		public string ReadStringAsString() {
			sbyte []str = this.ReadString();
			return Common.ToString(str);
		}
		public sbyte []ReadBigString() {
			sbyte []str = new sbyte[Common.BigInfoString];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0) {
					break;
				}
				if (c == '%') {
					c = '.';
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.BigInfoString-1);
			str[l] = 0;
			return str;
		}
		public string ReadBigStringAsString() {
			sbyte []str = this.ReadBigString();
			return Common.ToString(str);
		}
		public sbyte []ReadStringLine() {
			sbyte []str = new sbyte[Common.MaxStringChars];
			int l, c;
			l = 0;
			do {
				c = this.ReadByte();
				if (c == -1 || c == 0 || c == '\n') {
					break;
				}
				if (c == '%') {
					c = '.';
				}
				str[l] = (sbyte)c;
				l++;
			} while (l < sizeof(sbyte)*Common.MaxStringChars-1);
			str[l] = 0;
			return str;
		}
		public string ReadStringLineAsString() {
			sbyte []str = this.ReadStringLine();
			return Common.ToString(str);
		}
		//we don't really need any Data in assetsless client
		public void ReadData(byte []data, int len) {
			for (int i = 0; i < len; i++) {
				/*data[i] = (byte)*/this.ReadByte();
			}
		}
		public unsafe void ReadDeltaEntity(EntityState *from, EntityState *to, int number, IClientHandler clientHandler) {
			if (number < 0 || number >= Common.MaxGEntities) {
				throw new JKClientException($"Bad delta entity number: {number}");
			}
			if (this.ReadBits(1) == 1) {
				Common.MemSet(to, 0);
				to->Number = Common.MaxGEntities - 1;
				return;
			}
			if (this.ReadBits(1) == 0) {
				*to = *from;
				to->Number = number;
				return;
			}
			var fields = clientHandler.GetEntityStateFields();
			int lc = this.ReadByte();
			to->Number = number;
			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1) == 0) {
					*toF = *fromF;
				} else {
					int bits = fields[i].Bits;
					if (bits == 0) {
						if (this.ReadBits(1) == 0) {
							*(float*)toF = 0.0f;
						} else {
							if (this.ReadBits(1) == 0) {
								trunc = this.ReadBits(Message.FloatIntBits);
								trunc -= Message.FloatIntBias;
								*(float*)toF = trunc;
							} else {
								*toF = this.ReadBits(32);
							}
						}
					} else {
						if (this.ReadBits(1) == 0) {
							*toF = 0;
						} else {
							*toF = this.ReadBits(bits);
						}
					}
				}
				fields[i].Adjust?.Invoke(toF);
			}
			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				fields[i].Adjust?.Invoke(toF);
			}
		}
		public unsafe void ReadDeltaPlayerstate(PlayerState *from, PlayerState *to, bool isVehicle, IClientHandler clientHandler) {
			var nullPlayerState = PlayerState.Null;
			if (from == null) {
				from = &nullPlayerState;
			}
			*to = *from;
			bool isPilot() {
				return this.ReadBits(1) != 0;
			}
			var fields = clientHandler.GetPlayerStateFields(isVehicle, isPilot);
			int lc = this.ReadByte();
			int* fromF, toF;
			int trunc;
			for (int i = 0; i < lc; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				if (this.ReadBits(1) == 0) {
					*toF = *fromF;
				} else {
					int bits = fields[i].Bits;
					if (bits == 0) {
						if (this.ReadBits(1) == 0) {
							trunc = this.ReadBits(Message.FloatIntBits);
							trunc -= Message.FloatIntBias;
							*(float*)toF = trunc;
						} else {
							*toF = this.ReadBits(32);
						}
					} else {
						*toF = this.ReadBits(bits);
					}
				}
				fields[i].Adjust?.Invoke(toF);
			}
			for (int i = lc; i < fields.Count; i++) {
				fromF = (int*)((byte*)from + fields[i].Offset);
				toF = (int*)((byte*)to + fields[i].Offset);
				*toF = *fromF;
				fields[i].Adjust?.Invoke(toF);
			}
			if (this.ReadBits(1) != 0) {
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							if (i == 4
								&& (clientHandler.Protocol == (int)ProtocolVersion.Protocol25
								|| clientHandler.Protocol == (int)ProtocolVersion.Protocol26)) {
								to->Stats[i] = this.ReadBits(19);
							} else {
								to->Stats[i] = this.ReadShort();
							}
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							this.ReadShort();
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							this.ReadShort();
						}
					}
				}
				if (this.ReadBits(1) != 0) {
					int bits = this.ReadShort();
					for (int i = 0; i < 16; i++) {
						if ((bits & (1<<i)) != 0) {
							this.ReadLong();
						}
					}
				}
			}
		}
		private static readonly ushort []hDataDecoderTable = new ushort[2048] {
			2512, 2182, 512, 2763, 1859, 2808, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2603, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2767, 512, 1664,
			1731, 2116, 512, 2788, 1791, 1808, 512, 1840, 2153, 1921, 512, 2708, 2723, 1549, 512, 2046,
			1893, 2717, 512, 2602, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2711, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2762, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2729, 512, 2633, 1791, 1919, 512, 2184, 1917, 1802, 512, 2710, 1795, 1549, 512, 2172,
			2375, 2789, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2751, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2786, 512, 1281, 1640, 2641, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2586, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2773, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2781, 512, 2097, 2411, 2740, 512, 2396, 1794, 2024, 512, 2734, 1922, 2733, 512, 2112,
			1857, 2528, 512, 2593, 2079, 1288, 512, 2648, 2143, 1908, 512, 1281, 1640, 2770, 512, 1664,
			1731, 2169, 512, 2714, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2801, 2361, 512, 2400, 2328, 1288, 512, 1568, 2783, 2713, 512, 1281, 1858, 1923, 512, 1543,
			2816, 2182, 512, 2497, 1859, 2397, 512, 2794, 1918, 1988, 512, 1803, 2158, 2772, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2764, 512, 1664,
			1731, 2116, 512, 2620, 1791, 1808, 512, 1840, 2153, 1921, 512, 2716, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2722, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2709, 512, 2162, 1794, 2024, 512, 2168, 1922, 2735, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2779, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2705, 1791, 1919, 512, 2184, 1917, 1802, 512, 2642, 1795, 1549, 512, 2172,
			2394, 2645, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2771, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2585, 512, 2403,
			1798, 2619, 512, 1804, 2777, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2811, 1921, 512, 2402, 2601, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2719, 512, 1281, 2747, 2776, 512, 1543,
			1909, 2725, 512, 2097, 2445, 2765, 512, 2638, 1794, 2024, 512, 2444, 1922, 2774, 512, 2112,
			1857, 2727, 512, 2644, 2079, 1288, 512, 2800, 2143, 1908, 512, 1281, 1640, 2580, 512, 1664,
			1731, 2169, 512, 2646, 1791, 1919, 512, 2185, 1917, 1802, 512, 2588, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2623, 2350, 1288, 512, 1568, 2323, 2721, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2746, 1859, 2798, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2745, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2806, 512, 1664,
			1731, 2116, 512, 2796, 1791, 1808, 512, 1840, 2153, 1921, 512, 2582, 2761, 1549, 512, 2046,
			1893, 2793, 512, 2647, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2738, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2715, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2795, 512, 2750, 1791, 1919, 512, 2184, 1917, 1802, 512, 2732, 1795, 1549, 512, 2172,
			2375, 2604, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2813, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2743, 512, 1281, 1640, 2748, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2637, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2812, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2799, 512, 2097, 2411, 2802, 512, 2396, 1794, 2024, 512, 2649, 1922, 2595, 512, 2112,
			1857, 2528, 512, 2790, 2079, 1288, 512, 2634, 2143, 1908, 512, 1281, 1640, 2724, 512, 1664,
			1731, 2169, 512, 2730, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2605, 2361, 512, 2400, 2328, 1288, 512, 1568, 2787, 2810, 512, 1281, 1858, 1923, 512, 1543,
			2803, 2182, 512, 2497, 1859, 2397, 512, 2758, 1918, 1988, 512, 1803, 2158, 2598, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2726, 512, 1664,
			1731, 2116, 512, 2583, 1791, 1808, 512, 1840, 2153, 1921, 512, 2712, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2639, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2731, 512, 2162, 1794, 2024, 512, 2168, 1922, 2766, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2809, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2587, 1791, 1919, 512, 2184, 1917, 1802, 512, 2643, 1795, 1549, 512, 2172,
			2394, 2635, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2749, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2778, 512, 2403,
			1798, 2791, 512, 1804, 2775, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2805, 1921, 512, 2402, 2741, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2769, 512, 1281, 2739, 2780, 512, 1543,
			1909, 2737, 512, 2097, 2445, 2596, 512, 2757, 1794, 2024, 512, 2444, 1922, 2599, 512, 2112,
			1857, 2804, 512, 2744, 2079, 1288, 512, 2707, 2143, 1908, 512, 1281, 1640, 2782, 512, 1664,
			1731, 2169, 512, 2742, 1791, 1919, 512, 2185, 1917, 1802, 512, 2718, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2581, 2350, 1288, 512, 1568, 2323, 2597, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2763, 1859, 2808, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2603, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2767, 512, 1664,
			1731, 2116, 512, 2788, 1791, 1808, 512, 1840, 2153, 1921, 512, 2708, 2723, 1549, 512, 2046,
			1893, 2717, 512, 2602, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2711, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2762, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2729, 512, 2633, 1791, 1919, 512, 2184, 1917, 1802, 512, 2710, 1795, 1549, 512, 2172,
			2375, 2789, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2751, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2786, 512, 1281, 1640, 2641, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2586, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2773, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2781, 512, 2097, 2411, 2740, 512, 2396, 1794, 2024, 512, 2734, 1922, 2733, 512, 2112,
			1857, 2528, 512, 2593, 2079, 1288, 512, 2648, 2143, 1908, 512, 1281, 1640, 2770, 512, 1664,
			1731, 2169, 512, 2714, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2801, 2361, 512, 2400, 2328, 1288, 512, 1568, 2783, 2713, 512, 1281, 1858, 1923, 512, 1543,
			3063, 2182, 512, 2497, 1859, 2397, 512, 2794, 1918, 1988, 512, 1803, 2158, 2772, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2764, 512, 1664,
			1731, 2116, 512, 2620, 1791, 1808, 512, 1840, 2153, 1921, 512, 2716, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2722, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2709, 512, 2162, 1794, 2024, 512, 2168, 1922, 2735, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2779, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2705, 1791, 1919, 512, 2184, 1917, 1802, 512, 2642, 1795, 1549, 512, 2172,
			2394, 2645, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2771, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2585, 512, 2403,
			1798, 2619, 512, 1804, 2777, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2811, 1921, 512, 2402, 2601, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2719, 512, 1281, 2747, 2776, 512, 1543,
			1909, 2725, 512, 2097, 2445, 2765, 512, 2638, 1794, 2024, 512, 2444, 1922, 2774, 512, 2112,
			1857, 2727, 512, 2644, 2079, 1288, 512, 2800, 2143, 1908, 512, 1281, 1640, 2580, 512, 1664,
			1731, 2169, 512, 2646, 1791, 1919, 512, 2185, 1917, 1802, 512, 2588, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2623, 2350, 1288, 512, 1568, 2323, 2721, 512, 1281, 1858, 1923, 512, 1543,
			2512, 2182, 512, 2746, 1859, 2798, 512, 2360, 1918, 1988, 512, 1803, 2158, 2358, 512, 2180,
			1798, 2053, 512, 1804, 2745, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2806, 512, 1664,
			1731, 2116, 512, 2796, 1791, 1808, 512, 1840, 2153, 1921, 512, 2582, 2761, 1549, 512, 2046,
			1893, 2793, 512, 2647, 1801, 1288, 512, 1568, 2480, 2062, 512, 1281, 2145, 2738, 512, 1543,
			1909, 2150, 512, 2077, 2338, 2715, 512, 2162, 1794, 2024, 512, 2168, 1922, 2447, 512, 2334,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2321, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2795, 512, 2750, 1791, 1919, 512, 2184, 1917, 1802, 512, 2732, 1795, 1549, 512, 2172,
			2375, 2604, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2374, 2446, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2813, 512, 2413,
			1798, 2529, 512, 1804, 2344, 1288, 512, 2404, 2156, 2743, 512, 1281, 1640, 2748, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2395, 1921, 512, 2637, 2319, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2812, 512, 1281, 2365, 2410, 512, 1543,
			1909, 2799, 512, 2097, 2411, 2802, 512, 2396, 1794, 2024, 512, 2649, 1922, 2595, 512, 2112,
			1857, 2528, 512, 2790, 2079, 1288, 512, 2634, 2143, 1908, 512, 1281, 1640, 2724, 512, 1664,
			1731, 2169, 512, 2730, 1791, 1919, 512, 2185, 1917, 1802, 512, 2398, 1795, 1549, 512, 2098,
			2605, 2361, 512, 2400, 2328, 1288, 512, 1568, 2787, 2810, 512, 1281, 1858, 1923, 512, 1543,
			2803, 2182, 512, 2497, 1859, 2397, 512, 2758, 1918, 1988, 512, 1803, 2158, 2598, 512, 2180,
			1798, 2053, 512, 1804, 2464, 1288, 512, 2166, 2285, 2167, 512, 1281, 1640, 2726, 512, 1664,
			1731, 2116, 512, 2583, 1791, 1808, 512, 1840, 2153, 1921, 512, 2712, 2384, 1549, 512, 2046,
			1893, 2448, 512, 2639, 1801, 1288, 512, 1568, 2472, 2062, 512, 1281, 2145, 2376, 512, 1543,
			1909, 2150, 512, 2077, 2366, 2731, 512, 2162, 1794, 2024, 512, 2168, 1922, 2766, 512, 2407,
			1857, 2117, 512, 2100, 2240, 1288, 512, 2186, 2809, 1908, 512, 1281, 1640, 2242, 512, 1664,
			1731, 2359, 512, 2587, 1791, 1919, 512, 2184, 1917, 1802, 512, 2643, 1795, 1549, 512, 2172,
			2394, 2635, 512, 2171, 2187, 1288, 512, 1568, 2095, 2163, 512, 1281, 1858, 1923, 512, 1543,
			2450, 2749, 512, 2181, 1859, 2160, 512, 2183, 1918, 1988, 512, 1803, 2161, 2778, 512, 2403,
			1798, 2791, 512, 1804, 2775, 1288, 512, 2355, 2156, 2362, 512, 1281, 1640, 2380, 512, 1664,
			1731, 2052, 512, 2170, 1791, 1808, 512, 1840, 2805, 1921, 512, 2402, 2741, 1549, 512, 2046,
			1893, 2101, 512, 2159, 1801, 1288, 512, 1568, 2247, 2769, 512, 1281, 2739, 2780, 512, 1543,
			1909, 2737, 512, 2097, 2445, 2596, 512, 2757, 1794, 2024, 512, 2444, 1922, 2599, 512, 2112,
			1857, 2804, 512, 2744, 2079, 1288, 512, 2707, 2143, 1908, 512, 1281, 1640, 2782, 512, 1664,
			1731, 2169, 512, 2742, 1791, 1919, 512, 2185, 1917, 1802, 512, 2718, 1795, 1549, 512, 2098,
			2322, 2504, 512, 2581, 2350, 1288, 512, 1568, 2323, 2597, 512, 1281, 1858, 1923, 512, 1543
		};
		private static readonly ushort []hDataEncoderTable = new ushort[256] {
			34, 437, 1159, 1735, 2584, 280, 263, 1014, 341, 839, 1687, 183, 311, 726, 920, 2761,
			599, 1417, 7945, 8073, 7642, 16186, 8890, 12858, 3913, 6362, 2746, 13882, 7866, 1080, 1273, 3400,
			886, 3386, 1097, 11482, 15450, 16282, 12506, 15578, 2377, 6858, 826, 330, 10010, 12042, 8009, 1928,
			631, 3128, 3832, 6521, 1336, 2840, 217, 5657, 121, 3865, 6553, 6426, 4666, 3017, 5193, 7994,
			3320, 1287, 1991, 71, 536, 1304, 2057, 1801, 5081, 1594, 11642, 14106, 6617, 10938, 7290, 13114,
			4809, 2522, 5818, 14010, 7482, 5914, 7738, 9018, 3450, 11450, 5897, 2697, 3193, 4185, 3769, 3464,
			3897, 968, 6841, 6393, 2425, 775, 1048, 5369, 454, 648, 3033, 3145, 2440, 2297, 200, 2872,
			2136, 2248, 1144, 1944, 1431, 1031, 376, 408, 1208, 3608, 2616, 1848, 1784, 1671, 135, 1623,
			502, 663, 1223, 2007, 248, 2104, 24, 2168, 1656, 3704, 1400, 1864, 7353, 7241, 2073, 1241,
			4889, 5690, 6153, 15738, 698, 5210, 1722, 986, 12986, 3994, 3642, 9306, 4794, 794, 16058, 7066,
			4425, 8090, 4922, 714, 11738, 7194, 12762, 7450, 5001, 1562, 11834, 13402, 9914, 3290, 3258, 5338,
			905, 15386, 9178, 15306, 3162, 15050, 15930, 10650, 15674, 8522, 8250, 7114, 10714, 14362, 9786, 2266,
			1352, 4153, 1496, 518, 151, 15482, 12410, 2952, 7961, 8906, 1114, 58, 4570, 7258, 13530, 474,
			9, 15258, 3546, 6170, 4314, 2970, 7386, 14666, 7130, 6474, 14554, 5514, 15322, 3098, 15834, 3978,
			3353, 2329, 2458, 12170, 570, 1818, 11578, 14618, 1175, 8986, 4218, 9754, 8762, 392, 8282, 11290,
			7546, 3850, 11354, 12298, 15642, 14986, 8666, 20491, 90, 13706, 12186, 6794, 11162, 10458, 759, 582
		};
	}
}
