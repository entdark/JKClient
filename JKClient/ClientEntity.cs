using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
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
