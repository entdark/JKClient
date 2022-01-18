using System;

namespace JKClient {
	internal struct Snapshot {
		public const int MaxEntities = 256;
		public int Flags;
		public int ServerTime;
		public PlayerState PlayerState;
		public PlayerState VehiclePlayerState;
		public int NumEntities;
		public EntityState []Entities;
		public int ServerCommandSequence;
		public void SetPlayerState(PlayerState ps) => this.PlayerState = ps;
	}
}
