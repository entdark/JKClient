namespace JKClient {
	public interface INetHandler {
		ProtocolVersion Protocol { get; }
		int MaxMessageLength { get; }
	}
	public abstract class NetHandler : INetHandler {
		public virtual ProtocolVersion Protocol { get; private set; }
		public abstract int MaxMessageLength { get; }
		public NetHandler(ProtocolVersion protocol) {
			this.Protocol = protocol;
		}
	}
}
