using System.Collections.Generic;

namespace JKClient {
	public class JABrowserHandler : JANetHandler, IBrowserHandler {
		private const string MasterJK3RavenSoftware = "masterjk3.ravensoft.com";
		private const string MasterJKHub = "master.jkhub.org";
		private const ushort PortMasterJA = 29060;
		public virtual bool NeedStatus { get; private set; }
		public JABrowserHandler(ProtocolVersion protocol) : base(protocol) {}
		public virtual IEnumerable<ServerBrowser.ServerAddress> GetMasterServers() {
			return new ServerBrowser.ServerAddress[] {
				new ServerBrowser.ServerAddress(JABrowserHandler.MasterJK3RavenSoftware, JABrowserHandler.PortMasterJA),
				new ServerBrowser.ServerAddress(JABrowserHandler.MasterJKHub, JABrowserHandler.PortMasterJA)
			};
		}
		public virtual void HandleInfoPacket(in ServerInfo serverInfo, in InfoString info) {
			this.NeedStatus = true;
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
			serverInfo.GameType = (GameType)info["gametype"].Atoi();
			serverInfo.NeedPassword = info["needpass"].Atoi() != 0;
			serverInfo.TrueJedi = info["truejedi"].Atoi() != 0;
			serverInfo.WeaponDisable = info["wdisable"].Atoi() != 0;
			serverInfo.ForceDisable = info["fdisable"].Atoi() != 0;
			if (info.TryGetValue("g_humanplayers", out string humanPlayers)) {
				this.NeedStatus = false;
				serverInfo.Clients = humanPlayers.Atoi();
			}
		}
		public virtual void HandleStatusResponse(in ServerInfo serverInfo, in InfoString info) {
			this.NeedStatus = false;
		}
	}
}
