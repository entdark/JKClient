using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class CommandEventArgs {
		public Command Command { get; init; }
		private CommandEventArgs() {}
		internal CommandEventArgs(Command command) {
			this.Command = command;
		}
	}
}
