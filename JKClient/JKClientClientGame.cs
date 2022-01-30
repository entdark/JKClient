using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public sealed partial class JKClient {
		public event Action<CommandEventArgs> ServerCommandExecuted;
		internal void ExecuteServerCommand(CommandEventArgs eventArgs) {
			this.ServerCommandExecuted?.Invoke(eventArgs);
		}
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
					this.connectTCS.TrySetResult(true);
				}
				if (this.Status != ConnectionStatus.Active) {
					return;
				}
			}
			this.serverTime = this.snap.ServerTime;
		}
		private ClientGame InitClientGame() {
			this.Status = ConnectionStatus.Primed;
			switch (this.Version) {
			default:
			case ClientVersion.JA_v1_00:
			case ClientVersion.JA_v1_01:
				switch (this.gameMod) {
				default:
					return new ClientGameJA(this, this.serverMessageSequence, this.serverCommandSequence, this.clientNum);
				}
			case ClientVersion.JO_v1_02:
			case ClientVersion.JO_v1_03:
			case ClientVersion.JO_v1_04:
				return new ClientGameJO(this, this.serverMessageSequence, this.serverCommandSequence, this.clientNum);
			}
		}
		private unsafe void ConfigstringModified(Command command, sbyte []s) {
			int index = command.Argv(1).Atoi();
			if (index < 0 || index >= GameState.MaxConfigstrings) {
				throw new JKClientException($"ConfigstringModified: bad index {index}");
			}
			int start = 4 + command.Argv(1).Length;
			if (s[start] == 34) { //'\"'
				start++;
			}
			int blen = command.Argv(2).Length;
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
				fixed (GameState *gs = &this.gameState) {
					Common.MemSet(gs, 0, sizeof(GameState));
				}
				this.gameState.DataCount = 1;
				int len;
				for (int i = 0; i < GameState.MaxConfigstrings; i++) {
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
		internal void GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime) {
			snapshotNumber = this.snap.MessageNum;
			serverTime = this.snap.ServerTime;
		}
		internal unsafe bool GetSnapshot(int snapshotNumber, ref Snapshot snapshot) {
			if (snapshotNumber > this.snap.MessageNum) {
				throw new JKClientException("GetSnapshot: snapshotNumber > this.snapshot.messageNum");
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
			snapshot.Entities = new EntityState[Snapshot.MaxEntities];
			for (int i = 0; i < snapshot.NumEntities; i++) {
				int entNum = (clSnapshot.ParseEntitiesNum + i) & (JKClient.MaxParseEntities-1);
				snapshot.Entities[i] = this.parseEntities[entNum];
			}
			return true;
		}
		internal bool GetServerCommand(int serverCommandNumber, out Command command) {
			if (serverCommandNumber <= this.serverCommandSequence - JKClient.MaxReliableCommands) {
				throw new JKClientException("GetServerCommand: a reliable command was cycled out");
			}
			if (serverCommandNumber > this.serverCommandSequence) {
				throw new JKClientException("GetServerCommand: requested a command not received");
			}
			this.lastExecutedServerCommand = serverCommandNumber;
			sbyte []sc = this.serverCommands[serverCommandNumber & (JKClient.MaxReliableCommands - 1)];
			string s = Common.ToString(sc);
			command = new Command(s);
			s = Common.ToString(sc, Encoding.UTF8);
			var utf8Command = new Command(s);
			string cmd = command.Argv(0);
			this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command, utf8Command));
			if (string.Compare(cmd, "disconnect", StringComparison.OrdinalIgnoreCase) == 0) {
				this.Disconnect();
				return true;
			} else if (string.Compare(cmd, "cs", StringComparison.OrdinalIgnoreCase) == 0) {
				this.ConfigstringModified(command, sc);
				return true;
			}
			return true;
		}
	}
}
