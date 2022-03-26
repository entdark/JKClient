namespace JKClient {
	public interface INetHandler {
		int Protocol { get; }
		int MaxMessageLength { get; }
	}
	public abstract class NetHandler : INetHandler {
		public virtual int Protocol { get; protected set; }
		public abstract int MaxMessageLength { get; }
		public NetHandler(int protocol) {
			this.Protocol = protocol;
		}
	}
}
