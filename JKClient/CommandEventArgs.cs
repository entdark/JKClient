namespace JKClient {
	public sealed class CommandEventArgs {
		public Command Command { get; init; }
		private CommandEventArgs() {}
		internal CommandEventArgs(Command command) {
			this.Command = command;
		}
	}
}
