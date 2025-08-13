﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {
	public sealed partial class JKClient : NetClient {
		private const int FrameTime = 8;
		private const int LastPacketTimeOut = 5 * 60000;
		private const int RetransmitTimeOut = 3000;
		private const int MaxPacketUserCmds = 32;
		private const string DefaultName = "AssetslessClient";
		private readonly Random random = new();
		private readonly int port;
		private readonly InfoString userInfo = new() {
			["name"] = JKClient.DefaultName,
			["rate"] = "25000",
			["snaps"] = "40",
			["model"] = "kyle/default",
			["forcepowers"] = "7-1-032330000000001333",
			["color1"] = "4",
			["color2"] = "4",
			["handicap"] = "100",
			["teamtask"] = "0",
			["sex"] = "male",
			["password"] = "",
			["cg_predictItems"] = "1",
			["saber1"] = "single_1",
			["saber2"] = "none",
			["char_color_red"] = "255",
			["char_color_green"] = "255",
			["char_color_blue"] = "255",
			["engine"] = "jkclient",
			["assets"] = "0"
		};
		private readonly ConcurrentQueue<Action> actionsQueue = new();
		private ClientGame clientGame;
		private TaskCompletionSource<bool> connectTCS;
#region ClientConnection
		private int clientNum = 0;
		private int lastPacketSentTime = 0;
		private int lastPacketTime = 0;
		private NetAddress serverAddress;
		private int connectTime = -9999;
		private int connectPacketCount = 0;
		private int challenge = 0;
		private int checksumFeed = 0;
		private int reliableSequence = 0;
		private int reliableAcknowledge = 0;
		private sbyte [][]reliableCommands;
		private int serverMessageSequence = 0;
		private int serverCommandSequence = 0;
		private int lastExecutedServerCommand = 0;
		private sbyte [][]serverCommands;
		private NetChannel netChannel;
#endregion
#region ClientStatic
		private int realTime = 0;
		private string servername;
		public ConnectionStatus Status { get; private set; }
#endregion
		private IClientHandler ClientHandler => this.NetHandler as IClientHandler;
		public ClientVersion Version => this.ClientHandler.Version;
		private int MaxReliableCommands => this.ClientHandler.MaxReliableCommands;
		private string GuidKey => this.ClientHandler.GuidKey;
		public string Name {
			get => this.userInfo["name"];
			set {
				string name = value;
				if (string.IsNullOrEmpty(name)) {
					name = JKClient.DefaultName;
				} else if (name.Length > 31) {
					name = name.Substring(0, 31);
				}
				this.userInfo["name"] = name;
				this.UpdateUserInfo();
			}
		}
		public string Password {
			get => this.userInfo["password"];
			set {
				this.userInfo["password"] = value;
				this.UpdateUserInfo();
			}
		}
		public Guid Guid {
			get => Guid.TryParse(this.userInfo[this.GuidKey], out Guid guid) ? guid : Guid.Empty;
			set {
				this.userInfo[this.GuidKey] = value.ToString();
				this.UpdateUserInfo();
			}
		}
		public ClientGame ClientGame => this.clientGame;
		private readonly ServerInfo serverInfo = new();
		public ServerInfo ServerInfo {
			get {
				string serverInfoCSStr = this.GetConfigstring(GameState.ServerInfo);
				var info = new InfoString(serverInfoCSStr);
				this.serverInfo.Address = this.serverAddress;
				if (this.clientGame?.ClientsInfo is {} clientsInfo) {
					var clientInfoRange = Enumerable.Range(0, this.clientGame.ClientsInfo.Length);
					this.serverInfo.PlayersInfo = clientInfoRange.Where(i => this.clientGame.ClientsInfo[i].InfoValid).Select(i => this.clientGame.ClientsInfo[i]).ToArray();
					this.serverInfo.Clients = this.serverInfo.PlayersInfo.Length;
				} else {
					this.serverInfo.PlayersInfo = null;
					this.serverInfo.Clients = 0;
				}
				this.serverInfo.SetConfigstringInfo(info);
				this.ClientHandler.SetExtraConfigstringInfo(this.serverInfo, info);
				return this.serverInfo;
			}
		}
		public GameModification Modification => this.ClientHandler.Modification;
		public event Action<ServerInfo> ServerInfoChanged;
		public event Action<long> FrameExecuted;
		public JKClient(IClientHandler clientHandler) : base(clientHandler) {
			this.Status = ConnectionStatus.Disconnected;
			this.port = this.random.Next(1, 0xffff) & 0xffff;
			this.reliableCommands = new sbyte[this.MaxReliableCommands][];
			this.serverCommands = new sbyte[this.MaxReliableCommands][];
			for (int i = 0; i < this.MaxReliableCommands; i++) {
				this.serverCommands[i] = new sbyte[Common.MaxStringChars];
				this.reliableCommands[i] = new sbyte[Common.MaxStringChars];
			}
		}
		private protected override void OnStart() {
			//don't start with any pending actions
			this.DequeueActions(false);
			base.OnStart();
		}
		private protected override void OnStop(bool afterFailure) {
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			this.Status = ConnectionStatus.Disconnected;
			if (afterFailure) {
				this.DequeueActions();
				this.ClearState();
				this.ClearConnection();
			}
			base.OnStop(afterFailure);
		}
		private protected override async Task Run(CancellationToken cancellationToken) {
			long frameTime, lastTime = Common.Milliseconds;
			int msec;
			this.realTime = 0;
			while (true) {
				if (cancellationToken.IsCancellationRequested) {
					break;
				}
				if (this.realTime - this.lastPacketTime > JKClient.LastPacketTimeOut && this.Status == ConnectionStatus.Active) {
					var cmd = new Command(new string []{ "disconnect", "Last packet from server was too long ago" });
					this.Disconnect();
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd));
				}
				this.GetPacket();
				frameTime = Common.Milliseconds;
				msec = (int)(frameTime - lastTime);
				if (msec > 5000) {
					msec = 5000;
				}
				this.DequeueActions();
				lastTime = frameTime;
				this.realTime += msec;
				this.SendCommand();
				this.CheckForResend();
				this.SetTime();
				if (this.Status >= ConnectionStatus.Primed) {
					bool processedSnapshots = this.clientGame.Frame(this.serverTime);
					if (processedSnapshots) {
						this.FrameExecuted?.Invoke(frameTime);
					}
				}
				await Task.Delay(FrameTime);
			}
			//complete all actions after stop
			this.DequeueActions();
		}
		private void DequeueActions(bool invoke = true) {
#if NETSTANDARD2_1
			if (!invoke) {
				this.actionsQueue.Clear();
				return;
			}
#endif
			while (this.actionsQueue.TryDequeue(out var action)) {
				if (invoke) {
					action?.Invoke();
				}
			}
		}
		public void SetUserInfoKeyValue(string key, string value) {
			if (string.Compare(key, "name", StringComparison.OrdinalIgnoreCase) == 0) {
				this.Name = value;
			} else if (string.Compare(key, "password", StringComparison.OrdinalIgnoreCase) == 0) {
				this.Password = value;
			} else if (string.Compare(key, this.GuidKey, StringComparison.OrdinalIgnoreCase) == 0) {
				this.Guid = Guid.TryParse(value, out Guid guid) ? guid : Guid.Empty;
			} else {
				this.userInfo[key] = value;
				this.UpdateUserInfo();
			}
		}
		private void UpdateUserInfo() {
			if (this.Status < ConnectionStatus.Challenging) {
				return;
			}
			this.ExecuteCommand($"userinfo \"{this.userInfo}\"");
		}
		private void CheckForResend() {
			if (this.Status != ConnectionStatus.Connecting && this.Status != ConnectionStatus.Challenging) {
				return;
			}
			if (this.realTime - this.connectTime < JKClient.RetransmitTimeOut) {
				return;
			}
			this.connectTime = this.realTime;
			this.connectPacketCount++;
			switch (this.Status) {
			case ConnectionStatus.Connecting:
				this.ClientHandler.RequestAuthorization((address, data2) => {
					this.OutOfBandPrint(address, data2);
				});
				this.OutOfBandPrint(this.serverAddress, $"getchallenge {this.challenge}");
				break;
			case ConnectionStatus.Challenging:
				string data = $"connect \"{this.userInfo}\\protocol\\{this.Protocol}\\qport\\{this.port}\\challenge\\{this.challenge}\"";
				this.OutOfBandData(this.serverAddress, data, data.Length);
				break;
			}
		}
		private unsafe void Encode(in Message msg) {
			if (msg.CurSize <= 12) {
				return;
			}
			msg.SaveState();
			msg.BeginReading();
			int serverId = msg.ReadLong();
			int messageAcknowledge = msg.ReadLong();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.serverCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ serverId ^ messageAcknowledge);
					for (int i = 12; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == '%') {
							key ^= (byte)('.' << (i & 1));
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
		}
		private unsafe void Decode(in Message msg) {
			msg.SaveState();
			msg.Bitstream();
			int reliableAcknowledge = msg.ReadLong();
			msg.RestoreState();
			fixed (sbyte *b = this.reliableCommands[reliableAcknowledge & (this.MaxReliableCommands-1)]) {
				fixed (byte *d = msg.Data) {
					byte *str = (byte*)b;
					int index = 0;
					byte key = (byte)(this.challenge ^ *(uint*)d);
					for (int i = msg.ReadCount + 4; i < msg.CurSize; i++) {
						if (str[index] == 0)
							index = 0;
						if ((!this.ClientHandler.FullByteEncoding && str[index] > 127) || str[index] == '%') {
							key ^= (byte)('.' << (i & 1));
						} else {
							key ^= (byte)(str[index] << (i & 1));
						}
						index++;
						*(d + i) = (byte)(*(d + i) ^ key);
					}
				}
			}
		}
		private protected override unsafe void PacketEvent(in NetAddress address, in Message msg) {
//			this.lastPacketTime = this.realTime;
			fixed (byte *b = msg.Data) {
				if (msg.CurSize >= 4 && *(int*)b == -1) {
					this.ConnectionlessPacket(address, msg);
					return;
				}
				if (this.Status < ConnectionStatus.Connected) {
					return;
				}
				if (msg.CurSize < 4) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				if (!this.netChannel.Process(msg)) {
					return;
				}
				this.Decode(msg);
				this.serverMessageSequence = *(int*)b;
				this.lastPacketTime = this.realTime;
				this.ParseServerMessage(msg);
			}
		}
		private void ConnectionlessPacket(NetAddress address, Message msg) {
			msg.BeginReading(true);
			msg.ReadLong();
			string s = msg.ReadStringLineAsString();
			var command = new Command(s);
			string c = command[0];
			if (string.Compare(c, "challengeResponse", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Connecting) {
					return;
				}
				c = command[2];
				if (address != this.serverAddress) {
					if (string.IsNullOrEmpty(c) || c.Atoi() != this.challenge)
						return;
				}
				this.challenge = command[1].Atoi();
				this.Status = ConnectionStatus.Challenging;
				this.connectPacketCount = 0;
				this.connectTime = -99999;
				this.serverAddress = address;
			} else if (string.Compare(c, "connectResponse", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.Status != ConnectionStatus.Challenging) {
					return;
				}
				if (address != this.serverAddress) {
					return;
				}
				this.netChannel = new NetChannel(this.net, address, this.port, this.ClientHandler.MaxMessageLength);
				this.Status = ConnectionStatus.Connected;
				this.lastPacketSentTime = -9999;
			} else if (string.Compare(c, "disconnect", StringComparison.OrdinalIgnoreCase) == 0) {
				if (this.netChannel == null) {
					return;
				}
				if (address != this.netChannel.Address) {
					return;
				}
				if (this.realTime - this.lastPacketTime < 3000) {
					return;
				}
				this.ServerCommandExecuted?.Invoke(new CommandEventArgs(command));
				this.Disconnect();
			} else if (string.Compare(c, "echo", StringComparison.OrdinalIgnoreCase) == 0) {
				this.OutOfBandPrint(address, command[1]);
			} else if (string.Compare(c, "print", StringComparison.OrdinalIgnoreCase) == 0) {
				if (address == this.serverAddress) {
					s = msg.ReadStringAsString();
					var cmd = new Command(new string []{ "print", s });
					this.ServerCommandExecuted?.Invoke(new CommandEventArgs(cmd));
					Debug.WriteLine(s);
				}
			} else {
				Debug.WriteLine(c);
			}
		}
		private void CreateNewCommand() {
			if (this.Status < ConnectionStatus.Primed) {
				return;
			}
			this.cmdNumber++;
			this.cmds[this.cmdNumber & UserCommand.Mask].ServerTime = this.serverTime;
		}
		private void SendCommand() {
			if (this.Status < ConnectionStatus.Connected) {
				return;
			}
			this.CreateNewCommand();
			int oldPacketNum = (this.netChannel.OutgoingSequence - 1) & JKClient.PacketMask;
			int delta = this.realTime - this.outPackets[oldPacketNum].RealTime;
			if (delta < 10) {
				return;
			}
			this.WritePacket();
		}
		private void WritePacket() {
			if (this.netChannel == null) {
				return;
			}
			lock (this.netChannel) {
				var oldcmd = new UserCommand();
				byte []data = new byte[this.ClientHandler.MaxMessageLength];
				var msg = new Message(data, sizeof(byte)*this.ClientHandler.MaxMessageLength);
				msg.Bitstream();
				msg.WriteLong(this.serverId);
				msg.WriteLong(this.serverMessageSequence);
				msg.WriteLong(this.serverCommandSequence);
				for (int i = this.reliableAcknowledge + 1; i <= this.reliableSequence; i++) {
					msg.WriteByte((int)ClientCommandOperations.ClientCommand);
					msg.WriteLong(i);
					msg.WriteString(this.reliableCommands[i & (this.MaxReliableCommands-1)]);
				}
				int oldPacketNum = (this.netChannel.OutgoingSequence - 1 - 1) & JKClient.PacketMask;
				int count = this.cmdNumber - this.outPackets[oldPacketNum].CommandNumber;
				if (count > JKClient.MaxPacketUserCmds) {
					count = JKClient.MaxPacketUserCmds;
				}
				if (count >= 1) {
					if (!this.snap.Valid || this.serverMessageSequence != this.snap.MessageNum) {
						msg.WriteByte((int)ClientCommandOperations.MoveNoDelta);
					} else {
						msg.WriteByte((int)ClientCommandOperations.Move);
					}
					msg.WriteByte(count);
					int key = this.checksumFeed;
					key ^= this.serverMessageSequence;
					key ^= Common.HashKey(this.serverCommands[this.serverCommandSequence & (this.MaxReliableCommands-1)], 32);
					for (int i = 0; i < count; i++) {
						int j = (this.cmdNumber - count + i + 1) & UserCommand.Mask;
						msg.WriteDeltaUsercmdKey(key, ref oldcmd, ref this.cmds[j]);
						oldcmd = this.cmds[j];
					}
				}
				int packetNum = this.netChannel.OutgoingSequence & JKClient.PacketMask;
				this.outPackets[packetNum].RealTime = this.realTime;
				this.outPackets[packetNum].ServerTime = oldcmd.ServerTime;
				this.outPackets[packetNum].CommandNumber = this.cmdNumber;
				msg.WriteByte((int)ClientCommandOperations.EOF);
				this.Encode(msg);
				this.netChannel.Transmit(msg.CurSize, msg.Data);
				while (this.netChannel.UnsentFragments) {
					this.netChannel.TransmitNextFragment();
				}
			}
		}
		private unsafe void AddReliableCommand(string cmd, bool disconnect = false) {
			int unacknowledged = this.reliableSequence - this.reliableAcknowledge;
			fixed (sbyte *reliableCommand = this.reliableCommands[++this.reliableSequence & (this.MaxReliableCommands-1)]) {
				var encoding = Common.Encoding;
				Marshal.Copy(encoding.GetBytes(cmd+'\0'), 0, (IntPtr)(reliableCommand), encoding.GetByteCount(cmd)+1);
			}
		}
		void IJKClientImport.SendClientCommand(string cmd) {
			this.ExecuteCommand(cmd);
		}
		public void ExecuteCommand(string cmd) {
			void executeCommand() {
				if (cmd.StartsWith("rcon ", StringComparison.OrdinalIgnoreCase)) {
					this.ExecuteCommandDirectly(cmd);
				} else {
					this.AddReliableCommand(cmd);
				}
			}
			this.actionsQueue.Enqueue(executeCommand);
		}
		private void ExecuteCommandDirectly(string cmd) {
			this.OutOfBandPrint(this.serverAddress, cmd);
		}
		public Task Connect(in ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return this.Connect(serverInfo.Address.ToString());
		}
		public async Task Connect(string address) {
			this.connectTCS?.TrySetCanceled();
			this.connectTCS = null;
			var serverAddress = await NetSystem.StringToAddressAsync(address);
			if (serverAddress == null) {
				throw new JKClientException("Bad server address");
			}
			this.connectTCS = new TaskCompletionSource<bool>();
			void connect() {
				this.servername = address;
				this.serverAddress = serverAddress;
				this.challenge = ((this.random.Next() << 16) ^ this.random.Next()) ^ (int)Common.Milliseconds;
				this.connectTime = -9999;
				this.connectPacketCount = 0;
				this.Status = ConnectionStatus.Connecting;
			}
			this.actionsQueue.Enqueue(connect);
			await this.connectTCS.Task;
		}
		public void Disconnect() {
			var status = this.Status;
			this.Status = ConnectionStatus.Disconnected;
			void disconnect() {
				this.connectTCS?.TrySetCanceled();
				this.connectTCS = null;
				if (status >= ConnectionStatus.Connected) {
					this.AddReliableCommand("disconnect", true);
					this.WritePacket();
					this.WritePacket();
					this.WritePacket();
				}
				this.ClearState();
				this.ClearConnection();
			}
			this.actionsQueue.Enqueue(disconnect);
		}
//TODO: base on this.realTime changes to be more precise
		public void WaitFrames(int count = 1) {
			Task.Delay(FrameTime*count).Wait(FrameTime*count);
		}
		public static IClientHandler GetKnownClientHandler(in ServerInfo serverInfo) {
			if (serverInfo == null) {
				throw new JKClientException(new ArgumentNullException(nameof(serverInfo)));
			}
			return JKClient.GetKnownClientHandler(serverInfo.Protocol, serverInfo.Version);
		}
		public static IClientHandler GetKnownClientHandler(in ProtocolVersion protocol, in ClientVersion version) {
			switch (protocol) {
			case ProtocolVersion.Protocol25:
			case ProtocolVersion.Protocol26:
				return new JAClientHandler(protocol, version);
			case ProtocolVersion.Protocol15:
			case ProtocolVersion.Protocol16:
				return new JOClientHandler(protocol, version);
			case ProtocolVersion.Protocol68:
			case ProtocolVersion.Protocol71:
				return new Q3ClientHandler(protocol);
			}
			throw new JKClientException($"There isn't any known client handler for given protocol: {protocol}");
		}
	}
}
