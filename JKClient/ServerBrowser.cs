using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JKClient {
	public sealed class ServerBrowser : NetClient {
		private const int RefreshTimeout = 3000;
		private readonly List<ServerAddress> masterServers;
		private readonly Dictionary<NetAddress, ServerInfo> globalServers;
		private TaskCompletionSource<IEnumerable<ServerInfo>> getListTCS, refreshListTCS;
		private long serverRefreshTimeout = 0L;
		private IBrowserHandler BrowserHandler => this.NetHandler as IBrowserHandler;
		public ServerBrowser(IBrowserHandler browserHandler, IEnumerable<ServerAddress> customMasterServers = null, bool customOnly = false)
			: base(browserHandler) {
			if (customOnly && customMasterServers == null) {
				throw new JKClientException(new ArgumentNullException(nameof(customMasterServers)));
			}
			if (customOnly) {
				this.masterServers = new List<ServerAddress>(customMasterServers);
			} else {
				this.masterServers = new List<ServerAddress>(this.BrowserHandler.GetMasterServers());
				if (customMasterServers != null) {
					this.masterServers.AddRange(customMasterServers);
				}
			}
			this.globalServers = new Dictionary<NetAddress, ServerInfo>(new NetAddressComparer());
		}
		private protected override void OnStop() {
			this.getListTCS?.TrySetCanceled();
			this.refreshListTCS?.TrySetCanceled();
			this.serverRefreshTimeout = 0;
			base.OnStop();
		}
		private protected override async Task Run() {
			const int frameTime = 8;
			while (true) {
				this.GetPacket();
				if (this.serverRefreshTimeout != 0 && this.serverRefreshTimeout < Common.Milliseconds) {
					this.getListTCS?.TrySetResult(this.globalServers.Values);
					this.getListTCS = null;
					this.refreshListTCS?.TrySetResult(this.globalServers.Values);
					this.refreshListTCS = null;
					this.serverRefreshTimeout = 0;
				}
				await Task.Delay(frameTime);
			}
		}
		public async Task<IEnumerable<ServerInfo>> GetNewList() {
			this.getListTCS?.TrySetCanceled();
			this.getListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			this.globalServers.Clear();
			foreach (var masterServer in this.masterServers) {
				var address = NetSystem.StringToAddress(masterServer.Name, masterServer.Port);
				if (address == null) {
					continue;
				}
				this.OutOfBandPrint(address, $"getservers {this.Protocol}");
			}
			this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			return await this.getListTCS.Task;
		}
		public async Task<IEnumerable<ServerInfo>> RefreshList() {
			if (this.globalServers.Count <= 0) {
				return await this.GetNewList();
			}
			this.refreshListTCS?.TrySetCanceled();
			this.refreshListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			foreach (var server in this.globalServers) {
				var serverInfo = server.Value;
				serverInfo.InfoSet = false;
				serverInfo.Start = Common.Milliseconds;
				this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
			}
			this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			return await this.refreshListTCS.Task;
		}
		private protected override unsafe void PacketEvent(NetAddress address, Message msg) {
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					msg.BeginReading(true);
					msg.ReadLong();
					string s = msg.ReadStringLineAsString();
					var command = new Command(s);
					string c = command.Argv(0);
					if (string.Compare(c, "infoResponse", StringComparison.OrdinalIgnoreCase) == 0) {
						this.ServerInfoPacket(address, msg);
					} else if (string.Compare(c, "statusResponse", StringComparison.OrdinalIgnoreCase) == 0) {
						this.ServerStatusResponse(address, msg);
					} else if (string.Compare(c, 0, "getserversResponse", 0, 18, StringComparison.Ordinal) == 0) {
						this.ServersResponsePacket(address, msg);
					}
				}
			}
		}
		private unsafe void ServersResponsePacket(NetAddress address, Message msg) {
			fixed (byte *b = msg.Data) {
				byte *buffptr = b;
				byte *buffend = buffptr + msg.CurSize;
				do {
					if (*buffptr == 92) { //'\\'
						break;
					}
					buffptr++;
				} while (buffptr < buffend);
				while (buffptr + 1 < buffend) {
					if (*buffptr != 92) { //'\\'
						break;
					}
					buffptr++;
					byte []ip = new byte[4];
					if (buffend - buffptr < ip.Length + sizeof(ushort) + 1) {
						break;
					}
					for (int i = 0; i < ip.Length; i++) {
						ip[i] = *buffptr++;
					}
					int port = (*buffptr++) << 8;
					port += *buffptr++;
					var serverInfo = new ServerInfo() {
						Address = new NetAddress(ip, (ushort)port),
						Start = Common.Milliseconds
					};
					this.globalServers[serverInfo.Address] = serverInfo;
					this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
					if (*buffptr != 92 && *buffptr != 47) { //'\\' '/'
						break;
					}
				}
			}
		}
		private void ServerStatusResponse(NetAddress address, Message msg) {
			var info = new InfoString(msg.ReadStringLineAsString());
			if (this.globalServers.ContainsKey(address)) {
				var serverInfo = this.globalServers[address];
				this.BrowserHandler.HandleStatusResponse(serverInfo, info);
				this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			}
		}
		private void ServerInfoPacket(NetAddress address, Message msg) {
			var info = new InfoString(msg.ReadStringAsString());
			if (this.globalServers.ContainsKey(address)) {
				var serverInfo = this.globalServers[address];
				if (serverInfo.InfoSet) {
					return;
				}
				serverInfo.Ping = (int)(Common.Milliseconds - serverInfo.Start);
				serverInfo.SetInfo(info);
				this.BrowserHandler.HandleInfoPacket(serverInfo, info);
				if (this.BrowserHandler.NeedStatus) {
					this.OutOfBandPrint(serverInfo.Address, "getstatus");
				}
				this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			}
		}
		public sealed class ServerAddress {
			public string Name { get; private set; }
			public ushort Port { get; private set; }
			public ServerAddress(string name, ushort port) {
				this.Name = name;
				this.Port = port;
			}
		}
		public static IBrowserHandler GetKnownBrowserHandler(ProtocolVersion protocol) {
			switch (protocol) {
			case ProtocolVersion.Protocol25:
			case ProtocolVersion.Protocol26:
				return new JABrowserHandler(protocol);
			case ProtocolVersion.Protocol15:
			case ProtocolVersion.Protocol16:
				return new JOBrowserHandler(protocol);
			case ProtocolVersion.Protocol68:
			case ProtocolVersion.Protocol71:
				return new Q3BrowserHandler(protocol);
			}
			throw new JKClientException($"There isn't any known server browser handler for given protocol: {protocol}");
		}
	}
}
