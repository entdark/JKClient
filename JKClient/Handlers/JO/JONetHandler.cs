namespace JKClient {
	public abstract class JONetHandler : NetHandler {
		public override int MaxMessageLength => 16384;
		public JONetHandler(ProtocolVersion protocol) : base(protocol) {}
	}
}
