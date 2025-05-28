using System;
using System.Collections.Generic;

namespace JKClient {
	public class JAClientHandler : JANetHandler, IClientHandler {
		private const int MaxConfigstringsBase = 1700;
		private const int MaxConfigstringsOJP = 2200;
		public virtual ClientVersion Version { get; protected set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings { get; protected set; } = JAClientHandler.MaxConfigstringsBase;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => true;
		public virtual bool CanParseVehicle => true;
		public virtual string GuidKey => "ja_guid";
		public virtual bool FullByteEncoding => true;
		public virtual GameModification Modification { get; protected set; } = GameModification.Unknown;
		public JAClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public void RequestAuthorization(Action<NetAddress, string> authorize) {}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == GameState.ServerInfo) {
				var info = new InfoString(csStr);
				string gamename = info["gamename"];
				if (gamename.Contains("base_enhanced")) {
					this.Modification = GameModification.BaseEnhanced;
				} else if (gamename.Contains("base_entranced")) {
					this.Modification = GameModification.BaseEntranced;
				} else if (gamename.Contains("JA+ Mod")
					|| gamename.Contains("^4U^3A^5Galaxy")
					|| gamename.Contains("^5X^2Jedi ^5Academy")) {
					this.Modification = GameModification.JAPlus;
				} else if (gamename.Contains("Szlakiem Jedi RPE")
					|| gamename.Contains("Open Jedi Project")
					|| gamename.Contains("OJP Enhanced")
					|| gamename.Contains("OJP Basic")
					|| gamename.Contains("OJRP")) {
					this.Modification = GameModification.OJP;
					this.MaxConfigstrings = JAClientHandler.MaxConfigstringsOJP;
				} else if (gamename.Contains("Movie Battles II")) {
					this.Modification = GameModification.MovieBattlesII;
				} else {
					this.Modification = GameModification.Base;
				}
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			switch (this.Modification) {
			case GameModification.JAPlus:
				return new JAPlusClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
			default:
				return new JAClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
			}
		}
		public virtual bool CanParseSnapshot() {
			switch (this.Modification) {
			default:
				return true;
			case GameModification.Unknown:
			case GameModification.MovieBattlesII:
				return false;
			}
		}
		public virtual IList<NetField> GetEntityStateFields() {
			switch (this.Modification) {
			default:
				return JAClientHandler.entityStateFields26;
			case GameModification.MovieBattlesII:
				return JAClientHandler.entityStateFieldsMBII;
			case GameModification.OJP:
				return JAClientHandler.entityStateFieldsOJP;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot, out int count) {
			if (isVehicle) {
				count = JAClientHandler.vehPlayerStateFields26.Count;
				return JAClientHandler.vehPlayerStateFields26;
			} else {
				if (isPilot()) {
					count = JAClientHandler.pilotPlayerStateFields26.Count-82;
					return JAClientHandler.pilotPlayerStateFields26;
				} else {
					count = JAClientHandler.playerStateFields26.Count;
					switch (this.Modification) {
					default:
						return JAClientHandler.playerStateFields26;
					case GameModification.MovieBattlesII:
						return JAClientHandler.playerStateFieldsMBII;
					case GameModification.OJP:
						return JAClientHandler.playerStateFieldsOJP;
					}
				}
			}
		}
		public virtual void ClearState() {
			this.Modification = GameModification.Unknown;
			this.MaxConfigstrings = JAClientHandler.MaxConfigstringsBase;
		}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol25:
				serverInfo.Version = ClientVersion.JA_v1_00;
				break;
			case ProtocolVersion.Protocol26:
				serverInfo.Version = ClientVersion.JA_v1_01;
				break;
			}
			if (info.Count <= 0) {
				return;
			}
			serverInfo.GameType = (GameType)info["g_gametype"].Atoi();
			serverInfo.NeedPassword = info["g_needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (serverInfo.GameType == GameType.Duel || serverInfo.GameType == GameType.PowerDuel) {
				serverInfo.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				serverInfo.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			serverInfo.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
		}
		private static readonly NetFieldsArray entityStateFields26 = new NetFieldsArray(typeof(EntityState)) {
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Time)		,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.EntityType)	,	8	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.Weapon)	,	8	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	32	},
			{	nameof(EntityState.EntityFlags)	,	32	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Duration)	,	typeof(Trajectory)	,	32	},
			{	0	,	8	},
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*0	,	0	},
			{	0	,	24	},
			{	0	,	2	},
			{	nameof(EntityState.Event)	,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	nameof(EntityState.ClientNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Time)		,	typeof(Trajectory)	,	32	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	nameof(EntityState.Owner)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	nameof(EntityState.EventParm)	,	8	},
			{	0	,	8	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	0	,	0	},
			{	nameof(EntityState.ModelIndex)	,	-16	},
			{	0	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	0	,	1	},
			{	nameof(EntityState.Angles2)	,	sizeof(float)*1	,	0	},
			{	0	,	Common.GEntitynumBits	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*2	,	0	},
			{	0	,	8	},
			{	0	,	8	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*0	,	0	},
			{	0	,	1	},
			{	0	,	16	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*1	,	0	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	6	},
			{	0	,	10	},
			{	0	,	1	},
			{	0	,	2	},
			{	0	,	9	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	32	},
			{	0	,	10	},
			{	nameof(EntityState.Powerups)	,	16	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{	nameof(EntityState.Angles2)	,	sizeof(float)*0	,	0	},
			{	0	,	16	},
			{	nameof(EntityState.Angles2)	,	sizeof(float)*2	,	0	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	6	},
			{	nameof(EntityState.NPCClass)	,	8	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Duration)	,	typeof(Trajectory)	,	32	},
			{	0	,	9	},
			{	0	,	8	},
			{	0	,	16	},
			{	nameof(EntityState.VehicleNum)	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	0	,	32	},
			{	0	,	10	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	6	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	6	},
			{	0	,	9	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	6	},
			{	0	,	6	},
			{	0	,	6	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	}
		};
		private static readonly NetFieldsArray entityStateFieldsMBII = new NetFieldsArray(JAClientHandler.entityStateFields26);
		private static readonly NetFieldsArray entityStateFieldsOJP = new NetFieldsArray(JAClientHandler.entityStateFields26)
			.Override(120, 32)
			.Override(122, 32);
		private static readonly NetFieldsArray playerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*2	,	0	},
			{	0	,	8	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(PlayerState.EntityFlags)	,	32	},
			{	0	,	8	},
			{	nameof(PlayerState.EventSequence)	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	-8	},
			{	0	,	4	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	4	},
			{	0	,	32	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*0	,	10	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	10	},
			{	0	,	8	},
			{	0	,	4	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	4	},
			{	0	,	32	},
			{	0	,	10	},
			{	0	,	10	},
			{	0	,	-16	},
			{	nameof(PlayerState.PlayerMoveFlags)	,	16	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	-16	},
			{	0	,	8	},
			{	nameof(PlayerState.ClientNum)	,	Common.GEntitynumBits	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	16	},
			{	nameof(PlayerState.Weapon)	,	8	},
			{	0	,	16	},
			{	0	,	1	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*2	,	0	},
			{	0	,	32	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	1	},
			{	nameof(PlayerState.ExternalEvent)	,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*1	,	8	},
			{	0	,	2	},
			{	0	,	4	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*0	,	-16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	6	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	8	},
			{	nameof(PlayerState.VehicleNum)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	10	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	16	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	2	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	6	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	}
		};
		private static readonly NetFieldsArray playerStateFieldsMBII = new NetFieldsArray(JAClientHandler.playerStateFields26);
		private static readonly NetFieldsArray playerStateFieldsOJP = new NetFieldsArray(JAClientHandler.playerStateFields26)
			.Override(125, 10)
			.Override(127, 32);
		private static readonly NetFieldsArray pilotPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*2	,	0	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.EntityFlags)	,	32	},
			{	nameof(PlayerState.EventSequence)	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*0	,	10	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	10	},
			{	0	,	4	},
			{	nameof(PlayerState.PlayerMoveFlags)	,	16	},
			{	0	,	-16	},
			{	nameof(PlayerState.ClientNum)	,	Common.GEntitynumBits	},
			{	nameof(PlayerState.Weapon)	,	8	},
			{	0	,	16	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.ExternalEvent)	,	10	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*1	,	8	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*0	,	-16	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	32	},
			{	nameof(PlayerState.VehicleNum)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	8	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*2	,	0	},
			{	0	,	8	},
			{	0	,	0	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	-8	},
			{	0	,	4	},
			{	0	,	4	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	4	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	10	},
			{	0	,	10	},
			{	0	,	-16	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	32	},
			{	0	,	2	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	2	},
			{	0	,	4	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	6	},
			{	0	,	32	},
			{	0	,	2	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	2	},
			{	0	,	8	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	6	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	}
		};
		private static readonly NetFieldsArray vehPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*2	,	0	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(PlayerState.EntityFlags)		,	32	},
			{	nameof(PlayerState.EventSequence)	,	16	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*0	,	10	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	10	},
			{	0	,	4	},
			{	nameof(PlayerState.PlayerMoveFlags)	,	16	},
			{	0	,	-16	},
			{	nameof(PlayerState.ClientNum)	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	nameof(PlayerState.Weapon)	,	8	},
			{	0	,	16	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.ExternalEvent)	,	10	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*1	,	8	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*0	,	-16	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	32	},
			{	nameof(PlayerState.VehicleNum)	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	10	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	}
		};
	}
}
