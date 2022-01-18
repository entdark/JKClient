using System;

namespace JKClient {
	//TODO: migrate to C# 8.0 with access modifier for interface elements
/*	internal interface IJKClientImport {
		internal void GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime);
		internal bool GetSnapshot(int snapshotNumber, ref Snapshot snapshot);
		internal bool GetServerCommand(int serverCommandNumber, out Command command);
		internal string GetConfigstring(int index);
	}*/
	internal abstract class ClientGame {
		protected readonly int ClientNum;
		protected int LatestSnapshotNum = 0;
		protected int ProcessedSnapshotNum = 0;
		protected Snapshot? Snap = null, NextSnap = null;
		protected int ServerCommandSequence = 0;
		protected readonly ClientEntity []Entities = new ClientEntity[Common.MaxGEntities];
		protected readonly /*IJKClientImport*/JKClient Client;
		protected int ServerTime;
		public ClientInfo []ClientInfo {
			get;
			protected set;
		}
		public unsafe ClientGame(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			this.Client = client;
			this.ClientNum = clientNum;
			this.ProcessedSnapshotNum = serverMessageNum;
			this.ServerCommandSequence = serverCommandSequence;
			this.LatestSnapshotNum = 0;
			this.Snap = null;
			this.NextSnap = null;
			Common.MemSet(this.Entities, 0, sizeof(ClientEntity)*Common.MaxGEntities);
			this.ClientInfo = new ClientInfo[Common.MaxClients];
			for (int i = 0; i < Common.MaxClients; i++) {
				this.NewClientInfo(i);
			}
		}
		public virtual void Frame(int serverTime) {
			this.ServerTime = serverTime;
			this.ProcessSnapshots();
		}
		protected virtual void ProcessSnapshots() {
			this.Client.GetCurrentSnapshotNumber(out int n, out int _);
			if (n != this.LatestSnapshotNum) {
				if (n < this.LatestSnapshotNum) {
					this.Snap = null;
					this.NextSnap = null;
					this.ProcessedSnapshotNum = -2;
				}
				this.LatestSnapshotNum = n;
			}
			Snapshot snap = new Snapshot();
			while (this.Snap == null) {
				if (!this.ReadNextSnapshot(ref snap)) {
					return;
				}
				if ((snap.Flags & ClientSnapshot.NotActive) == 0) {
					this.SetInitialSnapshot(in snap);
				}
			}
			do {
				if (this.NextSnap == null) {
					if (!this.ReadNextSnapshot(ref snap)) {
						break;
					}
					this.SetNextSnap(in snap);
					if (this.NextSnap.Value.ServerTime < this.Snap.Value.ServerTime) {
						throw new JKClientException("ProcessSnapshots: Server time went backwards");
					}
				}
				if (this.ServerTime >= this.Snap.Value.ServerTime && this.ServerTime < this.NextSnap.Value.ServerTime) {
					break;
				}
				this.TransitionSnapshot();
			} while (true);
		}
		protected virtual bool ReadNextSnapshot(ref Snapshot dest) {
			while (this.ProcessedSnapshotNum < this.LatestSnapshotNum) {
				this.ProcessedSnapshotNum++;
				if (this.Client.GetSnapshot(this.ProcessedSnapshotNum, ref dest)) {
					return true;
				}
			}
			return false;
		}
		protected virtual void SetInitialSnapshot(in Snapshot snap) {
			this.Snap = snap;
			this.Snap.Value.PlayerState.ToEntityState(ref this.Entities[snap.PlayerState.ClientNum].CurrentState);
			this.ExecuteNewServerCommands(snap.ServerCommandSequence);
			int count = this.Snap.Value.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Value.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = es;
				cent.Interpolate = false;
				cent.CurrentValid = true;
				this.ResetEntity(ref cent);
				this.CheckEvents(ref cent);
			}
		}
		protected virtual void SetNextSnap(in Snapshot snap) {
			this.NextSnap = snap;
			this.NextSnap.Value.PlayerState.ToEntityState(ref this.Entities[snap.PlayerState.ClientNum].NextState);
			int count = this.NextSnap.Value.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.NextSnap.Value.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.NextState = es;
				if (!cent.CurrentValid || (((cent.CurrentState.EntityFlags ^ es.EntityFlags) & this.GetEntityFlag(EntityFlag.TeleportBit)) != 0)) {
					cent.Interpolate = false;
				} else {
					cent.Interpolate = true;
				}
			}
		}
		protected virtual void TransitionSnapshot() {
			this.ExecuteNewServerCommands(this.NextSnap.Value.ServerCommandSequence);
			int count = this.Snap.Value.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Value.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentValid = false;
			}
			var oldFrame = this.Snap;
			this.Snap = this.NextSnap;
			this.Snap.Value.PlayerState.ToEntityState(ref this.Entities[this.Snap.Value.PlayerState.ClientNum].CurrentState);
			this.Entities[this.Snap.Value.PlayerState.ClientNum].Interpolate = false;
			count = this.Snap.Value.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Value.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = cent.NextState;
				cent.CurrentValid = true;
				if (!cent.Interpolate) {
					this.ResetEntity(ref cent);
				}
				cent.Interpolate = false;
				this.CheckEvents(ref cent);
				cent.SnapshotTime = this.Snap.Value.ServerTime;
			}
			this.NextSnap = null;
			PlayerState ops = oldFrame.Value.PlayerState,
				ps = this.Snap.Value.PlayerState;
			this.TransitionPlayerState(ref ps, ref ops);
			this.Snap.Value.SetPlayerState(ps);
		}
		protected virtual void ResetEntity(ref ClientEntity cent) {
			if (cent.SnapshotTime < this.ServerTime - ClientEntity.EventValidMsec) {
				cent.PreviousEvent = 0;
			}
		}
		protected virtual unsafe void TransitionPlayerState(ref PlayerState ps, ref PlayerState ops) {
			if (ps.ClientNum != ops.ClientNum) {
				ops = ps;
			}
			if (ps.ExternalEvent != 0 && ps.ExternalEvent != ops.ExternalEvent) {
				ref var cent = ref this.Entities[ps.ClientNum];
				ref var es = ref cent.CurrentState;
				es.Event = ps.ExternalEvent;
				es.EventParm = ps.ExternalEventParm;
				this.HandleEvent(ref cent);
			}
			for (int i = ps.EventSequence - PlayerState.MaxEvents; i < ps.EventSequence; i++) {
				if (i >= ops.EventSequence
					|| (i > ops.EventSequence - PlayerState.MaxEvents && ps.Events[i & (PlayerState.MaxEvents-1)] != ops.Events[i & (PlayerState.MaxEvents-1)])) {
					ref var cent = ref this.Entities[ps.ClientNum];
					ref var es = ref cent.CurrentState;
					es.Event = ps.Events[i & (PlayerState.MaxEvents-1)];
					es.EventParm = ps.EventParms[i & (PlayerState.MaxEvents-1)];
					this.HandleEvent(ref cent);
				}
			}
		}
		protected virtual void ExecuteNewServerCommands(int latestSequence) {
			while (this.ServerCommandSequence < latestSequence) {
				if (this.Client.GetServerCommand(++this.ServerCommandSequence, out var command)) {
					ServerCommand(command);
				}
			}
		}
		protected virtual void ServerCommand(Command command) {
			this.ConfigstringModified(command);
		}
		protected virtual void ConfigstringModified(Command command) {
			int num = command.Argv(1).Atoi();
			int configstringPlayers = this.GetConfigstringIndex(Configstring.Players);
			if (num >= configstringPlayers && num < configstringPlayers+Common.MaxClients) {
				this.NewClientInfo(num - configstringPlayers);
			}
		}
		protected virtual void NewClientInfo(int clientNum) {
			string configstring = this.Client.GetConfigstring(clientNum + this.GetConfigstringIndex(Configstring.Players));
			if (string.IsNullOrEmpty(configstring) || configstring[0] == '\0'
				|| !configstring.Contains("n")) {
				this.ClientInfo[clientNum].Clear();
				return;
			}
			var clientInfoString = new InfoString(configstring);
			this.ClientInfo[clientNum].ClientNum = clientNum;
//			this.ClientInfo[clientNum].Team = (Team)clientInfoString["t"].Atoi();
			this.ClientInfo[clientNum].Name = clientInfoString["n"];
			this.ClientInfo[clientNum].InfoValid = true;
		}
		protected virtual void CheckEvents(ref ClientEntity cent) {
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
		protected virtual void HandleEvent(ref ClientEntity cent) {
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
			if (es.EntityType == this.GetEntityType(EntityType.Player)) {
				if (!this.ClientInfo[clientNum].InfoValid) {
					return;
				}
			}
			switch (ev) {
			case EntityEvent.VoiceCommandSound:
				if (es.GroundEntityNum >= 0 && es.GroundEntityNum < Common.MaxClients) {
					clientNum = es.GroundEntityNum;
					string description = this.Client.GetConfigstring(this.GetConfigstringIndex(Configstring.Sounds) + es.EventParm);
					string message = $"<{this.ClientInfo[clientNum].Name}^7\u0019: {description}>";
					var command = new Command(new string[] { "chat", message });
					this.Client.ExecuteServerCommand(new CommandEventArgs(command));
				}
				break;
			}
		}
		protected abstract int GetConfigstringIndex(Configstring index);
		protected abstract EntityEvent GetEntityEvent(int entityEvent);
		protected abstract int GetEntityType(EntityType entityType);
		protected abstract int GetEntityFlag(EntityFlag entityFlag);
		internal enum Configstring {
			Sounds,
			Players
		}
		internal enum EntityFlag : int {
			TeleportBit,
			PlayerEvent
		}
		internal enum EntityEvent : int {
			None,
			VoiceCommandSound,
			Bits = 0x300
		}
		internal enum EntityType : int {
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			NPC,
			Team,
			Body,
			Terrain,
			FX,
			Grapple,
			Events
		}
	}
	internal class ClientGameJA : ClientGame {
		public ClientGameJA(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		protected override int GetConfigstringIndex(Configstring index) {
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringJA.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJA.Players;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJA), entityEvent)) {
				switch ((EntityEventJA)entityEvent) {
				case EntityEventJA.VoiceCommandSound:
					return EntityEvent.VoiceCommandSound;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJA), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJA.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJA.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
			switch (entityType) {
			default:
				return (int)entityType;
			case EntityType.Events:
				return (int)EntityTypeJA.Events;
			case EntityType.Grapple:
				throw new JKClientException($"Invalid entity type: {entityType}");
			}
		}
		protected override void HandleEvent(ref ClientEntity cent) {
			ref var es = ref cent.CurrentState;
			if (es.EntityType == this.GetEntityType(EntityType.NPC)) {
				return;
			}
			base.HandleEvent(ref cent);
		}
		internal enum ConfigstringJA {
			Sounds = 811,
			Players = 1131
		}
		[Flags]
		internal enum EntityFlagJA : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		internal enum EntityEventJA : int {
			None,
			VoiceCommandSound = 75,
			Bits = 0x300
		}
		internal enum EntityTypeJA : int {
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			NPC,
			Team,
			Body,
			Terrain,
			FX,
			Events
		}
	}
	internal class ClientGameJO : ClientGame {
		public ClientGameJO(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {}
		protected override int GetConfigstringIndex(Configstring index) {
			switch (index) {
			case Configstring.Sounds:
				return (int)ConfigstringJO.Sounds;
			case Configstring.Players:
				return (int)ConfigstringJO.Players;
			}
			return 0;
		}
		protected override EntityEvent GetEntityEvent(int entityEvent) {
			if (Enum.IsDefined(typeof(EntityEventJO), entityEvent)) {
				switch ((EntityEventJO)entityEvent) {
				default:
					break;
				}
			}
			return EntityEvent.None;
		}
		protected override int GetEntityFlag(EntityFlag entityFlag) {
			if (Enum.IsDefined(typeof(EntityFlagJO), (int)entityFlag)) {
				switch (entityFlag) {
				case EntityFlag.TeleportBit:
					return (int)EntityFlagJO.TeleportBit;
				case EntityFlag.PlayerEvent:
					return (int)EntityFlagJO.PlayerEvent;
				}
			}
			return 0;
		}
		protected override int GetEntityType(EntityType entityType) {
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
		internal enum ConfigstringJO {
			Sounds = 288,
			Players = 544
		}
		[Flags]
		internal enum EntityFlagJO : int {
			TeleportBit = (1<<3),
			PlayerEvent = (1<<5)
		}
		internal enum EntityEventJO : int {
			None
		}
		internal enum EntityTypeJO : int {
			General,
			Player,
			Item,
			Missile,
			Special,
			Holocron,
			Mover,
			Beam,
			Portal,
			Speaker,
			PushTrigger,
			TeleportTrigger,
			Invisible,
			Grapple,
			Team,
			Body,
			Events
		}
	}
}
