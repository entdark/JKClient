using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class JKClientException : Exception {
		private JKClientException() {}
		internal JKClientException(Exception exception) : base("Rethrown Exception", exception) {}
		internal JKClientException(string message) : base(message) {}
	}
}
