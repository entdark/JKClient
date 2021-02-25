using System.Collections.Generic;
using System.Text;

namespace JKClient {
	internal sealed class InfoString : Dictionary<string, string> {
		public new string this[string key] {
			get => this.ContainsKey(key.ToLower()) ? base[key.ToLower()] : string.Empty;
			internal set => base[key.ToLower()] = value;
		}
		public InfoString(string infoString) {
			if (string.IsNullOrEmpty(infoString)) {
				return;
			}
			int index = infoString.IndexOf('\\');
			if (index < 0) {
				return;
			}
			if (index == 0) {
				infoString = infoString.Substring(index+1);
			}
			string []keyValuePairs = infoString.Split('\\');
			int length = keyValuePairs.Length & ~1;
			for (int i = 0; i < length; i+=2) {
				this[keyValuePairs[i].ToLower()] = keyValuePairs[i+1];
			}
		}
		public override string ToString() {
			if (this.Count <= 0) {
				return string.Empty;
			}
			var builder = new StringBuilder();
			foreach (var keyValuePair in this) {
				builder.Append('\\');
				builder.Append(keyValuePair.Key);
				builder.Append('\\');
				builder.Append(keyValuePair.Value);
			}
			return builder.ToString();
		}
	}
}
