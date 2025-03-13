﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {
	public sealed class ServerBrowser : NetClient {
		private const long RefreshTimeout = 3000L;
		private readonly List<ServerAddress> masterServers;
		private readonly ConcurrentDictionary<NetAddress, ServerInfo> globalServers;
		private TaskCompletionSource<IEnumerable<ServerInfo>> getListTCS, refreshListTCS;
		private long serverRefreshTimeout = 0L;
		private readonly ConcurrentDictionary<NetAddress, ServerInfoTask> serverInfoTasks;
		private readonly HashSet<NetAddress> serverInfoTasksToRemove;
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
			this.globalServers = new ConcurrentDictionary<NetAddress, ServerInfo>(new NetAddress.Comparer());
			this.serverInfoTasks = new ConcurrentDictionary<NetAddress, ServerInfoTask>(new NetAddress.Comparer());
			this.serverInfoTasksToRemove = new HashSet<NetAddress>(new NetAddress.Comparer());
		}
		private protected override void OnStop(bool afterFailure) {
			this.getListTCS?.TrySetCanceled();
			this.refreshListTCS?.TrySetCanceled();
			this.serverRefreshTimeout = 0L;
			base.OnStop(afterFailure);
		}
		private protected override async Task Run(CancellationToken cancellationToken) {
			const int frameTime = 8;
			while (true) {
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				this.GetPacket();
				this.HandleServersList();
				this.HandleServerInfoTasks();
				await Task.Delay(frameTime);
			}
		}
		private void HandleServersList() {
			if (this.serverRefreshTimeout != 0L && this.serverRefreshTimeout < Common.Milliseconds) {
				this.getListTCS?.TrySetResult(this.globalServers.Values);
				this.refreshListTCS?.TrySetResult(this.globalServers.Values);
				this.serverRefreshTimeout = 0L;
			}
		}
		private void HandleServerInfoTasks() {
			foreach (var serverInfoTask in this.serverInfoTasks) {
				if (serverInfoTask.Value.Timeout < Common.Milliseconds) {
					serverInfoTask.Value.TrySetCanceled();
					this.serverInfoTasksToRemove.Add(serverInfoTask.Key);
				}
			}
			foreach (var serverInfoTaskToRemove in this.serverInfoTasksToRemove) {
				this.serverInfoTasks.TryRemove(serverInfoTaskToRemove, out _);
			}
			this.serverInfoTasksToRemove.Clear();
		}
		public async Task<IEnumerable<ServerInfo>> GetNewList() {
			this.getListTCS?.TrySetCanceled();
			this.getListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			this.globalServers.Clear();
			this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			foreach (var masterServer in this.masterServers) {
				var address = await NetSystem.StringToAddressAsync(masterServer.Name, masterServer.Port);
				if (address == null) {
					continue;
				}
				this.OutOfBandPrint(address, $"getservers {this.Protocol} full empty");
			}
			return await this.getListTCS.Task;
		}
		public async Task<IEnumerable<ServerInfo>> RefreshList() {
			if (this.globalServers.Count <= 0) {
				return await this.GetNewList();
			}
			this.refreshListTCS?.TrySetCanceled();
			this.refreshListTCS = new TaskCompletionSource<IEnumerable<ServerInfo>>();
			this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			foreach (var server in this.globalServers) {
				var serverInfo = server.Value;
				serverInfo.InfoSet = false;
				serverInfo.Start = Common.Milliseconds;
				this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
			}
			return await this.refreshListTCS.Task;
		}
		public async Task<ServerInfo> GetServerInfo(NetAddress address) {
			if (this.serverInfoTasks.TryGetValue(address, out var serverInfoTask)) {
				serverInfoTask.TrySetCanceled();
			}
			var serverInfoTCS = this.serverInfoTasks[address] = new ServerInfoTask(address);
			this.OutOfBandPrint(address, "getinfo xxx");
			return await serverInfoTCS.Task;
		}
		public async Task<ServerInfo> GetServerInfo(string address, ushort port = 0) {
			var netAddress = await NetSystem.StringToAddressAsync(address, port);
			if (netAddress == null) {
				return null;
			}
			return await this.GetServerInfo(netAddress);
		}
		private protected override unsafe void PacketEvent(in NetAddress address, in Message msg) {
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					msg.BeginReading(true);
					msg.ReadLong();
					string s = msg.ReadStringLineAsString();
					var command = new Command(s);
					string c = command[0];
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
		private unsafe void ServersResponsePacket(in NetAddress address, in Message msg) {
			fixed (byte *b = msg.Data) {
				byte *buffptr = b;
				byte *buffend = buffptr + msg.CurSize;
				do {
					if (*buffptr == '\\') {
						break;
					}
					buffptr++;
				} while (buffptr < buffend);
				while (buffptr + 1 < buffend) {
					if (*buffptr != '\\') {
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
						Address = new NetAddress(ip, (ushort)port)
					};
					this.globalServers[serverInfo.Address] = serverInfo;
					this.OutOfBandPrint(serverInfo.Address, "getinfo xxx");
					if (*buffptr != '\\' && *buffptr != '/') {
						break;
					}
				}
			}
		}
		private void ServerStatusResponse(in NetAddress address, in Message msg) {
			var info = new InfoString(msg.ReadStringLineAsString());
			if ((this.serverInfoTasks.TryGetValue(address, out var serverInfoTask) && serverInfoTask.ServerInfo is var serverInfo) || this.globalServers.TryGetValue(address, out serverInfo)) {
				var players = new List<ServerInfo.PlayerInfo>();
				for (string s = msg.ReadStringLineAsString(); !string.IsNullOrEmpty(s); s = msg.ReadStringLineAsString()) {
					var command = new Command(s);
					var playerInfo = new ServerInfo.PlayerInfo(command);
					players.Add(playerInfo);
				}
				serverInfo.PlayersInfo = players.ToArray();
				serverInfo.Clients = players.Count(playerInfo => playerInfo.Ping > 0);
				serverInfo.SetConfigstringInfo(info);
				this.BrowserHandler.HandleStatusResponse(serverInfo, info);
				serverInfoTask?.SetCompleted();
				this.serverInfoTasks?.TryRemove(address, out _);
				this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			}
		}
		private void ServerInfoPacket(in NetAddress address, in Message msg) {
			var info = new InfoString(msg.ReadStringAsString());
			if ((this.serverInfoTasks.TryGetValue(address, out var serverInfoTask) && serverInfoTask.ServerInfo is var serverInfo) || this.globalServers.TryGetValue(address, out serverInfo)) {
				if (serverInfo.InfoSet) {
					return;
				}
				serverInfo.Ping = (int)(Common.Milliseconds - serverInfo.Start);
				serverInfo.SetInfo(info);
				this.BrowserHandler.HandleInfoPacket(serverInfo, info);
				this.OutOfBandPrint(serverInfo.Address, "getstatus");
				serverInfoTask?.ResetTimeout();
				this.serverRefreshTimeout = Common.Milliseconds + ServerBrowser.RefreshTimeout;
			}
		}
		private class ServerInfoTask : TaskCompletionSource<ServerInfo> {
			private const long CancelTimeout = 3000L;
			public long Timeout { get; private set; }
			public ServerInfo ServerInfo { get; private init; }
			public ServerInfoTask(NetAddress address) {
				this.ServerInfo = new ServerInfo(address);
				this.ResetTimeout();
			}
			public void ResetTimeout() => this.Timeout = Common.Milliseconds + ServerInfoTask.CancelTimeout;
			public void SetCompleted() => this.TrySetResult(this.ServerInfo);
		}
		public sealed class ServerAddress {
			public string Name { get; init; }
			public ushort Port { get; init; }
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
