using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JKClient {
	public unsafe delegate void NetFieldAdjust(int *value);
	public sealed class NetField {
		public int Offset { get; internal set; }
		public int Bits { get; internal set; }
		public NetFieldAdjust Adjust { get; internal set; }
		internal NetField() {}
		public NetField(int offset, int bits, NetFieldAdjust adjust = null) {
			this.Offset = offset;
			this.Bits = bits;
			this.Adjust = adjust;
		}
		public NetField(NetField field) {
			this.Offset = field.Offset;
			this.Bits = field.Bits;
			this.Adjust = field.Adjust;
		}
	}
	internal class NetFieldsArray : List<NetField> {
		private readonly Type netType;
		public NetFieldsArray(Type netType) {
			this.netType = netType;
		}
		public NetFieldsArray(NetFieldsArray fields) {
			this.netType = fields.netType;
			foreach (var field in fields) {
				this.Add(new NetField(field));
			}
		}
		public void Add(int offset, int bits, NetFieldAdjust adjust = null) {
			this.Add(new NetField() {
				Offset = offset,
				Bits = bits,
				Adjust = adjust
			});
		}
		public void Add(string fieldName, int extraOffset, int bits, NetFieldAdjust adjust = null) {
			this.Add(Marshal.OffsetOf(this.netType, fieldName).ToInt32() + extraOffset, bits, adjust);
		}
		public void Add(string fieldName, int bits, NetFieldAdjust adjust = null) {
			this.Add(fieldName, 0, bits, adjust);
		}
		public NetFieldsArray Override(int index, int bits) {
			this[index].Bits = bits;
			return this;
		}
	}
}
