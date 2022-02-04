using System;

namespace JKClient {
	//TODO: remake to struct?
	public sealed class ServerInfo {
		public NetAddress Address;
		public string HostName;
		public string MapName;
		public string Game;
		public GameType GameType;
		public int Clients;
		public int MaxClients;
		public int MinPing;
		public int MaxPing;
		public int Ping;
		public bool Visibile;
		public bool NeedPassword;
		public bool TrueJedi;
		public bool WeaponDisable;
		public bool ForceDisable;
		public ProtocolVersion Protocol;
		public ClientVersion Version;
		public bool Pure;
		internal bool InfoSet;
		internal long Start;
		internal void SetInfo(InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			this.Version = JKClient.GetVersion(this.Protocol);
			this.Clients = info["clients"].Atoi();
			this.HostName = info["hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.Game = info["game"];
			this.GameType = ServerInfo.GetGameType(info["gametype"].Atoi(), this.Protocol);
			this.MinPing = info["minping"].Atoi();
			this.MaxPing = info["maxping"].Atoi();
			this.NeedPassword = info["needpass"].Atoi() != 0;
			this.TrueJedi = info["truejedi"].Atoi() != 0;
			this.WeaponDisable = info["wdisable"].Atoi() != 0;
			this.ForceDisable = info["fdisable"].Atoi() != 0;
			//JO doesn't have Power Duel, the rest game types match
			if (JKClient.IsJO(this.Protocol) && this.GameType >= GameType.PowerDuel) {
				this.GameType++;
			}
			this.Pure = info["pure"].Atoi() != 0;
			this.InfoSet = true;
		}
		internal void SetConfigstringInfo(InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			if (this.Protocol == ProtocolVersion.Protocol15 && info["version"].Contains("v1.03")) {
				this.Version = ClientVersion.JO_v1_03;
			} else {
				this.Version = JKClient.GetVersion(this.Protocol);
			}
			this.HostName = info["sv_hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.GameType = ServerInfo.GetGameType(info["g_gametype"].Atoi(), this.Protocol);
			this.MinPing = info["sv_minping"].Atoi();
			this.MaxPing = info["sv_maxping"].Atoi();
			this.NeedPassword = info["g_needpass"].Atoi() != 0;
			this.TrueJedi = info["g_jediVmerc"].Atoi() != 0;
			if (this.GameType == GameType.Duel || this.GameType == GameType.PowerDuel) {
				this.WeaponDisable = info["g_duelWeaponDisable"].Atoi() != 0;
			} else {
				this.WeaponDisable = info["g_weaponDisable"].Atoi() != 0;
			}
			this.ForceDisable = info["g_forcePowerDisable"].Atoi() != 0;
			this.InfoSet = true;
		}
		private static GameType GetGameType(int gameType, ProtocolVersion protocol) {
			if (JKClient.IsQ3(protocol)) {
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
			//JO doesn't have Power Duel, the rest game types match
			} else if (JKClient.IsJO(protocol) && gameType >= (int)GameType.PowerDuel) {
				gameType++;
			}
			return (GameType)gameType;
		}
	}
}
