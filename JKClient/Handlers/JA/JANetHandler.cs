namespace JKClient {
	public abstract class JANetHandler : NetHandler {
		public override int MaxMessageLength => 49152;
		public JANetHandler(ProtocolVersion protocol) : base(protocol) {}
	}
}
