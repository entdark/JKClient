using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct PlayerState {
		public int Dummy;
		public PlayerMoveType PlayerMoveType;
		public int PlayerMoveFlags;
		public int GroundEntityNum;
		public int EntityFlags;
		public int EventSequence;
		public unsafe fixed int Events[PlayerState.MaxEvents];
		public unsafe fixed int EventParms[PlayerState.MaxEvents];
		public int ExternalEvent;
		public int ExternalEventParm;
		public int ClientNum;
		public int EntityEventSequence;
		public int VehicleNum;
		public unsafe fixed int Stats[(int)Stat.Max];
		//IMPORTANT: update all playerStateFields in Message after adding new fields
		public static readonly PlayerState Null = new PlayerState();
		public const int MaxEvents = 2;
		public unsafe void ToEntityState(ref EntityState es) {
			if (this.PlayerMoveType == PlayerMoveType.Intermission || this.PlayerMoveType == PlayerMoveType.Spectator) {
				es.EntityType = (int)EntityType.Invisible;
			} else if (this.Stats[(int)Stat.Health] <= Common.GibHealth) {
				es.EntityType = (int)EntityType.Invisible;
			} else {
				es.EntityType = (int)EntityType.Player;
			}
			es.Number = es.ClientNum = this.ClientNum;
			if (this.ExternalEvent != 0) {
				es.Event = this.ExternalEvent;
				es.EventParm = this.ExternalEventParm;
			} else if (this.EntityEventSequence < this.EventSequence) {
				if (this.EntityEventSequence < this.EventSequence - PlayerState.MaxEvents) {
					this.EntityEventSequence = this.EventSequence - PlayerState.MaxEvents;
				}
				int sequence = this.EntityEventSequence & (PlayerState.MaxEvents-1);
				es.Event = this.Events[sequence] | ((this.EntityEventSequence & 3) << 8);
				es.EventParm = this.EventParms[sequence];
				this.EntityEventSequence++;
			}
			es.GroundEntityNum = this.GroundEntityNum;
		}
	}
}
