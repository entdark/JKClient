using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct OutPacket {
		public int CommandNumber;
		public int ServerTime;
		public int RealTime;
	}
}
