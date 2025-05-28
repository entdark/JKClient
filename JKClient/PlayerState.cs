using System.Numerics;
using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct PlayerState {
		public int Dummy;
		public PlayerMoveType PlayerMoveType;
		public Vector3 Origin;
		public Vector3 Velocity;
		public PlayerMoveFlag PlayerMoveFlags;
		public int GroundEntityNum;
		public int EntityFlags;
		public int EventSequence;
		public unsafe fixed int Events[PlayerState.MaxEvents];
		public unsafe fixed int EventParms[PlayerState.MaxEvents];
		public int ExternalEvent;
		public int ExternalEventParm;
		public int ClientNum;
		public int Weapon;
		public Vector3 ViewAngles;
		public int EntityEventSequence;
		public int VehicleNum;
		public unsafe fixed int Stats[(int)Stat.Max];
		public unsafe fixed int Powerups[(int)ClientGame.Powerup.Max];
		//IMPORTANT: update all playerStateFields in Message after adding new fields
		public static readonly PlayerState Null = new PlayerState();
		public const int MaxEvents = 2;
	}
}
