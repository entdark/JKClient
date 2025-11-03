using System;
using System.Runtime.InteropServices;
namespace JKClient {
	public sealed partial class JKClient {
		private const int PacketBackup = 32;
		private const int PacketMask = (JKClient.PacketBackup-1);
		private const int MaxParseEntities = 2048;
#region ClientActive
		private ClientSnapshot snap = new ClientSnapshot();
		private int serverTime = 0;
		private int oldServerTime = 0;
		private int oldFrameServerTime = 0;
		private int serverTimeDelta = 0;
		private bool extrapolatedSnapshot = false;
		private bool newSnapshots = false;
		private GameState gameState = new GameState();
		private int parseEntitiesNum = 0;
		private UserCommand []cmds = new UserCommand[UserCommand.Backup];
		private int cmdNumber = 0;
		private OutPacket []outPackets = new OutPacket[JKClient.PacketBackup];
		private int serverId = 0;
		private ClientSnapshot []snapshots = new ClientSnapshot[JKClient.PacketBackup];
		private EntityState []entityBaselines = new EntityState[Common.MaxGEntities];
		private EntityState []parseEntities = new EntityState[JKClient.MaxParseEntities];
#endregion
		private int MaxConfigstrings => this.ClientHandler.MaxConfigstrings;
		private void ParseServerMessage(in Message msg) {
			msg.Bitstream();
			this.reliableAcknowledge = msg.ReadLong();
			if (this.reliableAcknowledge < this.reliableSequence - this.MaxReliableCommands) {
				this.reliableAcknowledge = this.reliableSequence;
			}
			bool eof = false;
			ServerCommandOperations cmd;
			while (true) {
				if (msg.ReadCount > msg.CurSize) {
					throw new JKClientException("ParseServerMessage: read past end of server message");
				}
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
				if (cmd == ServerCommandOperations.EOF) {
					break;
				}
				switch (cmd) {
				default:
					throw new JKClientException("ParseServerMessage: Illegible server message");
				case ServerCommandOperations.Nop:
					break;
				case ServerCommandOperations.ServerCommand:
					this.ParseCommandString(in msg);
					break;
				case ServerCommandOperations.Gamestate:
					this.ParseGamestate(in msg);
					break;
				case ServerCommandOperations.Snapshot:
					this.ParseSnapshot(in msg);
					eof = true;
					break;
				case ServerCommandOperations.SetGame:
//					this.ParseSetGame(in msg);
					eof = true;
					break;
				case ServerCommandOperations.Download:
//					this.ParseDownload(in msg);
					eof = true;
					break;
				case ServerCommandOperations.MapChange:
					break;
				}
				if (eof) {
					break;
				}
			}
		}
		private unsafe void ParseGamestate(in Message msg) {
			this.connectPacketCount = 0;
			this.ClearState();
			this.serverCommandSequence = msg.ReadLong();
			this.gameState.DataCount = 1;
			ServerCommandOperations cmd;
			while (true) {
				cmd = (ServerCommandOperations)msg.ReadByte();
				this.ClientHandler.AdjustServerCommandOperations(ref cmd);
				if (cmd == ServerCommandOperations.EOF) {
					break;
				} else if (cmd == ServerCommandOperations.Configstring) {
					int i = msg.ReadShort();
					if (i < 0 || i > this.MaxConfigstrings) {
						throw new JKClientException("configstring > MaxConfigStrings");
					}
					sbyte []s = msg.ReadBigString();
					int len = Common.StrLen(s);
					if (len + 1 + this.gameState.DataCount > GameState.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					string csStr = Common.ToString(s);
					this.ClientHandler.AdjustGameStateConfigstring(i, csStr);
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						Marshal.Copy((byte[])(Array)s, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
					}
					this.gameState.DataCount += len + 1;
				} else if (cmd == ServerCommandOperations.Baseline) {
					int newnum = msg.ReadBits(Common.GEntitynumBits);
					if (newnum < 0 || newnum >= Common.MaxGEntities) {
						throw new JKClientException($"Baseline number out of range: {newnum}");
					}
					fixed (EntityState *nes = &EntityState.Null) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(nes, bl, newnum, this.ClientHandler);
						}
					}
				} else {
					throw new JKClientException("ParseGamestate: bad command byte");
				}
			}
			this.clientNum = msg.ReadLong();
			this.checksumFeed = msg.ReadLong();
			if (this.ClientHandler.CanParseRMG) {
				this.ParseRMG(msg);
			}
			this.SystemInfoChanged();
			this.clientGame = this.InitClientGame();
			this.ServerInfoChanged?.Invoke(this.ServerInfo);
		}
		private void ParseRMG(in Message msg) {
			ushort rmgHeightMapSize = (ushort)msg.ReadShort();
			if (rmgHeightMapSize == 0) {
				return;
			}
			if (msg.ReadBits(1) != 0) {
				msg.ReadData(null, rmgHeightMapSize);
			} else {
				msg.ReadData(null, rmgHeightMapSize);
			}
			ushort size = (ushort)msg.ReadShort();
			if (msg.ReadBits(1) != 0) {
				msg.ReadData(null, size);
			} else {
				msg.ReadData(null, size);
			}
			int rmgSeed = msg.ReadLong();
			ushort rmgAutomapSymbolCount = (ushort)msg.ReadShort();
			for (int i = 0; i < rmgAutomapSymbolCount; i++) {
				msg.ReadByte();
				msg.ReadByte();
				msg.ReadLong();
				msg.ReadLong();
			}
		}
		private void SystemInfoChanged() {
			string systemInfo = this.GetConfigstring(GameState.SystemInfo);
			var info = new InfoString(systemInfo);
			this.serverId = info["sv_serverid"].Atoi();
			if (info["sv_pure"].Atoi() != 0) {
				throw new JKClientException("Cannot connect to a pure server without assets");
			}
		}
		internal unsafe string GetConfigstring(in int index) {
			if (index < 0 || index >= this.MaxConfigstrings) {
				throw new JKClientException($"Configstring: bad index: {index}");
			}
			fixed (sbyte *s = this.gameState.StringData) {
				sbyte *cs = s + this.gameState.StringOffsets[index];
				return Common.ToString(cs, Common.StrLen(cs));
			}
		}
		private void ClearState() {
			this.snap = new ClientSnapshot();
			this.serverTime = 0;
			this.oldServerTime = 0;
			this.oldFrameServerTime = 0;
			this.serverTimeDelta = 0;
			this.extrapolatedSnapshot = false;
			this.newSnapshots = false;
			Common.MemSet(ref this.gameState, 0);
			this.parseEntitiesNum = 0;
			Common.MemSet(this.cmds, 0);
			this.cmdNumber = 0;
			Common.MemSet(this.outPackets, 0);
			this.serverId = 0;
			Common.MemSet(this.snapshots, 0);
			Common.MemSet(this.entityBaselines, 0);
			Common.MemSet(this.parseEntities, 0);
			this.clientGame = null;
			this.ClientHandler.ClearState();
		}
		private void ClearConnection() {
			for (int i = 0; i < this.ClientHandler.MaxReliableCommands; i++) {
				Common.MemSet(this.serverCommands[i], 0);
				Common.MemSet(this.reliableCommands[i], 0);
			}
			this.clientNum = 0;
			this.lastPacketSentTime = 0;
			this.lastPacketTime = 0;
			this.serverAddress = null;
			this.connectTime = 0;
			this.connectPacketCount = 0;
			this.challenge = 0;
			this.checksumFeed = 0;
			this.reliableSequence = 0;
			this.reliableAcknowledge = 0;
			this.serverMessageSequence = 0;
			this.serverCommandSequence = 0;
			this.lastExecutedServerCommand = 0;
			this.netChannel = null;
		}
		private void ParseCommandString(in Message msg) {
			int seq = msg.ReadLong();
			sbyte []s = msg.ReadString();
			if (this.serverCommandSequence >= seq) {
				return;
			}
			this.serverCommandSequence = seq;
			int index = seq & (this.MaxReliableCommands-1);
			Array.Copy(s, 0, this.serverCommands[index], 0, Common.MaxStringChars);
		}
		private unsafe void ParseSnapshot(in Message msg) {
			ClientSnapshot *oldSnap;
			using var snapshotsPinned = new PinnedObject<ClientSnapshot>(this.snapshots);
			var newSnap = new ClientSnapshot() {
				ServerCommandNum = this.serverCommandSequence,
				ServerTime = msg.ReadLong(),
				MessageNum = this.serverMessageSequence
			};
			int deltaNum = msg.ReadByte();
			if (deltaNum == 0) {
				newSnap.DeltaNum = -1;
			} else {
				newSnap.DeltaNum = newSnap.MessageNum - deltaNum;
			}
			newSnap.Flags = msg.ReadByte();
			if (newSnap.DeltaNum <= 0) {
				newSnap.Valid = true;
				oldSnap = null;
			} else {
				oldSnap = snapshotsPinned[newSnap.DeltaNum & JKClient.PacketMask];
				if (!oldSnap->Valid) {

				} else if (oldSnap->MessageNum != newSnap.DeltaNum) {

				} else if (this.parseEntitiesNum - oldSnap->ParseEntitiesNum > JKClient.MaxParseEntities-128) {

				} else {
					newSnap.Valid = true;
				}
			}
			int len = msg.ReadByte();
			msg.ReadData(null, len);
			if (this.ClientHandler.CanParseSnapshot()) {
				msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->PlayerState : null, &newSnap.PlayerState, false, this.ClientHandler);
				if (this.ClientHandler.CanParseVehicle && newSnap.PlayerState.VehicleNum != 0) {
					msg.ReadDeltaPlayerstate(oldSnap != null ? &oldSnap->VehiclePlayerState : null, &newSnap.VehiclePlayerState, true, this.ClientHandler);
				}
				this.ParsePacketEntities(in msg, in oldSnap, &newSnap);
			}
			if (!newSnap.Valid) {
				return;
			}
			int oldMessageNum = this.snap.MessageNum + 1;
			if (newSnap.MessageNum - oldMessageNum >= JKClient.PacketBackup) {
				oldMessageNum = newSnap.MessageNum - JKClient.PacketMask;
			}
			for (;oldMessageNum < newSnap.MessageNum; oldMessageNum++) {
				this.snapshots[oldMessageNum & JKClient.PacketMask].Valid = false;
			}
			this.snap = newSnap;
			this.snapshots[this.snap.MessageNum & JKClient.PacketMask] = this.snap;
			this.newSnapshots = true;
		}
		private unsafe void ParsePacketEntities(in Message msg, in ClientSnapshot *oldSnap, in ClientSnapshot *newSnap) {
			newSnap->ParseEntitiesNum = this.parseEntitiesNum;
			newSnap->NumEntities = 0;
			EntityState *oldstate;
			using var parseEntitiesPinned = new PinnedObject<EntityState>(this.parseEntities);
			int oldindex = 0;
			int oldnum;
			int newnum = msg.ReadBits(Common.GEntitynumBits);
			while (true) {
				if (oldSnap != null && oldindex < oldSnap->NumEntities) {
					oldstate = parseEntitiesPinned[(oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1)];
					oldnum = oldstate->Number;
				} else {
					oldstate = null;
					oldnum = 99999;
				}
				EntityState *newstate = parseEntitiesPinned[this.parseEntitiesNum & (JKClient.MaxParseEntities-1)];
				if (oldstate == null && (newnum == (Common.MaxGEntities-1))) {
					break;
				} else if (oldnum < newnum) {
					*newstate = *oldstate;
					oldindex++;
				} else if (oldnum == newnum) {
					oldindex++;
					msg.ReadDeltaEntity(oldstate, newstate, newnum, this.ClientHandler);
					newnum = msg.ReadBits(Common.GEntitynumBits);
				} else if (oldnum > newnum) {
					fixed (EntityState *bl = &this.entityBaselines[newnum]) {
						msg.ReadDeltaEntity(bl, newstate, newnum, this.ClientHandler);
					}
					newnum = msg.ReadBits(Common.GEntitynumBits);
				}
				if (newstate->Number == Common.MaxGEntities-1)
					continue;
				this.parseEntitiesNum++;
//				this.parseEntitiesNum &= (JKClient.MaxParseEntities-1);
				newSnap->NumEntities++;
			}
		}
		private void ParseSetGame(in Message msg) {
			int i = 0;
			while (i < 64) {
				int next = msg.ReadByte();
				if (next != 0) {

				} else {
					break;
				}
				i++;
			}
		}
		private unsafe void ParseDownload(in Message msg) {
			ushort block = (ushort)msg.ReadShort();
			if (block == 0) {
				int downloadSize = msg.ReadLong();
				if (downloadSize < 0) {
					fixed (sbyte *s = msg.ReadString()) {
						byte* ss = (byte*)s;
						throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringChars)}");
					}
				}
			}
			int size = msg.ReadShort();
			if (size < 0 || size > sizeof(byte)*this.ClientHandler.MaxMessageLength) {
				throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			msg.ReadData(null, size);
		}
	}
}
