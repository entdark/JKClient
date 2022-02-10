using System.Collections.Generic;

namespace JKClient {
	public class JOBrowserHandler : JONetHandler, IBrowserHandler {
		private const string MasterJK2RavenSoftware = "masterjk2.ravensoft.com";
		private const string MasterJKHub = "master.jkhub.org";
		private const string MasterJK2MV = "master.jk2mv.org";
		private const ushort PortMasterJO = 28060;
		public JOBrowserHandler(ProtocolVersion protocol) : base(protocol) {}
		public virtual IEnumerable<ServerBrowser.ServerAddress> GetMasterServers() {
			return new ServerBrowser.ServerAddress[] {
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJK2RavenSoftware, JOBrowserHandler.PortMasterJO),
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJKHub, JOBrowserHandler.PortMasterJO),
				new ServerBrowser.ServerAddress(JOBrowserHandler.MasterJK2MV, JOBrowserHandler.PortMasterJO)
			};
		}
		public virtual void SetExtraServerInfo(ServerInfo serverInfo, InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			switch (serverInfo.Protocol) {
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
			serverInfo.NeedPassword = info["needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["truejedi"].Atoi() != 0;
			serverInfo.WeaponDisable = info["wdisable"].Atoi() != 0;
			serverInfo.ForceDisable = info["fdisable"].Atoi() != 0;
		}
	}
}
