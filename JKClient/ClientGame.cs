using System;
using System.Numerics;

namespace JKClient {
	public interface IJKClientImport {
		internal int MaxClients { get; }
		internal ServerInfo ServerInfo { get; }
		internal void GetCurrentSnapshotNumber(out int snapshotNumber, out int serverTime);
		internal bool GetSnapshot(in int snapshotNumber, ref Snapshot snapshot);
		internal bool GetServerCommand(in int serverCommandNumber, out Command command);
		internal string GetConfigstring(in int index);
		internal void ExecuteServerCommand(CommandEventArgs eventArgs);
		internal void ExecuteEntityEvent(EntityEventArgs eventArgs);
		internal void NotifyClientInfoChanged();
		internal bool GetDefaultState(int index, ref EntityState state, in int entityFlagPermanent);
		void SetUserInfoKeyValue(string key, string value);
	}
	public abstract class ClientGame {
		public const int ScoreNotPresent = -9999;
		internal const int DefaultGravity = 800;
		protected const int MaxClientScoreSend = 32;
		protected readonly bool Initialized = false;
		protected readonly int ClientNum;
		private protected bool NeedNotifyClientInfoChanged = false;
		private protected int LatestSnapshotNum = 0;
		private protected int ProcessedSnapshotNum = 0;
		private protected Snapshot Snap = null, NextSnap = null;
		private protected int ServerCommandSequence = 0;
		private protected float FrameInterpolation;
		private protected readonly IJKClientImport Client;
#region ClientGameStatic
		private protected int LevelStartTime = 0;
		public int Time { get; private protected set; }
#endregion
		private protected readonly Snapshot []ActiveSnapshots = new Snapshot[2] {
			new Snapshot(),
			new Snapshot()
		};
		public ClientEntity []Entities {
			get;
			private protected init;
		}
		public ClientInfo []ClientsInfo {
			get;
			private protected init;
		}
		public int Scores1 { get; private protected set; }
		public int Scores2 { get; private protected set; }
		public int Timer => Time - LevelStartTime;
		internal ClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			this.Client = client;
			this.ClientNum = clientNum;
			this.Entities = new ClientEntity[Common.MaxGEntities];
			Common.MemSet(this.Entities, 0);
			this.LatestSnapshotNum = 0;
			this.Snap = null;
			this.NextSnap = null;
			this.ProcessedSnapshotNum = serverMessageNum;
			this.ServerCommandSequence = serverCommandSequence;
			this.LevelStartTime = this.GetConfigstring(Configstring.LevelStartTime).Atoi();
			this.Scores1 = this.GetConfigstring(Configstring.Scores1).Atoi();
			this.Scores2 = this.GetConfigstring(Configstring.Scores2).Atoi();
			this.ClientsInfo = new ClientInfo[this.Client.MaxClients];
			for (int i = 0; i < this.Client.MaxClients; i++) {
				this.NewClientInfo(i);
			}
			this.Initialized = true;
		}
		internal virtual bool Frame(int serverTime) {
			this.Time = serverTime;
			this.ProcessSnapshots();
			if (this.Snap == null) {
				return false;
			}
			this.PreparePacketEntities();
			this.PlayerStateToEntityState(ref this.Snap.PlayerState, ref this.Entities[this.Snap.PlayerState.ClientNum].CurrentState);
			this.Entities[this.Snap.PlayerState.ClientNum].CurrentValid = true;
			this.Entities[this.Snap.PlayerState.ClientNum].Added = false;
			this.AddPacketEntities();
			if (this.NeedNotifyClientInfoChanged) {
				this.Client.NotifyClientInfoChanged();
				this.NeedNotifyClientInfoChanged = false;
			}
			return true;
		}
		private protected virtual void PreparePacketEntities() {
			if (this.NextSnap != null) {
				int delta = this.NextSnap.ServerTime - this.Snap.ServerTime;
				if (delta == 0) {
					this.FrameInterpolation = 0.0f;
				} else {
					this.FrameInterpolation = (float)(this.Time - this.Snap.ServerTime) / delta;
				}
			} else {
				this.FrameInterpolation = 0.0f;
			}
		}
		private protected virtual void AddPacketEntities() {
			this.AddClientEntity(ref this.Entities[this.Snap.PlayerState.ClientNum]);
			for (int num = 0; num < this.Snap.NumEntities; num++) {
				int number = this.Snap.Entities[num].Number;
				if (number != this.Snap.PlayerState.ClientNum) {
					ref var cent = ref this.Entities[number];
					this.AddClientEntity(ref cent);
				}
			}
		}
		private protected virtual void AddClientEntity(ref ClientEntity cent) {
			int entityType = cent.CurrentState.EntityType;
			if (entityType >= this.GetEntityType(EntityType.Events)) {
				return;
			}
			if (this.Snap.PlayerState.PlayerMoveType == PlayerMoveType.Intermission) {
				entityType = cent.CurrentState.EntityType;
				if (entityType == this.GetEntityType(EntityType.General)
					|| entityType == this.GetEntityType(EntityType.Player)
					|| entityType == this.GetEntityType(EntityType.Invisible)) {
					return;
				}
			}
			if (this.Snap.PlayerState.ClientNum == cent.CurrentState.Number && this.ClientsInfo[this.Snap.PlayerState.ClientNum].Team == Team.Spectator) {
//				return;
			}
			this.CalcEntityLerpPositions(ref cent);
			cent.Added = true;
		}
		private protected virtual bool CalcEntityLerpPositions(ref ClientEntity cent) {
			ref EntityState currentState = ref cent.CurrentState,
				nextState = ref cent.NextState;
			if (currentState.Number < this.Client.MaxClients) {
				currentState.PositionTrajectory.Type = TrajectoryType.Interpolate;
				nextState.PositionTrajectory.Type = TrajectoryType.Interpolate;
			}
			if (cent.Interpolate && currentState.PositionTrajectory.Type == TrajectoryType.Interpolate) {
				this.InterpolateEntityPosition(ref cent);
				return true;
			}
			cent.LerpOrigin = currentState.PositionTrajectory.Evaluate(this.Snap.ServerTime);
			cent.LerpAngles = currentState.AnglesTrajectory.Evaluate(this.Snap.ServerTime);
			return false;
		}
		private protected virtual void InterpolateEntityPosition(ref ClientEntity cent) {
			ref EntityState currentState = ref cent.CurrentState,
				nextState = ref cent.NextState;
			Vector3 current, next;
			current = currentState.PositionTrajectory.Evaluate(this.Snap.ServerTime);
			next = nextState.PositionTrajectory.Evaluate(this.NextSnap?.ServerTime ?? 0);
			cent.LerpOrigin = Vector3.Lerp(current, next, this.FrameInterpolation);
			current = currentState.AnglesTrajectory.Evaluate(this.Snap.ServerTime);
			next = nextState.AnglesTrajectory.Evaluate(this.NextSnap?.ServerTime ?? 0);
			cent.LerpAngles = Common.LerpAngles(current, next, this.FrameInterpolation);
		}
		private protected virtual void ProcessSnapshots() {
			this.Client.GetCurrentSnapshotNumber(out int n, out int _);
			if (n != this.LatestSnapshotNum) {
				if (n < this.LatestSnapshotNum) {
					this.Snap = null;
					this.NextSnap = null;
					this.ProcessedSnapshotNum = -2;
				}
				this.LatestSnapshotNum = n;
			}
			Snapshot snap;
			while (this.Snap == null) {
				snap = this.ReadNextSnapshot();
				if (snap == null) {
					return;
				}
				if ((snap.Flags & ClientSnapshot.NotActive) == 0) {
					this.SetInitialSnapshot(snap);
				}
			}
			do {
				if (this.NextSnap == null) {
					snap = this.ReadNextSnapshot();
					if (snap == null) {
						break;
					}
					this.SetNextSnap(snap);
					if (this.NextSnap.ServerTime < this.Snap.ServerTime) {
						throw new JKClientException("ProcessSnapshots: Server time went backwards");
					}
				}
				if (this.Time >= this.Snap.ServerTime && this.Time < this.NextSnap.ServerTime) {
					break;
				}
				this.TransitionSnapshot();
			} while (true);
		}
		private protected virtual Snapshot ReadNextSnapshot() {
			Snapshot dest;
			while (this.ProcessedSnapshotNum < this.LatestSnapshotNum) {
				if (this.Snap == this.ActiveSnapshots[0]) {
					dest = this.ActiveSnapshots[1];
				} else {
					dest = this.ActiveSnapshots[0];
				}
				this.ProcessedSnapshotNum++;
				if (this.Client.GetSnapshot(this.ProcessedSnapshotNum, ref dest)) {
					return dest;
				}
			}
			return null;
		}
		private protected virtual void SetInitialSnapshot(in Snapshot snap) {
			this.Snap = snap;
			this.PlayerStateToEntityState(ref this.Snap.PlayerState, ref this.Entities[snap.PlayerState.ClientNum].CurrentState);
			this.ExecuteNewServerCommands(snap.ServerCommandSequence);
			int count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = es;
				cent.Interpolate = false;
				cent.CurrentValid = true;
				cent.Added = false;
				this.ResetEntity(ref cent);
				this.CheckEvents(ref cent);
			}
		}
		private protected virtual void SetNextSnap(in Snapshot snap) {
			this.NextSnap = snap;
			this.PlayerStateToEntityState(ref this.NextSnap.PlayerState, ref this.Entities[snap.PlayerState.ClientNum].NextState);
			int count = this.NextSnap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.NextSnap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.NextState = es;
				if (!cent.CurrentValid || (((cent.CurrentState.EntityFlags ^ es.EntityFlags) & this.GetEntityFlag(EntityFlag.TeleportBit)) != 0)) {
					cent.Interpolate = false;
				} else {
					cent.Interpolate = true;
				}
			}
		}
		private protected virtual void TransitionSnapshot() {
			this.ExecuteNewServerCommands(this.NextSnap.ServerCommandSequence);
			int count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentValid = false;
				cent.Added = false;
			}
			var oldFrame = this.Snap;
			this.Snap = this.NextSnap;
			this.PlayerStateToEntityState(ref this.Snap.PlayerState, ref this.Entities[this.Snap.PlayerState.ClientNum].CurrentState);
			this.Entities[this.Snap.PlayerState.ClientNum].Interpolate = false;
			count = this.Snap.NumEntities;
			for (int i = 0; i < count; i++) {
				ref var es = ref this.Snap.Entities[i];
				ref var cent = ref this.Entities[es.Number];
				cent.CurrentState = cent.NextState;
				cent.CurrentValid = true;
				cent.Added = false;
				if (!cent.Interpolate) {
					this.ResetEntity(ref cent);
				}
				cent.Interpolate = false;
				this.CheckEvents(ref cent);
				cent.SnapshotTime = this.Snap.ServerTime;
			}
			this.NextSnap = null;
			this.TransitionPlayerState(ref this.Snap.PlayerState, ref oldFrame.PlayerState);
		}
		private protected virtual void ResetEntity(ref ClientEntity cent) {
			if (cent.SnapshotTime < this.Time - ClientEntity.EventValidMsec) {
				cent.PreviousEvent = 0;
			}
			cent.LerpOrigin = cent.CurrentState.Origin;
			cent.LerpAngles = cent.CurrentState.Angles;
		}
		private protected virtual unsafe void TransitionPlayerState(ref PlayerState ps, ref PlayerState ops) {
			if (ps.ClientNum != ops.ClientNum) {
				ops = ps;
			}
			if (ps.ExternalEvent != 0 && ps.ExternalEvent != ops.ExternalEvent) {
				ref var cent = ref this.Entities[ps.ClientNum];
				ref var es = ref cent.CurrentState;
				es.Event = ps.ExternalEvent;
				es.EventParm = ps.ExternalEventParm;
				this.HandleEvent(new EntityEventData(in cent));
			}
			for (int i = ps.EventSequence - PlayerState.MaxEvents; i < ps.EventSequence; i++) {
				if (i >= ops.EventSequence
					|| (i > ops.EventSequence - PlayerState.MaxEvents && ps.Events[i & (PlayerState.MaxEvents-1)] != ops.Events[i & (PlayerState.MaxEvents-1)])) {
					ref var cent = ref this.Entities[ps.ClientNum];
					ref var es = ref cent.CurrentState;
					es.Event = ps.Events[i & (PlayerState.MaxEvents-1)];
					es.EventParm = ps.EventParms[i & (PlayerState.MaxEvents-1)];
					this.HandleEvent(new EntityEventData(in cent));
				}
			}
			if (ps.ClientNum != ops.ClientNum) {
				for (int i = 0; i < this.Client.MaxClients; i++) {
					this.NewClientInfo(i);
				}
			}
		}
		protected virtual void ExecuteNewServerCommands(int latestSequence) {
			while (this.ServerCommandSequence < latestSequence) {
				if (this.Client.GetServerCommand(++this.ServerCommandSequence, out var command)) {
					this.ServerCommand(command);
				}
			}
		}
		protected virtual void ServerCommand(Command command) {
			string cmd = command[0];
			if (string.Compare(cmd, "cs", StringComparison.OrdinalIgnoreCase) == 0) {
				this.ConfigstringModified(command);
			} else if (string.Compare(cmd, "scores", StringComparison.OrdinalIgnoreCase) == 0) {
				this.ParseScores(command);
			}
		}
		protected virtual void ConfigstringModified(Command command) {
			int num = command[1].Atoi();
			if (num == this.GetConfigstringIndex(Configstring.LevelStartTime)) {
				this.LevelStartTime = this.Client.GetConfigstring(num).Atoi();
			} else if (num == this.GetConfigstringIndex(Configstring.Scores1)) {
				this.Scores1 = this.Client.GetConfigstring(num).Atoi();
			} else if (num == this.GetConfigstringIndex(Configstring.Scores2)) {
				this.Scores2 = this.Client.GetConfigstring(num).Atoi();
			}
			int csPlayers = this.GetConfigstringIndex(Configstring.Players);
			if (num >= csPlayers && num < csPlayers+this.Client.MaxClients) {
				this.NewClientInfo(num - csPlayers);
			}
		}
		protected virtual string GetConfigstring(Configstring index) {
			return this.Client.GetConfigstring(this.GetConfigstringIndex(index));
		}
		protected virtual void NewClientInfo(int clientNum) {
			string configstring = this.Client.GetConfigstring(clientNum + this.GetConfigstringIndex(Configstring.Players));
			if (string.IsNullOrEmpty(configstring) || configstring[0] == '\0'
				|| !configstring.Contains("n")) {
				this.ClientsInfo[clientNum].Clear();
			} else {
				var info = new InfoString(configstring);
				var t = info["t"].Atoi();
				this.ClientsInfo[clientNum].ClientNum = clientNum;
				this.ClientsInfo[clientNum].Team = Enum.IsDefined(typeof(Team), t) ? (Team)t : Team.Free;
				this.ClientsInfo[clientNum].Name = info["n"];
				this.ClientsInfo[clientNum].InfoValid = true;
			}
			if (this.Initialized) {
				this.NeedNotifyClientInfoChanged = true;
			}
		}
		protected virtual void ParseScores(Command command) {
			int numScores = command[1].Atoi();
			if (numScores > MaxClientScoreSend)
				numScores = MaxClientScoreSend;
			if (numScores > this.Client.MaxClients)
				numScores = this.Client.MaxClients;
			for (int i = 0; i < numScores; i++) {
				int clientNum = command[i*14+4].Atoi();
				if (clientNum < 0 || clientNum > this.Client.MaxClients) {
					continue;
				}
				int score = command[i*14+5].Atoi();
				int ping = command[i*14+6].Atoi();
				this.ClientsInfo[clientNum].Score = score;
				this.ClientsInfo[clientNum].Ping = ping;
			}
			this.NeedNotifyClientInfoChanged = true;
		}
		private protected virtual void CheckEvents(ref ClientEntity cent) {
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
			cent.LerpOrigin = es.PositionTrajectory.Evaluate(this.Snap.ServerTime);
			this.HandleEvent(new EntityEventData(in cent));
		}
		protected virtual EntityEvent HandleEvent(EntityEventData eventData) {
			ref var es = ref eventData.Cent.CurrentState;
			int entityEvent = es.Event & ~(int)EntityEvent.Bits;
			var ev = this.GetEntityEvent(entityEvent);
			if (ev == EntityEvent.None) {
				return EntityEvent.None;
			}
			int clientNum = es.ClientNum;
			if (clientNum < 0 || clientNum >= this.Client.MaxClients) {
				clientNum = 0;
			}
			if (es.EntityType == this.GetEntityType(EntityType.Player)) {
				if (!this.ClientsInfo[clientNum].InfoValid) {
					return EntityEvent.None;
				}
			}
			return ev;
		}
		private protected virtual unsafe void PlayerStateToEntityState(ref PlayerState ps, ref EntityState es) {
			if (ps.PlayerMoveType == PlayerMoveType.Intermission || ps.PlayerMoveType == PlayerMoveType.Spectator) {
				es.EntityType = this.GetEntityType(EntityType.Invisible);
			} else if (ps.Stats[(int)Stat.Health] <= Common.GibHealth) {
				es.EntityType = this.GetEntityType(EntityType.Invisible);
			} else {
				es.EntityType = this.GetEntityType(EntityType.Player);
			}
			es.Number = es.ClientNum = ps.ClientNum;
			es.PositionTrajectory.Type = TrajectoryType.Interpolate;
			es.PositionTrajectory.Base = ps.Origin;
			es.PositionTrajectory.Delta = ps.Velocity;
			es.AnglesTrajectory.Type = TrajectoryType.Interpolate;
			es.AnglesTrajectory.Base = ps.ViewAngles;
			es.EntityFlags = ps.EntityFlags;
			if (ps.Stats[(int)Stat.Health] <= 0) {
				es.EntityFlags |= this.GetEntityFlag(EntityFlag.Dead);
			} else {
				es.EntityFlags &= ~this.GetEntityFlag(EntityFlag.Dead);
			}
			if (ps.ExternalEvent != 0) {
				es.Event = ps.ExternalEvent;
				es.EventParm = ps.ExternalEventParm;
			} else if (ps.EntityEventSequence < ps.EventSequence) {
				if (ps.EntityEventSequence < ps.EventSequence - PlayerState.MaxEvents) {
					ps.EntityEventSequence = ps.EventSequence - PlayerState.MaxEvents;
				}
				int sequence = ps.EntityEventSequence & (PlayerState.MaxEvents-1);
				es.Event = ps.Events[sequence] | ((ps.EntityEventSequence & 3) << 8);
				es.EventParm = ps.EventParms[sequence];
				ps.EntityEventSequence++;
			}
			es.Weapon = ps.Weapon;
			es.GroundEntityNum = ps.GroundEntityNum;
			es.Powerups = 0;
			for (int i = 0; i < (int)Powerup.Max; i++) {
				if (ps.Powerups[i] != 0) {
					es.Powerups |= 1 << i;
				}
			}
			es.VehicleNum = ps.VehicleNum;
		}
		public virtual bool IsInvisible(ref ClientEntity cent) {
			return cent.CurrentState.EntityType == this.GetEntityType(EntityType.Invisible);
		}
		public virtual bool IsPlayer(ref ClientEntity cent) {
			return cent.CurrentState.EntityType == this.GetEntityType(EntityType.Player)
				&& cent.CurrentState.ClientNum >= 0 && cent.CurrentState.ClientNum < this.Client.MaxClients;
		}
		public virtual bool IsVehicle(ref ClientEntity cent, ref ClientEntity player) {
			return false;
		}
		public virtual bool IsMissile(ref ClientEntity cent) {
			return cent.CurrentState.EntityType == this.GetEntityType(EntityType.Missile);
		}
		public virtual bool IsPredictedClient(ref ClientEntity cent) {
			return cent.CurrentState.ClientNum == this.Snap.PlayerState.ClientNum;
		}
		public virtual bool IsFollowed(ref ClientEntity cent) {
			return this.IsPredictedClient(ref cent) && (this.Snap.PlayerState.PlayerMoveFlags & PlayerMoveFlag.Follow) != 0;
		}
		public virtual bool IsNoDraw(ref ClientEntity cent) {
			return (cent.CurrentState.EntityFlags & this.GetEntityFlag(EntityFlag.NoDraw)) != 0;
		}
		public virtual bool IsDead(ref ClientEntity cent) {
			return (cent.CurrentState.EntityFlags & this.GetEntityFlag(EntityFlag.Dead)) != 0;
		}
		public virtual Team GetFlagTeam(ref ClientEntity cent) {
			if (this.IsPlayer(ref cent)) {
				if ((cent.CurrentState.Powerups & (1 << this.GetPowerup(Powerup.RedFlag))) != 0) {
					return Team.Red;
				} else if ((cent.CurrentState.Powerups & (1 << this.GetPowerup(Powerup.BlueFlag))) != 0) {
					return Team.Blue;
				} else if ((cent.CurrentState.Powerups & (1 << this.GetPowerup(Powerup.NeutralFlag))) != 0) {
					return Team.Blue;
				}
			}
			return Team.Spectator;
		}
		protected abstract int GetConfigstringIndex(Configstring index);
		protected abstract EntityEvent GetEntityEvent(int entityEvent);
		protected abstract int GetEntityType(EntityType entityType);
		protected abstract int GetEntityFlag(EntityFlag entityFlag);
		protected abstract int GetPowerup(Powerup powerup);
		protected abstract int GetWeapon(Weapon weapon);
		public abstract Weapon GetWeapon(ref ClientEntity cent, out bool altFire);
		public enum Configstring {
			Scores1 = 6,
			Scores2 = 7,
			GameVersion = 20,
			LevelStartTime = 21,
			Items = 27,
			SiegeState,
			SiegeTimeOverride,
			Sounds,
			Players
		}
		public enum EntityFlag : int {
			Dead,
			TeleportBit,
			PlayerEvent,
			NoDraw,
			AltFiring
		}
		public enum EntityEvent : int {
			None,
			DisruptorMainShot,
			DisruptorSniperShot,
			PlayEffect,
			VoiceCommandSound,
			ConcAltImpact,
			MissileHit,
			MissileMiss,
			MissileMissMetal,
			Bits = 0x300
		}
		public enum EntityType : int {
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
		public enum Powerup : int {
			None,
			Quad,
			Battlesuit,
			Haste,
			Invis,
			Regen,
			Flight,
			Pull,
			RedFlag,
			BlueFlag,
			NeutralFlag,
			Scout,
			Guard,
			Doubler,
			AmmoRegen,
			Invulnerability,
			ShieldHit,
			SpeedBurst,
			Disint4,
			Speed,
			Cloaked,
			ForceLightning,
			ForceEnlightenedLight,
			ForceEnlightenedDark,
			ForceBoon,
			Ysalamiri,
			NumPowerups,
			Max = 16
		}
		public enum Weapon : int {
			//jk
			None,
			StunBaton,
			Melee,
			Saber,
			BryarPistol,
			Blaster,
			Disruptor,
			Bowcaster,
			Repeater,
			Demp2,
			Flechette,
			RocketLauncher,
			Thermal,
			TripMine,
			DetPack,
			Concussion,
			BryarOld,
			EmplacedGun,
			Turret,
			//quake3
			Gauntlet,
			Machinegun,
			Shotgun,
			GrenadeLauncher,
			Lightning,
			Railgun,
			Plasmagun,
			BFG,
			GrapplingHook,
			NumWeapons
		}
		public sealed class EntityEventData {
			public ClientEntity Cent;
			private EntityEventData() {}
			internal EntityEventData(in ClientEntity cent) {
				this.Cent = cent;
			}
		}
	}
}
