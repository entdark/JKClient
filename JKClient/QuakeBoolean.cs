using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Explicit, Size = 4)]
	internal readonly struct QuakeBoolean {
		[FieldOffset(0)]
		private readonly int value;
		private QuakeBoolean(bool value) {
			this.value = value ? 1 : 0;
		}
		public static implicit operator bool(QuakeBoolean b) => b.value != 0;
		public static implicit operator QuakeBoolean(bool b) => new QuakeBoolean(b);
	}
}
