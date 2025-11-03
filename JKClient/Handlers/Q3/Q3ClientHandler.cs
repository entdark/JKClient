using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace JKClient {
	public class Q3ClientHandler : Q3NetHandler, IClientHandler {
		private NetAddress authorizeServer;
		public virtual ClientVersion Version => ClientVersion.Q3_v1_32;
		public virtual int MaxReliableCommands => 64;
		public virtual int MaxConfigstrings => 1024;
		public virtual int MaxClients => 64;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "cl_guid";
		public virtual bool FullByteEncoding => false;
		public virtual GameModification Modification { get; protected set; } = GameModification.Base;
		public virtual string CDKey { get; set; } = string.Empty;
		public Q3ClientHandler(ProtocolVersion protocol) : base(protocol) {}
		public void RequestAuthorization(Action<NetAddress, string> authorize) {
			if (this.authorizeServer == null) {
				this.authorizeServer = NetSystem.StringToAddress("authorize.quake3arena.com", 27952);
				if (this.authorizeServer == null) {
					Debug.WriteLine("Couldn't resolve authorize address");
					return;
				}
			}
			string nums = Regex.Replace(this.CDKey, "[^a-zA-Z0-9]", string.Empty);
			authorize(this.authorizeServer, $"getKeyAuthorize {0} {nums}");
		}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//Q3 doesn't have setgame and mapchange commands, the rest commands match
			if (cmd == ServerCommandOperations.SetGame) {
				cmd = ServerCommandOperations.EOF;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == (int)Q3ClientGame.ConfigstringQ3.GameVersion) {
				if (csStr.Contains("cpma")) {
					this.Modification = GameModification.CPMA;
				} else if (csStr.Contains("defrag")) {
					this.Modification = GameModification.DeFRaG;
				}
			} else if (i == 12 && csStr.Contains("RA3")) {
				this.Modification = GameModification.RocketArena3;
			} else if (i == 872 && csStr.Length > 0 && csStr[0] != '\0') {
				this.Modification = GameModification.OSP;
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new Q3ClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields() {
			return Q3ClientHandler.entityStateFields68;
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot, out int count) {
			count = Q3ClientHandler.playerStateFields68.Count;
			return Q3ClientHandler.playerStateFields68;
		}
		public virtual void ClearState() {
			this.Modification = GameModification.Base;
		}
		public virtual void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info) {
			serverInfo.Version = ClientVersion.Q3_v1_32;
			if (info.Count <= 0) {
				return;
			}
			serverInfo.GameType = Q3ClientHandler.GetGameType(info["g_gametype"].Atoi());
		}
		private static GameType GetGameType(int gameType) {
			switch (gameType) {
			case 0:
				return GameType.FFA;
			case 1:
				return GameType.Duel;
			case 2:
				return GameType.SinglePlayer;
			case 3:
				return GameType.Team;
			case 4:
				return GameType.CTF;
			default:
				return (GameType)(gameType+5);
			}
		}
		private static readonly unsafe NetFieldsArray entityStateFields68 = new NetFieldsArray(typeof(EntityState)) {
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
			{	0	,	8	},
			{	nameof(EntityState.EventParm)	,	8	},
			{	0	,	8	},
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//Q3 doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.NonLinearStop) {
						(*value)++;
					}
				}
			}	},
			{	nameof(EntityState.EntityFlags)	,	19	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{	nameof(EntityState.Weapon)	,	8	},
			{	nameof(EntityState.ClientNum)	,	8	},
			{	nameof(EntityState.Angles)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.PositionTrajectory)	,	nameof(Trajectory.Duration)	,	typeof(Trajectory)	,	32	},
			{	nameof(EntityState.AnglesTrajectory)	,	nameof(Trajectory.Type)		,	typeof(Trajectory)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(TrajectoryType), *value)) {
					var trType = (TrajectoryType)(*value);
					//Q3 doesn't have non-linear stop trajectory type, the rest types match
					if (trType >= TrajectoryType.NonLinearStop) {
						(*value)++;
					}
				}
			}	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*1	,	0	},
			{	nameof(EntityState.Origin)	,	sizeof(float)*2	,	0	},
			{	0	,	24	},
			{	nameof(EntityState.Powerups)	,	16	},
			{	nameof(EntityState.ModelIndex)	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	8	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*2	,	0	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*0	,	0	},
			{	nameof(EntityState.Origin2)	,	sizeof(float)*1 ,	0	},
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
			{	0	,	16	}
		};
		private static readonly unsafe NetFieldsArray playerStateFields68 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*1	,	0	},
			{	0	,	8	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*0	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*1	,	0	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*0	,	0	},
			{	0	,	-16	},
			{	nameof(PlayerState.Origin)	,	sizeof(float)*2	,	0	},
			{	nameof(PlayerState.Velocity)	,	sizeof(float)*2	,	0	},
			{	0	,	8	},
			{	0	,	-16	},
			{	nameof(PlayerState.EventSequence)	,	16	},
			{	0	,	8	},
			{	0	,	4	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*0	,	8	},
			{	0	,	8	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	8	},
			{	nameof(PlayerState.PlayerMoveFlags)	,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	4	},
			{	nameof(PlayerState.EntityFlags)	,	16	},
			{	nameof(PlayerState.ExternalEvent)	,	10	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{	0	,	-8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	,	(value) => {
				if (Enum.IsDefined(typeof(PlayerMoveType), *value)) {
					var pmType = (PlayerMoveType)(*value);
					//Q3 doesn't have jetpack and float player movements, the rest movements match
					if (pmType >= PlayerMoveType.Jetpack) {
						(*value)+=2;
					}
				}
			}	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	12	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*1	,	8	},
			{	nameof(PlayerState.EventParms)	,	sizeof(int)*0	,	8	},
			{	nameof(PlayerState.ClientNum)	,	8	},
			{	nameof(PlayerState.Weapon)	,	5	},
			{	nameof(PlayerState.ViewAngles)	,	sizeof(float)*2	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	10	},
			{	0	,	16	}
		};
	}
}
