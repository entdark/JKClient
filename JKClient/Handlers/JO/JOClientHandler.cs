﻿using System;
using System.Collections.Generic;

namespace JKClient {
	public class JOClientHandler : JONetHandler, IClientHandler {
		public virtual new ProtocolVersion Protocol => (ProtocolVersion)base.Protocol;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings => 1400;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => throw new NotImplementedException();
		public virtual bool FullByteEncoding => false;
		public virtual GameModification Modification => GameModification.Base;
		public JOClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public void RequestAuthorization(Action<NetAddress, string> authorize) {}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//JO doesn't have setgame command, the rest commands match
			if (cmd >= ServerCommandOperations.SetGame) {
				cmd++;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == GameState.ServerInfo) {
				var info = new InfoString(csStr);
				if (info["version"].Contains("v1.03")) {
					this.Version = ClientVersion.JO_v1_03;
				}
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new JOClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields() {
			switch (this.Protocol) {
			default:
				throw new JKClientException("Protocol not supported");
			case ProtocolVersion.Protocol15 when this.Version == ClientVersion.JO_v1_03:
			case ProtocolVersion.Protocol16:
				return JOClientHandler.entityStateFields16;
			case ProtocolVersion.Protocol15:
				return JOClientHandler.entityStateFields15;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot, out int count) {
			switch (this.Protocol) {
			default:
				throw new JKClientException("Protocol not supported");
			case ProtocolVersion.Protocol15 when this.Version == ClientVersion.JO_v1_03:
			case ProtocolVersion.Protocol16:
				count = JOClientHandler.playerStateFields16.Count;
				return JOClientHandler.playerStateFields16;
			case ProtocolVersion.Protocol15:
				count = JOClientHandler.playerStateFields15.Count;
				return JOClientHandler.playerStateFields15;
			}
		}
		public virtual void ClearState() {}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol15 when info["version"].Contains("v1.03"):
				serverInfo.Version = ClientVersion.JO_v1_03;
				break;
			case ProtocolVersion.Protocol15:
				serverInfo.Version = ClientVersion.JO_v1_02;
				break;
			case ProtocolVersion.Protocol16:
				serverInfo.Version = ClientVersion.JO_v1_04;
				break;
			}
			if (info.Count <= 0) {
				return;
			}
			int gameType = info["g_gametype"].Atoi();
			//JO doesn't have Power Duel, the rest game types match
			if (gameType >= (int)GameType.PowerDuel) {
				gameType++;
			}
			serverInfo.GameType = (GameType)gameType;
			serverInfo.NeedPassword = info["g_needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (serverInfo.GameType == GameType.Duel) {
				serverInfo.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				serverInfo.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			serverInfo.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
		}
		private static unsafe readonly NetFieldsArray entityStateFields15 = new NetFieldsArray(typeof(EntityState)) {
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Time)		,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.Event)	,	10	},
			{	0	,	0	},
			{	nameof(EntityState.EntityType)	,	8	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(EntityState.EventParm)	,	8	},
			{	0	,	16	},
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//JO doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.NonLinearStop) {
						(*value)++;
					}
				}
			}	},
			{	nameof(EntityState.EntityFlags)	,	32	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	6	},
			{	0	,	32	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.Weapon)	,	8	},
			{	nameof(EntityState.ClientNum)	,	8	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Duration)	,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//JO doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.NonLinearStop) {
						(*value)++;
					}
				}
			}	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*2	,	0	},
			{	0	,	24	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	1	},
			{	nameof(EntityState.Powerups)	,	16	},
			{	0	,	4	},
			{	0	,	8	},
			{	nameof(EntityState.ModelIndex)	,	-8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*0	,	0	},
			{	0	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Time)		,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Duration)	,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Base)		,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Delta)	,	typeof(Trajectory)	,	sizeof(float)*2	,	0	},
			{	0	,	32	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*2	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	1	}
		};
		private static readonly NetFieldsArray entityStateFields16 = new NetFieldsArray(entityStateFields15)
			.Override(44, 5);
		private static unsafe readonly NetFieldsArray playerStateFields15 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*1	,	0	},
			{	0	,	8	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*0	,	0	},
			{	0	,	-16	},
			{	0	,	32	},
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*2	,	0	},
			{	0	,	-16	},
			{	nameof(PlayerState.EventSequence)	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	4	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*0	,	10	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	10	},
			{	nameof(PlayerState.PlayerMoveFlags)	,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	4	},
			{	nameof(PlayerState.EntityFlags)	,	32	},
			{	nameof(PlayerState.ExternalEvent)	,	10	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{	0	,	-8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	2	},
			{	0	,	8	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(PlayerMoveType), *value)) {
					var pmType = (PlayerMoveType)(*value);
					//JO doesn't have jetpack player movement, the rest movements match
					if (pmType >= PlayerMoveType.Jetpack) {
						(*value)++;
					}
				}
			}	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*0	,	-16	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*1	,	8	},
			{	nameof(PlayerState.ClientNum)	,	8	},
			{	nameof(PlayerState.Weapon)	,	5	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*2	,	0	},
			{	0	,	10	},
			{	0	,	16	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	2	},
			{	0	,	1	},
			{	0	,	2	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	6	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	1	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	2	},
			{	0	,	2	},
			{	0	,	4	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	1	},
			{	0	,	32	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	}
		};
		private static unsafe readonly NetFieldsArray playerStateFields16 = new NetFieldsArray(playerStateFields15)
			.Insert(79, 0, 1)
			.Insert(80, 0, 1)
			.Insert(81, 0, 1);
	}
}
