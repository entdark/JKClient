using System.Collections.Generic;

namespace JKClient {
	public interface IBrowserHandler : INetHandler {
		bool NeedStatus { get; }
		IEnumerable<ServerBrowser.ServerAddress> GetMasterServers();
		void HandleInfoPacket(ServerInfo serverInfo, InfoString info);
		void HandleStatusResponse(ServerInfo serverInfo, InfoString info);
	}
}
