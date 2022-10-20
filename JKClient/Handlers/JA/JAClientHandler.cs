using System;
using System.Collections.Generic;

namespace JKClient {
	public class JAClientHandler : JANetHandler, IClientHandler {
		private const int MaxConfigstringsBase = 1700;
		private const int MaxConfigstringsOJP = 2200;
		private GameMod gameMod = GameMod.Undefined;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings { get; private set; } = JAClientHandler.MaxConfigstringsBase;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => true;
		public virtual bool CanParseVehicle => true;
		public virtual string GuidKey => "ja_guid";
		public virtual bool RequiresAuthorization => false;
		public virtual bool FullByteEncoding => true;
		public JAClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {
			if (i == GameState.ServerInfo) {
				var info = new InfoString(csStr);
				string gamename = info["gamename"];
				//TODO: add mod handlers
				if (gamename.Contains("Szlakiem Jedi RPE")
					|| gamename.Contains("Open Jedi Project")
					|| gamename.Contains("OJP Enhanced")
					|| gamename.Contains("OJP Basic")
					|| gamename.Contains("OJRP")) {
					this.gameMod = GameMod.OJP;
					this.MaxConfigstrings = JAClientHandler.MaxConfigstringsOJP;
				} else if (gamename.Contains("Movie Battles II")) {
					this.gameMod = GameMod.MBII;
				} else {
					this.gameMod = GameMod.Base;
				}
			}
		}
		public virtual ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new JAClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			switch (this.gameMod) {
			default:
				return true;
			case GameMod.Undefined:
			case GameMod.MBII:
				return false;
			}
		}
		public virtual IList<NetField> GetEntityStateFields() {
			switch (this.gameMod) {
			default:
				return JAClientHandler.entityStateFields26;
			case GameMod.MBII:
				return JAClientHandler.entityStateFieldsMBII;
			case GameMod.OJP:
				return JAClientHandler.entityStateFieldsOJP;
			}
		}
		public virtual IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot) {
			if (isVehicle) {
				return JAClientHandler.vehPlayerStateFields26;
			} else {
				if (isPilot()) {
					return JAClientHandler.pilotPlayerStateFields26;
				} else {
					switch (this.gameMod) {
					default:
						return JAClientHandler.playerStateFields26;
					case GameMod.MBII:
						return JAClientHandler.playerStateFieldsMBII;
					case GameMod.OJP:
						return JAClientHandler.playerStateFieldsOJP;
					}
				}
			}
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Undefined;
			this.MaxConfigstrings = JAClientHandler.MaxConfigstringsBase;
		}
		public virtual void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info) {
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
		private enum GameMod {
			Undefined,
			Base,
			MBII,
			OJP
		}
		private static readonly NetFieldsArray entityStateFields26 = new NetFieldsArray(typeof(EntityState)) {
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	nameof(EntityState.EntityType)	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	32	},
			{	nameof(EntityState.EntityFlags)	,	32	},
			{	0	,	32	},
			{	0	,	8	},
			{	nameof(EntityState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	24	},
			{	0	,	2	},
			{	nameof(EntityState.Event)	,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	nameof(EntityState.ClientNum)	,	Common.GEntitynumBits	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{   nameof(EntityState.EventParm)   ,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	-16	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	1	},
			{	0	,	16	},
			{	nameof(EntityState.OtherEntityNum)	,	Common.GEntitynumBits	},
			{	0	,	0	},
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
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	6	},
			{	0	,	8	},
			{	0	,	32	},
			{	0	,	9	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
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
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	nameof(PlayerState.GroundEntityNum)	,	Common.GEntitynumBits	},
			{   nameof(PlayerState.EntityFlags) ,	32	},
			{	0	,	8	},
			{   nameof(PlayerState.EventSequence) ,	16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	-8	},
			{	0	,	4	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	4	},
			{	0	,	32	},
			{	nameof(PlayerState.Events)  ,   sizeof(int)*0	,   10	},
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
			{   nameof(PlayerState.PlayerMoveFlags) ,	16	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	-16	},
			{	0	,	8	},
			{   nameof(PlayerState.ClientNum) ,	Common.GEntitynumBits	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	1	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	2	},
			{	0	,	32	},
			{	0	,	8	},
			{	0	,	1	},
			{   nameof(PlayerState.ExternalEvent) ,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	1	},
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*1   ,   8	},
			{	0	,	2	},
			{	0	,	4	},
			{	nameof(PlayerState.PlayerMoveType)	,	8	},
			{	nameof(PlayerState.ExternalEventParm)	,	8	},
			{   nameof(PlayerState.EventParms)  ,   sizeof(int)*0   ,   -16	},
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
			{   nameof(PlayerState.VehicleNum) ,	Common.GEntitynumBits	},
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
			{	0	,	1   }
		};
		private static readonly NetFieldsArray playerStateFieldsMBII = new NetFieldsArray(JAClientHandler.playerStateFields26);
		private static readonly NetFieldsArray playerStateFieldsOJP = new NetFieldsArray(JAClientHandler.playerStateFields26)
			.Override(125, 10)
			.Override(127, 32);
		private static readonly NetFieldsArray pilotPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	10	},
			{	0	,	10	},
			{	0	,	4	},
			{	0	,	16	},
			{	0	,	-16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	-16	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	32	},
			{	0	,	Common.GEntitynumBits	},
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
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	8	},
			{	0	,	0	},
			{	0	,	Common.GEntitynumBits	},
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
			{	0	,	1   }
		};
		private static readonly NetFieldsArray vehPlayerStateFields26 = new NetFieldsArray(typeof(PlayerState)) {
			{	0	,	32	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	0	},
			{	0	,	-16	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	32	},
			{	0	,	16	},
			{	0	,	16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	10	},
			{	0	,	10	},
			{	0	,	4	},
			{	0	,	16	},
			{	0	,	-16	},
			{	0	,	Common.GEntitynumBits	},
			{	0	,	16	},
			{	0	,	8	},
			{	0	,	16	},
			{	0	,	0	},
			{	0	,	10	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	8	},
			{	0	,	-16	},
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
			{	0	,	Common.GEntitynumBits	},
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
			{	0	,	1   }
		};
	}
}
