using System;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {
	public abstract class NetClient : IDisposable {
		private protected readonly NetSystem net;
		private CancellationTokenSource cts;
		private byte []packetReceived = new byte[Message.MaxLength];
		public bool Started { get; private set; }
		internal NetClient() {
			this.net = new NetSystem();
		}
		public void Start(Func<JKClientException, Task> exceptionCallback) {
			if (this.Started) {
				return;
//				throw new JKClientException("NetClient is already started");
			}
			this.Started = true;
			this.cts = new CancellationTokenSource();
			Task.Run(this.Run, this.cts.Token)
				.ContinueWith((t) => {
					exceptionCallback?.Invoke(new JKClientException(t.Exception));
				}, TaskContinuationOptions.OnlyOnFaulted);
		}
		public void Stop() {
			if (!this.Started) {
				return;
//				throw new JKClientException("Cannot stop NetClient when it's not started");
			}
			this.Started = false;
			if (this.cts != null) {
				this.cts.Cancel();
				this.cts = null;
			}
		}
		private protected void GetPacket() {
			var netmsg = new Message(packetReceived, sizeof(byte)*Message.MaxLength);
			NetAddress address = null;
			while (this.net.GetPacket(ref address, netmsg)) {
				if ((uint)netmsg.CurSize <= netmsg.MaxSize) {
					this.PacketEvent(address, netmsg);
				}
				Common.MemSet(netmsg.Data, 0, sizeof(byte)*netmsg.MaxSize);
			}
		}
		internal void OutOfBandPrint(NetAddress address, string data) {
			byte []msg = new byte[Message.MaxLength];
			msg[0] = unchecked((byte)-1);
			msg[1] = unchecked((byte)-1);
			msg[2] = unchecked((byte)-1);
			msg[3] = unchecked((byte)-1);
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, 4);
			this.net.SendPacket(dataMsg.Length+4, msg, address);
		}
		internal void OutOfBandData(NetAddress address, string data, int length) {
			byte []msg = new byte[Message.MaxLength*2];
			msg[0] = 0xff;
			msg[1] = 0xff;
			msg[2] = 0xff;
			msg[3] = 0xff;
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, 4);
			var mbuf = new Message(msg, msg.Length) {
				CurSize = length+4
			};
			Huffman.Compress(mbuf, 12);
			this.net.SendPacket(mbuf.CurSize, mbuf.Data, address);
		}
		private protected abstract void PacketEvent(NetAddress address, Message msg);
		private protected abstract Task Run();
		public void Dispose() {
			this.net?.Dispose();
		}
	}
}
