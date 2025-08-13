using System.Numerics;
using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public unsafe struct ClientEntity {
		internal const int EventValidMsec = 300;
		internal EntityState CurrentState;
		internal EntityState NextState;
		internal QuakeBoolean Interpolate;
		internal QuakeBoolean CurrentValid;
		internal int PreviousEvent;
		internal int SnapshotTime;
		internal int LastVehTime;
		internal QuakeBoolean Added;
		public Vector3 LerpOrigin { get; internal set; }
		public Vector3 LerpAngles { get; internal set; }
		public Vector3 Angles => this.CurrentState.Angles;
		public Vector3 Angles2 => this.CurrentState.Angles2;
		public Vector3 Origin2 => this.CurrentState.Origin2;
		public int ClientNum => this.CurrentState.ClientNum;
		public int Owner => this.CurrentState.Owner;
		public bool Valid => this.CurrentValid/* && this.Added*/;
	}
}
