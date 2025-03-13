using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public sealed partial class JKClient : IJKClientImport {
		private readonly StringBuilder bigInfoString = new StringBuilder(Common.BigInfoString, Common.BigInfoString);
		public event Action<CommandEventArgs> ServerCommandExecuted;
		private void SetTime() {
			if (this.Status != ConnectionStatus.Active) {
				if (this.Status != ConnectionStatus.Primed) {
					return;
				}
				if (this.newSnapshots) {
					this.newSnapshots = false;
					if ((this.snap.Flags & ClientSnapshot.NotActive) != 0) {
						return;
					}
					this.Status = ConnectionStatus.Active;
					this.connectTCS?.TrySetResult(true);
				}
				if (this.Status != ConnectionStatus.Active) {
					return;
				}
			}
#if false
			this.serverTime = this.snap.ServerTime;
#else
			if (!this.snap.Valid) {
				throw new JKClientException("SetTime: !this.snap.Valid");
			}
			if (this.snap.ServerTime < this.oldFrameServerTime) {
				throw new JKClientException("this.snap.ServerTime < this.oldFrameServerTime");
			}
			this.oldFrameServerTime = this.snap.ServerTime;
			this.serverTime = this.realTime + this.serverTimeDelta;
			if (this.serverTime < this.oldServerTime) {
				this.serverTime = this.oldServerTime;
			}
			this.oldServerTime = this.serverTime;
			if (this.realTime + this.serverTimeDelta >= this.snap.ServerTime - 5) {
				this.extrapolatedSnapshot = true;
			}
			if (this.newSnapshots) {
				this.newSnapshots = false;
				int newDelta = this.snap.ServerTime - this.realTime;
				int deltaDelta = Math.Abs(newDelta - this.serverTimeDelta);
				if (deltaDelta > 500) {
					this.serverTimeDelta = newDelta;
					this.oldServerTime = this.snap.ServerTime;
					this.serverTime = this.snap.ServerTime;
				} else if (deltaDelta > 100) {
					this.serverTimeDelta = (this.serverTimeDelta + newDelta) >> 1;
				} else if (this.extrapolatedSnapshot) {
					this.extrapolatedSnapshot = false;
					this.serverTimeDelta -= 2;
				} else {
					this.serverTimeDelta++;
				}
			}
#endif
		}
		private ClientGame InitClientGame() {
			this.Status = ConnectionStatus.Primed;
			var clientGame = this.ClientHandler.CreateClientGame(this, this.serverMessageSequence, this.serverCommandSequence, this.clientNum);
			if (clientGame == null) {
				throw new JKClientException("Failed to create client game for unknown client");
			}
			return clientGame;
		}
		private unsafe void ConfigstringModified(in Command command, in sbyte []s) {
			int index = command[1].Atoi();
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"ConfigstringModified: bad index {index}");
			}
			int start = 4 + command[1].Length;
			if (s[start] == '\"') {
				start++;
			}
			int blen = command[2].Length;
			if (blen == 0) {
				blen = 1;
			}
			sbyte []b = new sbyte[blen];
			Array.Copy(s, start, b, 0, b.Length);
			fixed (sbyte *old = &this.gameState.StringData[this.gameState.StringOffsets[index]],
				sb = b) {
				if (Common.StriCmp(old, sb) == 0) {
					return;
				}
				var oldGs = this.gameState;
				Common.MemSet(ref this.gameState, 0);
				this.gameState.DataCount = 1;
				int len;
				for (int i = 0; i < this.MaxConfigstrings; i++) {
					byte []dup = new byte[GameState.MaxGameStateChars];
					if (i == index) {
						len = Common.StrLen(sb);
						Marshal.Copy((IntPtr)sb, dup, 0, len);
					} else {
						sbyte *bdup = &oldGs.StringData[oldGs.StringOffsets[i]];
						if (bdup[0] == 0) {
							continue;
						}
						len = Common.StrLen(bdup);
						Marshal.Copy((IntPtr)bdup, dup, 0, len);
					}
					if (len + 1 + this.gameState.DataCount > GameState.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						Marshal.Copy(dup, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
					}
					this.gameState.DataCount += len + 1;
				}
			}
			if (index == GameState.SystemInfo) {
				this.SystemInfoChanged();
			} else if (index == GameState.ServerInfo) {
				this.ServerInfoChanged?.Invoke(this.ServerInfo);
			}
		}
		int IJKClientImport.MaxClients => this.ClientHandler.MaxClients;
		ServerInfo IJKClientImport.ServerInfo => this.ServerInfo;
		void IJKClientImport.ExecuteServerCommand(CommandEventArgs eventArgs) {
			this.ServerCommandExecuted?.Invoke(eventArgs);
		}
		void IJKClientImport.NotifyClientInfoChanged() {
			this.ServerInfoChanged?.Invoke(this.ServerInfo);
		}
		void IJKClientImport.GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime) {
			snapshotNumber = this.snap.MessageNum;
			serverTime = this.snap.ServerTime;
		}
		bool IJKClientImport.GetSnapshot(in int snapshotNumber, ref Snapshot snapshot) {
			if (snapshotNumber > this.snap.MessageNum) {
				throw new JKClientException("GetSnapshot: snapshotNumber > this.snap.MessageNum");
			}
			if (this.snap.MessageNum - snapshotNumber >= JKClient.PacketBackup) {
				return false;
			}
			ref var clSnapshot = ref this.snapshots[snapshotNumber & JKClient.PacketMask];
			if (!clSnapshot.Valid) {
				return false;
			}
			if (this.parseEntitiesNum - clSnapshot.ParseEntitiesNum > JKClient.MaxParseEntities) {
				return false;
			}
			snapshot.Flags = clSnapshot.Flags;
			snapshot.ServerCommandSequence = clSnapshot.ServerCommandNum;
			snapshot.ServerTime = clSnapshot.ServerTime;
			snapshot.PlayerState = clSnapshot.PlayerState;
			snapshot.VehiclePlayerState = clSnapshot.VehiclePlayerState;
			snapshot.NumEntities = Math.Min(clSnapshot.NumEntities, Snapshot.MaxEntities);
			for (int i = 0; i < snapshot.NumEntities; i++) {
				int entNum = (clSnapshot.ParseEntitiesNum + i) & (JKClient.MaxParseEntities-1);
				snapshot.Entities[i] = this.parseEntities[entNum];
			}
			return true;
		}
		unsafe bool IJKClientImport.GetServerCommand(in int serverCommandNumber, out Command command) {
			if (serverCommandNumber <= this.serverCommandSequence - this.MaxReliableCommands) {
				throw new JKClientException("GetServerCommand: a reliable command was cycled out");
			}
			if (serverCommandNumber > this.serverCommandSequence) {
				throw new JKClientException("GetServerCommand: requested a command not received");
			}
			this.lastExecutedServerCommand = serverCommandNumber;
			sbyte []sc = this.serverCommands[serverCommandNumber & (this.MaxReliableCommands - 1)];
rescan:
			string s = Common.ToString(sc);
			command = new Command(s);
			string cmd = command[0];
			this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command));
			if (string.Compare(cmd, "disconnect", StringComparison.Ordinal) == 0) {
				this.Disconnect();
				return true;
			} else if (string.Compare(cmd, "bcs0", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Clear()
					.Append("cs ")
					.Append(command[1])
					.Append(" \"")
					.Append(command[2]);
				return false;
			} else if (string.Compare(cmd, "bcs1", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Append(command[2]);
				return false;
			} else if (string.Compare(cmd, "bcs2", StringComparison.Ordinal) == 0) {
				this.bigInfoString
					.Append(command[2])
					.Append('"');
				s = this.bigInfoString.ToString();
				sc = new sbyte[Common.BigInfoString];
				byte []bsc = Common.Encoding.GetBytes(s);
				fixed (sbyte *psc = sc) {
					Marshal.Copy(bsc, 0, (IntPtr)psc, bsc.Length);
				}
				goto rescan;
			} else if (string.Compare(cmd, "cs", StringComparison.Ordinal) == 0) {
				this.ConfigstringModified(command, sc);
				return true;
			}
			return true;
		}
		unsafe string IJKClientImport.GetConfigstring(in int index) => this.GetConfigstring(index);
	}
}
