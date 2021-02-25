using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct UserCommand {
		public const int CommandBackup = 64;
		public const int CommandMask = (UserCommand.CommandBackup - 1);
		public int ServerTime;
	}
}
