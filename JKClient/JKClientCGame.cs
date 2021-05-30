using System;
using System.Runtime.InteropServices;

namespace JKClient {
	public sealed partial class JKClient {
		private int cgLatestSnapshotNum = 0;
		private int cgProcessedSnapshotNum = 0;
		private Snapshot? cgSnap = null, cgNextSnap = null;
		private int cgServerCommandSequence = 0;
		private ClientEntity []cgEntities = new ClientEntity[Common.MaxGEntities];
		public event Action<CommandEventArgs> ServerCommandExecuted;
		private unsafe void InitCGame() {
			this.Status = ConnectionStatus.Primed;
			this.cgServerCommandSequence = this.serverCommandSequence;
			this.cgProcessedSnapshotNum = this.serverMessageSequence;
			this.cgLatestSnapshotNum = 0;
			this.cgSnap = null;
			this.cgNextSnap = null;
			Common.MemSet(this.cgEntities, 0, sizeof(ClientEntity)*Common.MaxGEntities);
			for (int i = 0; i < Common.MaxClients; i++) {
				this.NewClientInfo(i);
			}
		}
		private void SetTime() {
			if (this.Status != ConnectionStatus.Active) {
				if (this.Status != ConnectionStatus.Primed) {
					return;
				}
				if (this.newSnapshots) {
					this.newSnapshots = false;
					if ((this.snap.Flags & Snapshot.NotActive) != 0) {
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
		private void ProcessSnapshots() {
			int n = this.snap.MessageNum;
			if (n != this.cgLatestSnapshotNum) {
				if (n < this.cgLatestSnapshotNum) {
					this.cgSnap = null;
					this.cgNextSnap = null;
					this.cgProcessedSnapshotNum = -2;
				}
				this.cgLatestSnapshotNum = n;
			}
			Snapshot snap = new Snapshot();
			while (this.cgSnap == null) {
				if (!this.ReadNextSnapshot(ref snap)) {
					return;
				}
				if ((snap.Flags & Snapshot.NotActive) == 0) {
					this.SetInitialSnapshot(in snap);
				}
			}
			do {
				if (this.cgNextSnap == null) {
					if (!this.ReadNextSnapshot(ref snap)) {
						break;
					}
					this.SetNextSnap(in snap);
					if (this.cgNextSnap.Value.ServerTime < this.cgSnap.Value.ServerTime) {
						throw new JKClientException("ProcessSnapshots: Server time went backwards");
					}
				}
				if (this.serverTime >= this.cgSnap.Value.ServerTime && this.serverTime < this.cgNextSnap.Value.ServerTime) {
					break;
				}
				this.TransitionSnapshot();
			} while (true);
		}
		private bool ReadNextSnapshot(ref Snapshot dest) {
			while (this.cgProcessedSnapshotNum < this.cgLatestSnapshotNum) {
				this.cgProcessedSnapshotNum++;
				if (this.GetSnapshot(this.cgProcessedSnapshotNum, ref dest)) {
					return true;
				}
			}
			return false;
		}
		private unsafe bool GetSnapshot(int snapshotNumber, ref Snapshot snapshot) {
			if (snapshotNumber > this.snap.MessageNum) {
				throw new JKClientException("GetSnapshot: snapshotNumber > this.snapshot.messageNum");
			}
			if (this.snap.MessageNum - snapshotNumber >= JKClient.PacketBackup) {
				return false;
			}
			ref var snap = ref this.snapshots[snapshotNumber & JKClient.PacketMask];
			if (!snap.Valid) {
				return false;
			}
			if (this.parseEntitiesNum - snap.ParseEntitiesNum > JKClient.MaxParseEntities) {
				return false;
			}
			snapshot = this.snapshots[snapshotNumber & JKClient.PacketMask];
			return true;
		}
		private void SetInitialSnapshot(in Snapshot snap) {
			this.cgSnap = snap;
			this.cgSnap.Value.PlayerState.ToEntityState(ref this.cgEntities[snap.PlayerState.ClientNum].CurrentState);
			this.ExecuteNewServerCommands(snap.ServerCommandNum);
			int count = this.cgSnap.Value.CGNumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.EntityStateAt(i, in this.cgSnap);
				ref var cent = ref this.cgEntities[es.Number];
				cent.CurrentState = es;
				cent.Interpolate = false;
				cent.CurrentValid = true;
				this.ResetEntity(ref cent);
				this.CheckEvents(ref cent);
			}
		}
		private void SetNextSnap(in Snapshot snap) {
			this.cgNextSnap = snap;
			this.cgNextSnap.Value.PlayerState.ToEntityState(ref this.cgEntities[snap.PlayerState.ClientNum].NextState);
			int count = this.cgNextSnap.Value.CGNumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.EntityStateAt(i, in this.cgNextSnap);
				ref var cent = ref this.cgEntities[es.Number];
				cent.NextState = es;
				if (!cent.CurrentValid || (((cent.CurrentState.EntityFlags ^ es.EntityFlags) & this.GetEntityFlag(EntityFlag.TeleportBit)) != 0)) {
					cent.Interpolate = false;
				} else {
					cent.Interpolate = true;
				}
			}
		}
		private unsafe void TransitionSnapshot() {
			this.ExecuteNewServerCommands(this.cgNextSnap.Value.ServerCommandNum);
			int count = this.cgSnap.Value.CGNumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.EntityStateAt(i, in this.cgSnap);
				ref var cent = ref this.cgEntities[es.Number];
				cent.CurrentValid = false;
			}
			var oldFrame = this.cgSnap;
			this.cgSnap = this.cgNextSnap;
			this.cgSnap.Value.PlayerState.ToEntityState(ref this.cgEntities[this.cgSnap.Value.PlayerState.ClientNum].CurrentState);
			this.cgEntities[this.cgSnap.Value.PlayerState.ClientNum].Interpolate = false;
			count = this.cgSnap.Value.CGNumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.EntityStateAt(i, in this.cgSnap);
				ref var cent = ref this.cgEntities[es.Number];
				cent.CurrentState = cent.NextState;
				cent.CurrentValid = true;
				if (!cent.Interpolate) {
					this.ResetEntity(ref cent);
				}
				cent.Interpolate = false;
				this.CheckEvents(ref cent);
				cent.SnapshotTime = this.cgSnap.Value.ServerTime;
			}
			this.cgNextSnap = null;
			PlayerState ops = oldFrame.Value.PlayerState,
				ps = this.cgSnap.Value.PlayerState;
			this.TransitionPlayerState(ref ps, ref ops);
			this.cgSnap.Value.SetPlayerState(ps);
		}
		private void ResetEntity(ref ClientEntity cent) {
			if (cent.SnapshotTime < this.serverTime - ClientEntity.EventValidMsec) {
				cent.PreviousEvent = 0;
			}
		}
		private ref EntityState EntityStateAt(int number, in Snapshot? snap) {
			int entNum = (snap.Value.ParseEntitiesNum + number) & (JKClient.MaxParseEntities-1);
			return ref this.parseEntities[entNum];
		}
		private unsafe void TransitionPlayerState(ref PlayerState ps, ref PlayerState ops) {
			if (ps.ClientNum != ops.ClientNum) {
				ops = ps;
			}
			if (ps.ExternalEvent != 0 && ps.ExternalEvent != ops.ExternalEvent) {
				ref var cent = ref this.cgEntities[ps.ClientNum];
				ref var es = ref cent.CurrentState;
				es.Event = ps.ExternalEvent;
				es.EventParm = ps.ExternalEventParm;
				this.HandleEvent(ref cent);
			}
			for (int i = ps.EventSequence - PlayerState.MaxEvents; i < ps.EventSequence; i++) {
				if (i >= ops.EventSequence
					|| (i > ops.EventSequence - PlayerState.MaxEvents && ps.Events[i & (PlayerState.MaxEvents-1)] != ops.Events[i & (PlayerState.MaxEvents-1)])) {
					ref var cent = ref this.cgEntities[ps.ClientNum];
					ref var es = ref cent.CurrentState;
					es.Event = ps.Events[i & (PlayerState.MaxEvents-1)];
					es.EventParm = ps.EventParms[i & (PlayerState.MaxEvents-1)];
					this.HandleEvent(ref cent);
				}
			}
		}
		private void ExecuteNewServerCommands(int latestSequence) {
			while (this.cgServerCommandSequence < latestSequence) {
				if (this.GetServerCommand(++this.cgServerCommandSequence)) {

				}
			}
		}
		private unsafe bool GetServerCommand(int serverCommandNumber) {
			if (serverCommandNumber <= this.serverCommandSequence - JKClient.MaxReliableCommands) {
				throw new JKClientException("GetServerCommand: a reliable command was cycled out");
			}
			if (serverCommandNumber > this.serverCommandSequence) {
				throw new JKClientException("GetServerCommand: requested a command not received");
			}
			this.lastExecutedServerCommand = serverCommandNumber;
			sbyte []sc = this.serverCommands[serverCommandNumber & (JKClient.MaxReliableCommands - 1)];
			string s = Common.ToString(sc);
			var command = new Command(s);
			string cmd = command.Argv(0);
			this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command));
			if (string.Compare(cmd, "disconnect", true) == 0) {
				this.Disconnect();
				return true;
			} else if (string.Compare(cmd, "cs", true) == 0) {
				this.ConfigstringModified(command, sc);
				this.CGConfigstringModified(command);
				this.ServerInfoChanged?.Invoke(this.ServerInfo);
				return true;
			}
			return true;
		}
		private unsafe void ConfigstringModified(Command command, sbyte []s) {
			int index = command.Argv(1).Atoi();
			if (index < 0 || index >= GameState.MaxConfigstrings) {
				throw new JKClientException($"ConfigstringModified: bad index {index}");
			}
			int start = 4 + index.ToString().Length;
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
			}
		}
		private void CGConfigstringModified(Command command) {
			int num = command.Argv(1).Atoi();
//			string str = this.GetConfigstring(num);
			int configstringPlayers = this.GetConfigstringIndex(Configstring.Players);
			if (num >= configstringPlayers && num < configstringPlayers+Common.MaxClients) {
				this.NewClientInfo(num - configstringPlayers);
			}
		}
		private unsafe string GetConfigstring(int index) {
			if (index < 0 || index >= GameState.MaxConfigstrings) {
				throw new JKClientException($"Configstring: bad index: {index}");
			}
			fixed (sbyte *s = this.gameState.StringData) {
				sbyte *cs = s + this.gameState.StringOffsets[index];
				return Common.ToString(cs, Common.StrLen(cs));
			}
		}
		private int GetConfigstringIndex(Configstring index) {
			bool isJO = this.IsJO();
			switch (index) {
			case Configstring.Sounds:
				return !isJO ? (int)ConfigstringJA.Sounds : (int)ConfigstringJO.Sounds;
			case Configstring.Players:
				return !isJO ? (int)ConfigstringJA.Players : (int)ConfigstringJO.Players;
			}
			return 0;
		}
		private void NewClientInfo(int clientNum) {
			string configstring = this.GetConfigstring(clientNum + this.GetConfigstringIndex(Configstring.Players));
			if (string.IsNullOrEmpty(configstring) || configstring[0] == '\0'
				|| !configstring.Contains("n")) {
				this.clientInfo[clientNum].Clear();
				return;
			}
			var clientInfoString = new InfoString(configstring);
			this.clientInfo[clientNum].ClientNum = clientNum;
//			this.clientInfo[clientNum].Team = (Team)clientInfoString["t"].Atoi();
			this.clientInfo[clientNum].Name = clientInfoString["n"];
			this.clientInfo[clientNum].InfoValid = true;
		}
		private void CheckEvents(ref ClientEntity cent) {
			ref var es = ref cent.CurrentState;
			if (es.EntityType > this.GetEntityType(EntityType.Events)) {
				if (cent.PreviousEvent != 0) {
					return;
				}
				if ((es.EntityFlags & this.GetEntityFlag(EntityFlag.PlayerEvent)) != 0) {
					es.Number = es.OtherEntityNum;
				}
				cent.PreviousEvent = 1;
				es.Event = (es.EntityType - this.GetEntityType(EntityType.Events));
			} else {
				if (es.Event == cent.PreviousEvent) {
					return;
				}
				cent.PreviousEvent = es.Event;
				if ((es.Event & ~(int)EntityEvent.Bits) == 0) {
					return;
				}
			}
			this.HandleEvent(ref cent);
		}
		private void HandleEvent(ref ClientEntity cent) {
			ref var es = ref cent.CurrentState;
			int entityEvent = es.Event & ~(int)EntityEvent.Bits;
			var ev = this.GetEntityEvent(entityEvent);
			if (ev == EntityEvent.None) {
				return;
			}
			int clientNum = es.ClientNum;
			if (clientNum < 0 || clientNum >= Common.MaxClients) {
				clientNum = 0;
			}
			if (!this.IsJO() && es.EntityType == this.GetEntityType(EntityType.NPC)) {
				return;
			}
			if (es.EntityType == this.GetEntityType(EntityType.Player)) {
				if (!this.ClientInfo[clientNum].InfoValid) {
					return;
				}
			}
			switch (ev) {
			case EntityEvent.VoiceCommandSound:
				if (es.GroundEntityNum >= 0 && es.GroundEntityNum < Common.MaxClients) {
					clientNum = es.GroundEntityNum;
					string description = this.GetConfigstring(this.GetConfigstringIndex(Configstring.Sounds) + es.EventParm);
					string message = $"<{this.ClientInfo[clientNum].Name}^7\u0019: {description}>";
					var command = new Command(new string []{ "chat", message });
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command));
				}
				break;
			}
		}
		private EntityEvent GetEntityEvent(int entityEvent) {
			if (this.IsJO()) {
				if (!Enum.IsDefined(typeof(EntityEventJO), entityEvent)) {
					return EntityEvent.None;
				}
				switch (entityEvent) {
				default:
					return (EntityEvent)entityEvent;
				}
			} else if (!Enum.IsDefined(typeof(EntityEvent), entityEvent)) {
				return EntityEvent.None;
			} else {
				return (EntityEvent)entityEvent;
			}
		}
		private int GetEntityType(EntityType entityType) {
			if (!this.IsJO()) {
				switch (entityType) {
				default:
					return (int)entityType;
				case EntityType.Events:
					return (int)EntityTypeJA.Events;
				case EntityType.Grapple:
					throw new JKClientException($"Invalid entity type: {entityType}");
				}
			} else {
				switch (entityType) {
				default:
					return (int)entityType;
				case EntityType.Grapple:
					return (int)EntityTypeJO.Grapple;
				case EntityType.Events:
					return (int)EntityTypeJO.Events;
				case EntityType.NPC:
				case EntityType.Terrain:
				case EntityType.FX:
					throw new JKClientException($"Invalid entity type: {entityType}");
				}
			}
		}
		private int GetEntityFlag(EntityFlag entityFlag) {
			if (!this.IsJO()) {
				if (!Enum.IsDefined(typeof(EntityFlagJA), (int)entityFlag)) {
					return 0;
				}
				switch (entityFlag) {
				default:
					return (int)entityFlag;
				//currently all flags match
				}
			} else {
				if (!Enum.IsDefined(typeof(EntityFlagJO), (int)entityFlag)) {
					return 0;
				}
				switch (entityFlag) {
				default:
					return (int)entityFlag;
				//currently all flags match
				}
			}
		}
	}
}
