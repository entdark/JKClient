using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JKClient {
	internal sealed class NetSystem : IDisposable {
		private const ushort PortServer = 29070;
		private Socket ipSocket;
		private IPEndPoint endPoint;
		private bool disposed = false;
		public NetSystem() {
			this.InitSocket();
		}
		private void InitSocket(bool reinit = false) {
			try {
				this.ipSocket?.Close();
			} catch {}
			this.ipSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) {
				Blocking = false,
				EnableBroadcast = true
			};
			this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
			int i;
			bool tryToReuse = false;
			if (reinit) {
				i = this.endPoint.Port - NetSystem.PortServer;
				if (i < 0) {
					i = 0;
					tryToReuse = false;
				} else {
					this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					tryToReuse = true;
				}
			} else {
				i = 0;
			}
			bool triedToReuse = false;
			for (; i < 256; i++) {
				try {
					this.endPoint = new IPEndPoint(IPAddress.Any, NetSystem.PortServer + i);
					if (tryToReuse && triedToReuse) {
						this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
						tryToReuse = false;
					}
					if (tryToReuse && !triedToReuse) {
						triedToReuse = true;
						i = -1;
					}
					this.ipSocket.Bind(this.endPoint);
				} catch (SocketException exception) {
					switch (exception.SocketErrorCode) {
					case SocketError.AddressAlreadyInUse:
//					case SocketError.AddressFamilyNotSupported:
						break;
					default:
						throw;
					}
					Debug.WriteLine(exception);
					continue;
				}
				break;
			}
			if (reinit) {
				this.ipSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
			}
		}
		public void SendPacket(int length, byte []data, NetAddress address) {
			if (this.disposed) {
				return;
			}
			lock (this.ipSocket) {
			try {
				this.ipSocket.SendTo(data, length, SocketFlags.None, address.ToIPEndPoint());
			} catch (SocketException exception) {
				switch (exception.SocketErrorCode) {
				case SocketError.WouldBlock:
					break;
				case SocketError.NotConnected:
				case SocketError.Shutdown:
					this.InitSocket(true);
					goto default;
				default:
					Debug.WriteLine("SocketException:");
					Debug.WriteLine(exception);
					break;
				}
			}}
		}
		public void SendPacket(Message msg, NetAddress address) {
			SendPacket(msg.CurSize, msg.Data, address);
		}
		public bool GetPacket(ref NetAddress address, Message msg) {
			if (this.disposed) {
				return false;
			}
			EndPoint endPoint = new IPEndPoint(0, 0);
			try {
				int ret = this.ipSocket.ReceiveFrom(msg.Data, msg.MaxSize, SocketFlags.None, ref endPoint);
				if (ret == msg.MaxSize) {
					return false;
				}
				var ipEndPoint = endPoint as IPEndPoint;
				address = ipEndPoint.ToNetAddress();
				msg.CurSize = ret;
				return true;
			} catch (SocketException exception) {
				switch (exception.SocketErrorCode) {
				case SocketError.WouldBlock:
				case SocketError.ConnectionReset:
					break;
				case SocketError.NotConnected:
				case SocketError.Shutdown:
					this.InitSocket(true);
					goto default;
				default:
					Debug.WriteLine("SocketException:");
					Debug.WriteLine(exception);
					break;
				}
				return false;
			}
		}
		public static async Task<NetAddress> StringToAddressAsync(string address, ushort port = 0) {
			byte []ip;
			int index = address.IndexOf(':');
			if (port <= 0) {
				port = index >= 0 && ushort.TryParse(address.Substring(index+1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort p) ? p : NetSystem.PortServer;
			}
			if (index < 0) {
				index = address.Length;
			}
			ip = IPAddress.TryParse(address.Substring(0, index), out IPAddress ipAddress) ? ipAddress.GetAddressBytes() : null;
			if (ip == null) {
				try {
					var hostEntry = await Dns.GetHostEntryAsync(address);
					ip = hostEntry.AddressList.FirstOrDefault(adr => adr.AddressFamily == AddressFamily.InterNetwork)?.GetAddressBytes();
				} catch (SocketException exception) {
					if (exception.SocketErrorCode == SocketError.HostNotFound) {
						return null;
					} else {
						throw;
					}
				}
			}
			return new NetAddress(ip, port);
		}
		public static NetAddress StringToAddress(string address, ushort port = 0) {
			return NetSystem.StringToAddressAsync(address, port).Result;
		}
		public void Dispose() {
			this.disposed = true;
			this.ipSocket?.Close(5);
		}
	}
	internal static class NetSystemExtensions {
		public static IPEndPoint ToIPEndPoint(this NetAddress address) {
			return new IPEndPoint(new IPAddress(address.IP), address.Port);
		}
		public static NetAddress ToNetAddress(this IPEndPoint ipEndPoint) {
			return new NetAddress(ipEndPoint.Address.GetAddressBytes(), (ushort)ipEndPoint.Port);
		}
	}
}
