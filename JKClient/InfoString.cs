using System;
using System.Collections.Generic;
using System.Text;

namespace JKClient {
	public sealed class InfoString : Dictionary<string, string> {
		private const char Delimiter = '\\';
		public new string this[string key] {
			get => this.ContainsKey(key) ? base[key] : string.Empty;
			internal set => base[key] = value;
		}
		private InfoString() {}
		internal InfoString(string infoString) : base(new InfoStringComparer()) {
			if (string.IsNullOrEmpty(infoString)) {
				return;
			}
			int index = infoString.IndexOf(InfoString.Delimiter);
			if (index < 0) {
				return;
			}
			string []keyValuePairs = infoString.Split(InfoString.Delimiter);
			int i = index != 0 ? 0 : 1;
			int length = (keyValuePairs.Length - i) & ~1;
			for (; i < length; i+=2) {
				this[keyValuePairs[i]] = keyValuePairs[i+1];
			}
		}
		public override string ToString() {
			if (this.Count <= 0) {
				return string.Empty;
			}
			var builder = new StringBuilder();
			foreach (var keyValuePair in this) {
				builder
					.Append(InfoString.Delimiter)
					.Append(keyValuePair.Key)
					.Append(InfoString.Delimiter)
					.Append(keyValuePair.Value);
			}
			return builder.ToString();
		}
		private class InfoStringComparer : EqualityComparer<string> {
			public override bool Equals(string x, string y) {
				return x.Equals(y, StringComparison.OrdinalIgnoreCase);
			}
			public override int GetHashCode(string obj) {
				return obj.GetHashCode();
			}
		}
	}
}
