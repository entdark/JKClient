using System;

namespace JKClient {
	internal sealed class NetChannel {
		private const int MaxPacketLen = 1400;
		private const int FragmentSize = NetChannel.MaxPacketLen - 100;
		private const int FragmentBit = 1<<31;
		private readonly NetSystem net;
		private readonly int qport;
		private int dropped = 0;
		private int incomingSequence = 0;
		private int fragmentSequence = 0;
		private int fragmentLength = 0;
		private byte []fragmentBuffer = new byte[Message.MaxLength];
		private int unsentFragmentStart = 0;
		private int unsentLength = 0;
		private byte []unsentBuffer = new byte[Message.MaxLength];
		public int OutgoingSequence { get; private set; } = 1;
		public bool UnsentFragments { get; private set; } = false;
		public NetAddress Address { get; private set; }
		public NetChannel(NetSystem net, NetAddress address, int qport) {
			this.net = net;
			this.Address = address;
			this.qport = qport;
		}
		public unsafe bool Process(Message msg) {
			msg.BeginReading(true);
			int sequence = msg.ReadLong();
			msg.SaveState();
			bool fragmented;
			if ((sequence & NetChannel.FragmentBit) != 0) {
				sequence &= ~NetChannel.FragmentBit;
				fragmented = true;
			} else {
				fragmented = false;
			}
			int fragmentStart, fragmentLength;
			if (fragmented) {
				fragmentStart = (ushort)msg.ReadShort();
				fragmentLength = (ushort)msg.ReadShort();
			} else {
				fragmentStart = 0;
				fragmentLength = 0;
			}
			if (sequence <= this.incomingSequence) {
				return false;
			}
			this.dropped = sequence - (this.incomingSequence+1);
			if (fragmented) {
				if (sequence != this.fragmentSequence) {
					this.fragmentSequence = sequence;
					this.fragmentLength = 0;
				}
				if (fragmentStart != this.fragmentLength) {
					return false;
				}
				if (fragmentLength < 0 || (msg.ReadCount + fragmentLength) > msg.CurSize ||
					(this.fragmentLength + fragmentLength) > sizeof(byte)*Message.MaxLength) {
					return false;
				}
				Array.Copy(msg.Data, msg.ReadCount, this.fragmentBuffer, this.fragmentLength, fragmentLength);
				this.fragmentLength += fragmentLength;
				if (fragmentLength == NetChannel.FragmentSize) {
					return false;
				}
				if (this.fragmentLength+4 > msg.MaxSize) {
					return false;
				}
				fixed (byte* b = msg.Data) {
					*(int*)b = sequence;
				}
				Array.Copy(this.fragmentBuffer, 0, msg.Data, 4, this.fragmentLength);
				msg.CurSize = this.fragmentLength + 4;
				this.fragmentLength = 0;
				msg.RestoreState();
				return true;
			}
			this.incomingSequence = sequence;
			return true;
		}
		public void Transmit(int length, byte []data) {
			if (length > Message.MaxLength) {
				throw new JKClientException($"Transmit: length = {length}");
			}
			this.unsentFragmentStart = 0;
			if (length >= NetChannel.FragmentSize) {
				this.UnsentFragments = true;
				this.unsentLength = length;
				Array.Copy(data, 0, this.unsentBuffer, 0, length);
				this.TransmitNextFragment();
				return;
			}
			byte []buf = new byte[NetChannel.MaxPacketLen];
			var msg = new Message(buf, sizeof(byte)*NetChannel.MaxPacketLen, true);
			msg.WriteLong(this.OutgoingSequence);
			this.OutgoingSequence++;
			msg.WriteShort(this.qport);
			msg.WriteData(data, length);
			this.net.SendPacket(msg.CurSize, msg.Data, this.Address);
		}
		public unsafe void TransmitNextFragment() {
			byte []buf = new byte[NetChannel.MaxPacketLen];
			var msg = new Message(buf, sizeof(byte)*NetChannel.MaxPacketLen, true);
			msg.WriteLong(this.OutgoingSequence | NetChannel.FragmentBit);
			msg.WriteShort(this.qport);
			int fragmentLength = NetChannel.FragmentSize;
			if (this.unsentFragmentStart + fragmentLength > this.unsentLength) {
				fragmentLength = this.unsentLength - this.unsentFragmentStart;
			}
			msg.WriteShort(this.unsentFragmentStart);
			msg.WriteShort(fragmentLength);
			fixed (byte *b = this.unsentBuffer) {
				msg.WriteData(b + this.unsentFragmentStart, fragmentLength);
			}
			this.net.SendPacket(msg.CurSize, msg.Data, this.Address);
			this.unsentFragmentStart += fragmentLength;
			if (this.unsentFragmentStart == this.unsentLength && fragmentLength != NetChannel.FragmentSize) {
				this.OutgoingSequence++;
				this.UnsentFragments = false;
			}
		}
	}
}
