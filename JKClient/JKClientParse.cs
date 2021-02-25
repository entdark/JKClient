using System;
using System.Runtime.InteropServices;
using System.Text;

namespace JKClient {
	public sealed partial class JKClient {
		private const int PacketBackup = 32;
		private const int PacketMask = (JKClient.PacketBackup-1);
		private const int MaxParseEntities = 2048;
#region ClientActive
		private Snapshot snap = new Snapshot();
		private int serverTime = 0;
		private bool newSnapshots = false;
		private GameState gameState = new GameState();
		private int parseEntitiesNum = 0;
		private UserCommand []cmds = new UserCommand[UserCommand.CommandBackup];
		private int cmdNumber = 0;
		private OutPacket []outPackets = new OutPacket[JKClient.PacketBackup];
		private int serverId = 0;
		private Snapshot []snapshots = new Snapshot[JKClient.PacketBackup];
		private EntityState []entityBaselines = new EntityState[Common.MaxGEntities];
		private EntityState []parseEntities = new EntityState[JKClient.MaxParseEntities];
#endregion
		private GameMod gameMod = GameMod.Undefined;
		private void ParseServerMessage(Message msg) {
			msg.Bitstream();
			this.reliableAcknowledge = msg.ReadLong();
			if (this.reliableAcknowledge < this.reliableSequence - JKClient.MaxReliableCommands) {
				this.reliableAcknowledge = this.reliableSequence;
			}
			bool eof = false;
			ServerCommandOperation cmd;
			while (true) {
				if (msg.ReadCount > msg.CurSize) {
					throw new JKClientException("ParseServerMessage: read past end of server message");
				}
				cmd = (ServerCommandOperation)msg.ReadByte();
				//JO doesn't have setgame command, the rest commands match
				if (this.IsJO() && cmd >= ServerCommandOperation.SetGame) {
					cmd++;
				}
				if (cmd == ServerCommandOperation.EOF) {
					break;
				}
				switch (cmd) {
				default:
					throw new JKClientException("ParseServerMessage: Illegible server message");
				case ServerCommandOperation.Nop:
					break;
				case ServerCommandOperation.ServerCommand:
					this.ParseCommandString(msg);
					break;
				case ServerCommandOperation.Gamestate:
					this.ParseGamestate(msg);
					break;
				case ServerCommandOperation.Snapshot:
					this.ParseSnapshot(msg);
					eof = true;
					break;
				case ServerCommandOperation.SetGame:
//					this.ParseSetGame(msg);
					eof = true;
					break;
				case ServerCommandOperation.Download:
//					this.ParseDownload(msg);
					eof = true;
					break;
				case ServerCommandOperation.MapChange:
					break;
				}
				if (eof) {
					break;
				}
			}
		}
		private unsafe void ParseGamestate(Message msg) {
			this.connectPacketCount = 0;
			this.ClearState();
			this.serverCommandSequence = msg.ReadLong();
			this.gameState.DataCount = 1;
			ServerCommandOperation cmd;
			while (true) {
				cmd = (ServerCommandOperation)msg.ReadByte();
				//JO doesn't have setgame command, the rest commands match
				if (this.IsJO() && cmd >= ServerCommandOperation.SetGame) {
					cmd++;
				}
				if (cmd == ServerCommandOperation.EOF) {
					break;
				} else if (cmd == ServerCommandOperation.Configstring) {
					int i = msg.ReadShort();
					if (i < 0 || i > GameState.MaxConfigstrings) {
						throw new JKClientException("configstring > MaxConfigStrings");
					}
					sbyte []s = msg.ReadBigString();
					int len = Common.StrLen(s);
					if (len + 1 + this.gameState.DataCount > GameState.MaxGameStateChars) {
						throw new JKClientException("MaxGameStateChars exceeded");
					}
					if (i == GameState.ServerInfo) {
						string serverInfoCSStr = Common.ToString(s);
						var infoString = new InfoString(serverInfoCSStr);
						if (this.Protocol == ProtocolVersion.Protocol15 && infoString["version"].Contains("v1.03")) {
							this.Version = ClientVersion.JO_v1_03;
						}
						string gamename = infoString["gamename"];
						if (gamename.Contains("Szlakiem Jedi RPE")
							|| gamename.Contains("Open Jedi Project")
							|| gamename.Contains("OJP Enhanced")
							|| gamename.Contains("OJP Basic")
							|| gamename.Contains("OJRP")) {
							this.gameMod = GameMod.OJP;
						} else if (gamename.Contains("Movie Battles II")) {
							this.gameMod = GameMod.MBII;
						} else {
							this.gameMod = GameMod.Base;
						}
					}
					this.gameState.StringOffsets[i] = this.gameState.DataCount;
					fixed (sbyte *stringData = this.gameState.StringData) {
						Marshal.Copy((byte[])(Array)s, 0, (IntPtr)(stringData+this.gameState.DataCount), len+1);
					}
					this.gameState.DataCount += len + 1;
				} else if (cmd == ServerCommandOperation.Baseline) {
					int newnum = msg.ReadBits(Common.GEntitynumBits);
					if (newnum < 0 || newnum >= Common.MaxGEntities) {
						throw new JKClientException($"Baseline number out of range: {newnum}");
					}
					fixed (EntityState *nes = &EntityState.Null) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(nes, bl, newnum, this.Version, this.gameMod);
						}
					}
				} else {
					throw new JKClientException("ParseGamestate: bad command byte");
				}
			}
			this.ServerInfoChangedCallback?.Invoke(this.ServerInfo);
			this.clientNum = msg.ReadLong();
			this.checksumFeed = msg.ReadLong();
			if (!this.IsJO()) {
				this.ParseRMG(msg);
			}
			this.SystemInfoChanged();
			this.InitCGame();
		}
		private void ParseRMG(Message msg) {
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
		private unsafe void SystemInfoChanged() {
			fixed (sbyte *s = this.gameState.StringData) {
				sbyte *systemInfoCS = s + this.gameState.StringOffsets[GameState.SystemInfo];
				string systemInfoCSStr = Common.ToString(systemInfoCS, Common.StrLen(systemInfoCS));
				var infoString = new InfoString(systemInfoCSStr);
				this.serverId = infoString["sv_serverid"].Atoi();
			}
		}
		private unsafe void ClearState() {
			this.snap = new Snapshot();
			this.serverTime = 0;
			this.newSnapshots = false;
			fixed (GameState *gs = &this.gameState) {
				Common.MemSet(gs, 0, sizeof(GameState));
			}
			this.parseEntitiesNum = 0;
			Common.MemSet(this.cmds, 0, sizeof(UserCommand)*UserCommand.CommandBackup);
			this.cmdNumber = 0;
			Common.MemSet(this.outPackets, 0, sizeof(OutPacket)*JKClient.PacketBackup);
			this.serverId = 0;
			Common.MemSet(this.snapshots, 0, sizeof(Snapshot)*JKClient.PacketBackup);
			Common.MemSet(this.entityBaselines, 0, sizeof(EntityState)*Common.MaxGEntities);
			Common.MemSet(this.parseEntities, 0, sizeof(EntityState)*JKClient.MaxParseEntities);
			this.gameMod = GameMod.Undefined;
		}
		private void ClearConnection() {
			for (int i = 0; i < JKClient.MaxReliableCommands; i++) {
				Common.MemSet(this.serverCommands[i], 0, sizeof(byte)*Common.MaxStringChars);
				Common.MemSet(this.reliableCommands[i], 0, sizeof(byte)*Common.MaxStringChars);
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
		private void ParseCommandString(Message msg) {
			int seq = msg.ReadLong();
			sbyte []s = msg.ReadString();
			if (this.serverCommandSequence >= seq) {
				return;
			}
			this.serverCommandSequence = seq;
			int index = seq & (JKClient.MaxReliableCommands-1);
			Array.Copy(s, 0, this.serverCommands[index], 0, Common.MaxStringChars);
		}
		private unsafe void ParseSnapshot(Message msg) {
			bool oldSnap;
			int oldSnapNum = -1;
			var newSnap = new Snapshot() {
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
				oldSnap = false;
			} else {
				oldSnap = true;
				oldSnapNum = newSnap.DeltaNum & JKClient.PacketMask;
				if (!this.snapshots[oldSnapNum].Valid) {

				} else if (this.snapshots[oldSnapNum].MessageNum != newSnap.DeltaNum) {

				} else if (this.parseEntitiesNum - this.snapshots[oldSnapNum].ParseEntitiesNum > JKClient.MaxParseEntities-128) {

				} else {
					newSnap.Valid = true;
				}
			}
			int len = msg.ReadByte();
			if (len > sizeof(byte)*32) {
				throw new JKClientException("ParseSnapshot: Invalid size %d for areamask");
			}
			msg.ReadData(null, len);
			if (this.CanParseSnapshot()) {
				if (oldSnap) {
					fixed (PlayerState *ps = &this.snapshots[oldSnapNum].PlayerState,
						vps = &this.snapshots[oldSnapNum].VehiclePlayerState) {
						msg.ReadDeltaPlayerstate(ps, &newSnap.PlayerState, this.Version, this.gameMod);
						if (!this.IsJO() && newSnap.PlayerState.VehicleNum != 0) {
							msg.ReadDeltaPlayerstate(vps, &newSnap.VehiclePlayerState, this.Version, this.gameMod, true);
						}
					}
				} else {
					msg.ReadDeltaPlayerstate(null, &newSnap.PlayerState, this.Version, this.gameMod);
					if (!this.IsJO() && newSnap.PlayerState.VehicleNum != 0) {
						msg.ReadDeltaPlayerstate(null, &newSnap.VehiclePlayerState, this.Version, this.gameMod, true);
					}
				}
				if (oldSnap) {
					fixed (Snapshot *old = &this.snapshots[oldSnapNum]) {
						this.ParsePacketEntities(msg, old, &newSnap);
					}
				} else {
					this.ParsePacketEntities(msg, null, &newSnap);
				}
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
		private unsafe void ParsePacketEntities(Message msg, Snapshot *oldSnap, Snapshot* newSnap) {
			newSnap->ParseEntitiesNum = this.parseEntitiesNum;
			newSnap->NumEntities = 0;
			bool hasOldstate;
			int oldindex = 0;
			int oldnum;
			int newnum = msg.ReadBits(Common.GEntitynumBits);
			while (true) {
				if (oldSnap != null && oldindex < oldSnap->NumEntities) {
					hasOldstate = true;
					fixed (EntityState *oldstate = &this.parseEntities[(oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1)]) {
						oldnum = oldstate->Number;
					}
				} else {
					hasOldstate = false;
					oldnum = 99999;
				}
				fixed (EntityState *newstate = &this.parseEntities[this.parseEntitiesNum]) {
					if (!hasOldstate && (newnum == (Common.MaxGEntities-1))) {
						break;
					} else if (oldnum < newnum) {
						fixed (EntityState *oldstate = &this.parseEntities[(oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1)]) {
							*newstate = *oldstate;
						}
						oldindex++;
					} else if (oldnum == newnum) {
						fixed (EntityState *oldstate = &this.parseEntities[(oldSnap->ParseEntitiesNum + oldindex) & (JKClient.MaxParseEntities-1)]) {
							msg.ReadDeltaEntity(oldstate, newstate, newnum, this.Version, this.gameMod);
						}
						oldindex++;
						newnum = msg.ReadBits(Common.GEntitynumBits);
					} else if (oldnum > newnum) {
						fixed (EntityState *bl = &this.entityBaselines[newnum]) {
							msg.ReadDeltaEntity(bl, newstate, newnum, this.Version, this.gameMod);
						}
						newnum = msg.ReadBits(Common.GEntitynumBits);
					}
					if (newstate->Number == Common.MaxGEntities-1)
						continue;
					this.parseEntitiesNum++;
					this.parseEntitiesNum &= (JKClient.MaxParseEntities-1);
					newSnap->NumEntities++;
				}
			}
		}
		private bool CanParseSnapshot() {
			switch (this.gameMod) {
			default:
				return true;
			case GameMod.Undefined:
			case GameMod.MBII:
				return false;
			}
		}
		private void ParseSetGame(Message msg) {
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
		private unsafe void ParseDownload(Message msg) {
			ushort block = (ushort)msg.ReadShort();
			if (block == 0) {
				int downloadSize = msg.ReadLong();
				if (downloadSize < 0) {
					fixed (sbyte* s = msg.ReadString()) {
						byte* ss = (byte*)s;
						throw new JKClientException($"{Common.ToString(ss, sizeof(sbyte)*Common.MaxStringChars)}");
					}
				}
			}
			int size = msg.ReadShort();
			if (size < 0 || size > sizeof(byte)*Message.MaxMsgLen) {
				throw new JKClientException($"ParseDownload: Invalid size {size} for download chunk");
			}
			msg.ReadData(null, size);
		}
	}
}
