using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class CommandEventArgs {
		public Command Command { get; private set; }
		public Command UTF8Command { get; private set; }
		internal CommandEventArgs() {}
		internal CommandEventArgs(Command command, Command utf8Command = null) {
			this.Command = command;
			this.UTF8Command = utf8Command;
		}
	}
}
