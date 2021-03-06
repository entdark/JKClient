﻿using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public static class Common {
		internal const int MaxStringChars = 1024;
		internal const int BigInfoString = 8192;
		internal const int MaxClients = 32;
		internal const int GEntitynumBits = 10;
		internal const int MaxGEntities = (1<<Common.GEntitynumBits);
		internal const int GibHealth = -40;
		internal static long Milliseconds => (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond);
#if NETSTANDARD2_1
		private static Action<IntPtr, byte, int> memSetDelegate;
#endif
		public static Encoding Encoding { get; set; }
		public static bool AllowAllEncodingCharacters { get; set; } = false;
		static Common() {
			Common.Encoding = Encoding.GetEncoding("windows-1252");
#if NETSTANDARD2_1
			var memSetILMethod = new DynamicMethod(
				"MemSetIL",
				MethodAttributes.Assembly | MethodAttributes.Static, CallingConventions.Standard,
				null,
				new []{
					typeof(IntPtr),
					typeof(byte),
					typeof(int)
				},
				typeof(Common),
				true
			);
			var generator = memSetILMethod.GetILGenerator();
			generator.Emit(OpCodes.Ldarg_0);
			generator.Emit(OpCodes.Ldarg_1);
			generator.Emit(OpCodes.Ldarg_2);
			generator.Emit(OpCodes.Initblk);
			generator.Emit(OpCodes.Ret);
			memSetDelegate = (Action<IntPtr, byte, int>)memSetILMethod.CreateDelegate(typeof(Action<IntPtr, byte, int>));
#endif
		}
		internal static void MemSet(object dst, byte val, int size) {
			var gcHandle = GCHandle.Alloc(dst, GCHandleType.Pinned);
			Common.MemSet(gcHandle.AddrOfPinnedObject(), val, size);
			gcHandle.Free();
		}
		internal static unsafe void MemSet(void *dst, byte val, int size) {
			Common.MemSet((IntPtr)dst, val, size);
		}
		internal static unsafe void MemSet(IntPtr dst, byte val, int size) {
#if NETSTANDARD2_1
			memSetDelegate(dst, val, size);
#else
			for (int i = 0; i < size; i++) {
				((byte*)dst)[i] = val;
			}
#endif
		}
		internal static unsafe int StrLen(sbyte *str) {
			sbyte* s;
			for (s = str; *s != 0; s++);
			return (int)(s - str);
		}
		internal static unsafe int StrLen(sbyte []str) {
			fixed (sbyte *s = str) {
				return Common.StrLen(s);
			}
		}
		internal static unsafe int StrCmp(sbyte *s1, sbyte *s2, int n = 99999) {
			if (s1 == null) {
				if (s2 == null) {
					return 0;
				} else {
					return -1;
				}
			} else if (s2 == null) {
				return 1;
			}
			int c1, c2;
			do {
				c1 = *s1++;
				c2 = *s2++;
				if ((n--) == 0) {
					return 0;
				}
				if (c1 != c2) {
					return c1 < c2 ? -1 : 1;
				}
			} while (c1 != 0);
			return 0;
		}
		internal static unsafe int StriCmp(sbyte *s1, sbyte *s2, int n = 99999) {
			if (s1 == null) {
				if (s2 == null) {
					return 0;
				} else {
					return -1;
				}
			} else if (s2 == null) {
				return 1;
			}
			int c1, c2;
			do {
				c1 = *s1++;
				c2 = *s2++;
				if ((n--) == 0) {
					return 0;
				}
				if (c1 != c2) {
					if (c1 >= 97 && c1 <= 122) { //'a' 'z'
						c1 -= (97 - 65); //'a' 'A'
					}
					if (c2 >= 97 && c2 <= 122) { //'a' 'z'
						c2 -= (97 - 65); //'a' 'A'
					}
					if (c1 != c2) {
						return c1 < c2 ? -1 : 1;
					}
				}
			} while (c1 != 0);
			return 0;
		}
		internal static int Atoi(this string str) {
			return int.TryParse(str, out int integer) ? integer : 0;
		}
		internal static int HashKey(sbyte []str, int maxlen) {
			int hash = 0;
			for (int i = 0; i < maxlen && str[i] != 0; i++) {
				hash += str[i] * (119 + i);
			}
			hash = (hash ^ (hash >> 10) ^ (hash >> 20));
			return hash;
		}
		internal static unsafe string ToString(byte *b, int len) {
			byte []s = Common.FilterUnusedEncodingCharacters(b, len);
			return Common.Encoding.GetString(s).TrimEnd('\0');
		}
		internal static unsafe string ToString(sbyte* b, int len) {
			return Common.ToString((byte*)b, len);
		}
		internal static unsafe string ToString(byte []b) {
			fixed (byte *s = b) {
				return Common.ToString(s, b.Length);
			}
		}
		internal static string ToString(sbyte []b) {
			return Common.ToString((byte[])(Array)b);
		}
		private static unsafe byte []FilterUnusedEncodingCharacters(byte *b, int len) {
			byte []s = new byte[len];
			Marshal.Copy((IntPtr)b, s, 0, len);
			//fonts in JK don't support fancy characters, so we won't
			if (!Common.AllowAllEncodingCharacters) {
				for (int i = 0; i < len; i++) {
					if (s[i] > 126 && s[i] < 160) { //'~' ' '
						s[i] = 46; //'.'
					}
				}
			}
			return s;
		}
	}
}
