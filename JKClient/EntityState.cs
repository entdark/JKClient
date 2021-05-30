using System.Runtime.InteropServices;

namespace JKClient {
	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	internal struct EntityState {
		//Dummy is used as any value parsed in ReadDeltaEntity, as being offset by 0
		public int Dummy;
		public int Number;
		public int EntityType;
		public int EntityFlags;
		public int OtherEntityNum;
		public int GroundEntityNum;
		public int ClientNum;
		public int Event;
		public int EventParm;
		//IMPORTANT: update all entityStateFields in Message after adding new fields
		public static readonly EntityState Null = new EntityState();
	}
}
