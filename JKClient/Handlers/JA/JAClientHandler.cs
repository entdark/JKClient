namespace JKClient {
	public class JAClientHandler : JANetHandler, IClientHandler {
		private GameMod gameMod = GameMod.Undefined;
		public virtual ClientVersion Version { get; private set; }
		public virtual int MaxReliableCommands => 128;
		public virtual int MaxConfigstrings => 1700;
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
				var infoString = new InfoString(csStr);
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
		}
		public virtual ClientGame CreateClientGame(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum) {
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
		public virtual int GetEntityFieldOverride(int index, int defaultValue) {
			switch (this.gameMod) {
			case GameMod.MBII:
				//TODOorNOTTODO
				break;
			case GameMod.OJP:
				switch (index) {
				case 120: return 32;
				case 122: return 32;
				}
				break;
			}
			return defaultValue;
		}
		public virtual int GetPlayerFieldOverride(int index, int defaultValue) {
			switch (this.gameMod) {
			case GameMod.MBII:
				//TODOorNOTTODO
				break;
			case GameMod.OJP:
				switch (index) {
				case 125: return 10;
				case 127: return 32;
				}
				break;
			}
			return defaultValue;
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Undefined;
		}
		public virtual void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			switch (serverInfo.Protocol) {
			case ProtocolVersion.Protocol25:
				serverInfo.Version = ClientVersion.JA_v1_00;
				break;
			case ProtocolVersion.Protocol26:
				serverInfo.Version = ClientVersion.JA_v1_01;
				break;
			}
			serverInfo.GameType = (GameType)info["gametype"].Atoi();
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
	}
}
