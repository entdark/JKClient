using System;
using System.Runtime.InteropServices;

namespace JKClient {
	internal unsafe struct PinnedObject<T> : IDisposable where T : unmanaged {
		private GCHandle handle;
		public IntPtr Address => this.handle.AddrOfPinnedObject();
		public T *Pointer => (T*)this.Address;
		public T *this[int offset] => this.Pointer + offset;
		public PinnedObject() {
			throw new JKClientException("Cannot create a pinned object wrapper without an object");
		}
		public PinnedObject(ref T obj) {
			this.handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
		}
		public PinnedObject(T []obj) {
			this.handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
		}
		public void Dispose() {
			this.handle.Free();
		}
		public static implicit operator T*(PinnedObject<T> obj) {
			return obj.Pointer;
		}
		public static implicit operator IntPtr(PinnedObject<T> obj) {
			return obj.Address;
		}
	}
}