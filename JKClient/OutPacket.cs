using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct OutPacket {
		public int CommandNumber;
		public int ServerTime;
		public int RealTime;
	}
}
