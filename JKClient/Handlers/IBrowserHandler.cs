using System.Collections.Generic;

namespace JKClient {
	public interface IBrowserHandler : INetHandler {
		IEnumerable<ServerBrowser.ServerAddress> GetMasterServers();
		void SetExtraServerInfo(ServerInfo serverInfo, InfoString info);
	}
}
