using System;
using System.Collections.Generic;

namespace JKClient {
	public sealed class NetAddress {
		public byte []IP { get; private set; }
		public ushort Port { get; private set; }
		public NetAddress(NetAddress address) {
			this.IP = new byte[address.IP.Length];
			Array.Copy(address.IP, this.IP, address.IP.Length);
			this.Port = address.Port;
		}
		public NetAddress(byte []ip, ushort port) {
			this.IP = ip;
			this.Port = port;
		}
		public static bool operator ==(NetAddress address1, NetAddress address2) {
			if (address1 is null && address2 is null)
				return true;
			if (address1 is null || address2 is null)
				return false;
			if (address1.Port != address2.Port)
				return false;
			if (address1.IP == null && address2.IP == null)
				return true;
			if (address1.IP == null || address2.IP == null)
				return false;
			if (address1.IP.Length != address2.IP.Length)
				return false;
			for (int i = 0; i < address1.IP.Length; i++) {
				if (address1.IP[i] != address2.IP[i])
					return false;
			}
			return true;
		}
		public static bool operator !=(NetAddress address1, NetAddress address2) {
			return (address1 == address2) != true;
		}
		public override bool Equals(object obj) {
			return base.Equals(obj);
		}
		public override int GetHashCode() {
			return (this.IP[0], this.IP[1], this.IP[2], this.IP[3], this.Port).GetHashCode();
		}
		public override string ToString() {
			string toString = string.Empty;
			for (int i = 0; i < this.IP.Length; i++) {
				if (i != 0) {
					toString += ".";
				}
				toString += this.IP[i].ToString();
			}
			toString += ":" + this.Port.ToString();
			return toString;
		}
		public static NetAddress FromString(string address) {
			return NetSystem.StringToAddress(address);
		}
	}
	public sealed class NetAddressComparer : EqualityComparer<NetAddress> {
		public override bool Equals(NetAddress x, NetAddress y) {
			return x == y;
		}
		public override int GetHashCode(NetAddress obj) {
			return obj.GetHashCode();
		}
	}
}
