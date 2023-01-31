using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct UserCommand {
		public const int Backup = 64;
		public const int Mask = (UserCommand.Backup - 1);
		public int ServerTime;
	}
}
