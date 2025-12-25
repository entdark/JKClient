using System;
using System.Threading;
using System.Threading.Tasks;

namespace JKClient {
	public abstract class NetClient : IDisposable {
		private protected readonly NetSystem net;
		private readonly byte []packetReceived;
		private CancellationTokenSource cts;
		private protected readonly INetHandler NetHandler;
		public bool Started { get; private set; }
		public int Protocol => this.NetHandler.Protocol;
		internal NetClient(INetHandler netHandler) {
			if (netHandler == null) {
				throw new JKClientException(new ArgumentNullException(nameof(netHandler)));
			}
			this.net = new NetSystem();
			this.NetHandler = netHandler;
			this.packetReceived = new byte[this.NetHandler.MaxMessageLength];
		}
		public void Start(Action<JKClientException> exceptionCallback, bool restartOnFailure = false) {
			if (this.Started) {
				return;
//				throw new JKClientException("NetClient is already started");
			}
			this.Started = true;
			this.OnStart();
			this.cts = new CancellationTokenSource();
			Task.Factory.StartNew(async () => await this.Run(this.cts.Token), TaskCreationOptions.LongRunning)
				.ContinueWith((t) => {
					this.Stop(true);
					if (restartOnFailure) {
						this.Start(exceptionCallback);
					}
					exceptionCallback?.Invoke(new JKClientException(t.Exception));
				}, TaskContinuationOptions.OnlyOnFaulted);
		}
		public void Stop(bool afterFailure = false) {
			if (!this.Started) {
				return;
//				throw new JKClientException("Cannot stop NetClient when it's not started");
			}
			this.Started = false;
			this.OnStop(afterFailure);
			this.cts.Cancel();
		}
		private protected void GetPacket() {
			var message = new Message(this.packetReceived, sizeof(byte)*this.NetHandler.MaxMessageLength);
			NetAddress address = null;
			while (this.net.GetPacket(ref address, message)) {
				if ((uint)message.CurSize <= message.MaxSize) {
					this.PacketEvent(address, message);
				}
				message.Reset();
			}
		}
		internal void OutOfBandPrint(in NetAddress address, in string data) {
			byte []msg = new byte[this.NetHandler.MaxMessageLength];
			msg[0] = unchecked((byte)-1);
			msg[1] = unchecked((byte)-1);
			msg[2] = unchecked((byte)-1);
			msg[3] = unchecked((byte)-1);
			byte []dataMsg = Common.Encoding.GetBytes(data);
			dataMsg.CopyTo(msg, 4);
			this.net.SendPacket(dataMsg.Length+4, msg, address);
		}
		internal void OutOfBandData(in NetAddress address, in string data, in int length) {
			byte []msg = new byte[this.NetHandler.MaxMessageLength*2];
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
			this.net.SendPacket(mbuf, address);
		}
		private protected abstract void PacketEvent(in NetAddress address, in Message msg);
		private protected abstract Task Run(CancellationToken cancellationToken);
		private protected virtual void OnStart() {}
		private protected virtual void OnStop(bool afterFailure) {}
		public void Dispose() {
			this.Stop();
			this.net?.Dispose();
		}
	}
}
