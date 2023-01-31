using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct ClientEntity {
		public const int EventValidMsec = 300;
		public EntityState CurrentState;
		public EntityState NextState;
		public QuakeBoolean Interpolate;
		public QuakeBoolean CurrentValid;
		public int PreviousEvent;
		public int SnapshotTime;
	}
}
