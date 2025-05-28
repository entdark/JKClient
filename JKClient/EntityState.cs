using System.Numerics;
using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct EntityState {
		//Dummy is used as any value parsed in ReadDeltaEntity, as being offset by 0
		public int Dummy;
		public int Number;
		public int EntityType;
		public int EntityFlags;
		public Trajectory PositionTrajectory;
		public Trajectory AnglesTrajectory;
		public Vector3 Origin;
		public Vector3 Origin2;
		public Vector3 Angles;
		public Vector3 Angles2;
		public int OtherEntityNum;
		public int GroundEntityNum;
		public int ModelIndex;
		public int ClientNum;
		public int Event;
		public int EventParm;
		public int Owner;
		public int Powerups;
		public int Weapon;
		public NPCClass NPCClass;
		public int VehicleNum;
		//IMPORTANT: update all entityStateFields in Message after adding new fields
		public static readonly EntityState Null = new EntityState();
	}
	internal enum NPCClass : int {
		None,
		Vehicle = 53
	}
}
