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
		public virtual int GetEntityFieldOverride(int index, int defaultValue) {
			return defaultValue;
		}
		public virtual int GetPlayerFieldOverride(int index, int defaultValue) {
			return defaultValue;
		}
		public virtual void ClearState() {
			this.gameMod = GameMod.Base;
		}
		public virtual void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			serverInfo.Version = ClientVersion.Q3_v1_32;
			serverInfo.GameType = Q3ClientHandler.GetGameType(info["gametype"].Atoi());
		}
		internal static GameType GetGameType(int gameType) {
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
	}
}
