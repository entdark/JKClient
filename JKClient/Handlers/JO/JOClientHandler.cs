using System;

namespace JKClient {
	public class JOClientHandler : JONetHandler, IClientHandler {
		public virtual ClientVersion Version { get; internal set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings => 1400;
		public virtual int MaxClients => 32;
		public virtual bool CanParseRMG => false;
		public virtual bool CanParseVehicle => false;
		public virtual string GuidKey => "";
		public virtual bool RequiresAuthorization => false;
		public virtual bool FullByteEncoding => false;
		public JOClientHandler(ProtocolVersion protocol, ClientVersion version) : base(protocol) {
			this.Version = version;
		}
		public virtual void AdjustServerCommandOperations(ref ServerCommandOperations cmd) {
			//JO doesn't have setgame command, the rest commands match
			if (cmd >= ServerCommandOperations.SetGame) {
				cmd++;
			}
		}
		public virtual void AdjustGameStateConfigstring(int i, string csStr) {}
		public virtual ClientGame CreateClientGame(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum) {
			return new JOClientGame(client, serverMessageNum, serverCommandSequence, clientNum);
		}
		public virtual bool CanParseSnapshot() {
			return true;
		}
		public virtual int GetEntityFieldOverride(int index, int defaultValue) {
			return defaultValue;
		}
		public virtual int GetPlayerFieldOverride(int index, int defaultValue) {
			return defaultValue;
		}
		public virtual void ClearState() {}
		public virtual void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info) {
			if (info.Count <= 0) {
				return;
			}
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
			int gameType = info["gametype"].Atoi();
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
	}
}
