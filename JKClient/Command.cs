using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JKClient {
	public sealed class Command {
        private string []argv = null;
        public int Argc => this.argv != null ? this.argv.Length : 0;
        public string Argv(int arg) => arg >= this.Argc ? string.Empty : this.argv[arg];
        private Command() {}
        internal Command(string text) {
            this.argv = Command.TokenizeString(text).ToArray();
        }
        internal Command(string []argv) {
            this.argv = argv;
        }
        private static IEnumerable<string> TokenizeString(string line) {
            const char delimiter = ' ', textQualifier = '"';
            if (line == null) {
                yield break;
            } else {
                char prevChar = '\0';
                char nextChar = '\0';
                char currentChar = '\0';
                bool inString = false;
                var token = new StringBuilder();
                for (int i = 0; i < line.Length; i++) {
                    currentChar = line[i];
                    if (i > 0)
                        prevChar = line[i - 1];
                    else
                        prevChar = '\0';
                    if (i + 1 < line.Length)
                        nextChar = line[i + 1];
                    else
                        nextChar = '\0';
                    if (currentChar == textQualifier && (prevChar == '\0' || prevChar == delimiter || prevChar == '\n') && !inString) {
                        inString = true;
                        continue;
                    }
                    if (currentChar == textQualifier && (nextChar == '\0' || nextChar == delimiter || nextChar == '\n') && inString) {
                        inString = false;
                        continue;
                    }
                    if ((currentChar == delimiter || currentChar == '\n') && !inString) {
                        yield return token.ToString();
                        token = token.Remove(0, token.Length);
                        continue;
                    }
                    token = token.Append(currentChar);
                }
                yield return token.ToString();
            }
        }
    }
}
