using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct Snapshot {
		public const int NotActive = 2;
		private const int MaxEntities = 256;
		public bool Valid;
		public int Flags;
		public int ServerTime;
		public int MessageNum;
		public int DeltaNum;
		public PlayerState PlayerState;
		public PlayerState VehiclePlayerState;
		public int NumEntities;
		public int ParseEntitiesNum;
		public int ServerCommandNum;
		public void SetPlayerState(PlayerState ps) => this.PlayerState = ps;
		public int CGNumEntities => this.NumEntities > Snapshot.MaxEntities ? Snapshot.MaxEntities : this.NumEntities;
	}
}
