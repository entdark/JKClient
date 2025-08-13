using System.Numerics;

namespace JKClient {
	public class EntityEventArgs {
		public ClientGame.EntityEvent Event { get; internal init; }
		public ClientEntity Entity { get; internal init; }
		public int EventParm => Entity.CurrentState.EventParm;
		public Vector3 TrajectoryBase => Entity.CurrentState.PositionTrajectory.Base;
		internal EntityEventArgs(ClientGame.EntityEvent ev, ref ClientEntity cent) {
			this.Event = ev;
			this.Entity = cent;
		}
	}
}