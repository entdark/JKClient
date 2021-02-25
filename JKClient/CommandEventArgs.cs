using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public class CommandEventArgs : EventArgs {
		public string Command { get; internal set; }
		public Command Arguments { get; internal set; }
		internal CommandEventArgs() {}
		internal CommandEventArgs(Command command) {
			this.Command = command.Argv(0);
			this.Arguments = command;
		}
	}
}
