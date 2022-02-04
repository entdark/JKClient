using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct GameState {
		private const int MaxConfigstringsJA = 1700;
		private const int MaxConfigstringsJO = 1400;
		private const int MaxConfigstringsQ3 = 1024;
		public const int MaxGameStateChars = 16000;
		public const int ServerInfo = 0;
		public const int SystemInfo = 1;
		public unsafe fixed int StringOffsets[GameState.MaxConfigstringsJA];
		public unsafe fixed sbyte StringData[GameState.MaxGameStateChars];
		public int DataCount;
		public static int MaxConfigstrings(ProtocolVersion protocol) {
			if (JKClient.IsQ3(protocol)) {
				return GameState.MaxConfigstringsQ3;
			} else if (JKClient.IsJO(protocol)) {
				return GameState.MaxConfigstringsJO;
			} else/* if (JKClient.IsJA(protocol))*/ {
				return GameState.MaxConfigstringsJA;
			}
		}
	}
}
