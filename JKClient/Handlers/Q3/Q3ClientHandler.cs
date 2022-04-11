﻿using System.Collections.Generic;
using System;

namespace JKClient {
	public class Q3ClientHandler : Q3NetHandler, IClientHandler {
		private GameMod gameMod = GameMod.Base;
		public virtual ClientVersion Version => ClientVersion.Q3_v1_32;
		public virtual int MaxReliableCommands => 64;
		public virtual int MaxConfigstrings => 1024;
		public virtual int MaxClients => 64;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "cl_guid";
		public virtual bool RequiresAuthorization => true;
		public virtual bool FullByteEncoding => false;
		public Q3ClientHandler(ProtocolVersion protocol) : base(protocol) {}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//Q3 doesn't have setgame and mapchange commands, the rest commands match
			if (cmd == ServerCommandOperations.SetGame) {
				cmd = ServerCommandOperations.EOF;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == (int)Q3ClientGame.ConfigstringQ3.GameVersion) {
				if (csStr.Contains("cpma")) {
					this.gameMod = GameMod.CPMA;
				} else if (csStr.Contains("defrag")) {
					this.gameMod = GameMod.DeFRaG;
				}
			} else if (i == 12 && csStr.Contains("RA3")) {
				this.gameMod = GameMod.RocketArena3;
			} else if (i == 872 && csStr.Length > 0 && csStr[0] != '\0') {
				this.gameMod = GameMod.OSP;
			}
		}
		public virtual ClientGame CreateClientGame(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new Q3ClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual IList<NetField> GetEntityStateFields() {
			return Q3ClientHandler.entityStateFields68;
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot) {
			return Q3ClientHandler.playerStateFields68;
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Base;
		}
		public virtual void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info) {
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
		private enum GameMod {
			Base,
			DeFRaG,
			CPMA,
			OSP,
			RocketArena3
		}
		private static readonly NetFieldsArray entityStateFields68 = new NetFieldsArray(typeof(EntityState)) {
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	nameof(EntityState.Event)	,	10	},
			{	0	,	0	},
			{	nameof(EntityState.EntityType)	,	8	},
			{	0	,	8	},
			{   nameof(EntityState.EventParm)   ,	8	},
			{	0	,	8	},
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	nameof(EntityState.EntityFlags)	,	19	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	nameof(EntityState.ClientNum)	,	8	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	24	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	16	}
		};
		private static unsafe readonly NetFieldsArray playerStateFields68 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	-16	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	-16	},
			{   nameof(PlayerState.EventSequence) ,	16	},
			{	0	,	8	},
			{	0	,	4	},
			{	nameof(PlayerState.Events)  ,   sizeof(int)*0	,   8	},
			{	0	,	8	},
			{	nameof(PlayerState.Events)	,	sizeof(int)*1	,	8	},
			{   nameof(PlayerState.PlayerMoveFlags) ,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	4	},
			{   nameof(PlayerState.EntityFlags) ,	16	},
			{   nameof(PlayerState.ExternalEvent) ,	10	},
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
			{	nameof(PlayerState.PlayerMoveType)	,	8	, (value) => {
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
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8	},
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   8	},
			{   nameof(PlayerState.ClientNum) ,	8	},
			{	0	,	5	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	10	},
			{	0	,	16	}
		};
	}
}
