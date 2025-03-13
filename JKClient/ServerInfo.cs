﻿using System.Collections.Generic;

namespace JKClient {
//TODO: remake to struct?
	public sealed class ServerInfo {
		public NetAddress Address { get; internal set; }
		public string HostName;
		public string MapName;
		public string GameFolder;
		public string GameName;
		public GameType GameType;
		public int Clients;
		public int MaxClients;
		public int MinPing;
		public int MaxPing;
		public int Ping;
		public bool Visibile;
		public bool NeedPassword;
		public bool TrueJedi;
		public bool WeaponDisable;
		public bool ForceDisable;
		public ProtocolVersion Protocol;
		public ClientVersion Version;
		public bool Pure;
		public InfoString RawInfo { get; private set; }
		public ClientInfo []PlayersInfo { get; internal set;}
		internal bool InfoSet;
		internal long Start = Common.Milliseconds;
		public string this[string key] {
			get => this.RawInfo?[key] ?? string.Empty;
		}
		public ServerInfo() {}
		public ServerInfo(in NetAddress address) {
			this.Address = address;
		}
		public ServerInfo(in InfoString info) {
			this.SetInfo(info);
		}
		internal void SetInfo(in InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			this.Clients = info["clients"].Atoi();
			this.HostName = info["hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.GameFolder = info["game"];
			this.GameName = info["gamename"];
			this.MinPing = info["minPing"].Atoi();
			this.MaxPing = info["maxPing"].Atoi();
			this.InfoSet = true;
			this.RawInfo = info;
		}
		internal void SetConfigstringInfo(in InfoString info) {
			if (info.Count <= 0) {
				return;
			}
			this.Protocol = (ProtocolVersion)info["protocol"].Atoi();
			this.HostName = info["sv_hostname"];
			this.MapName = info["mapname"];
			this.MaxClients = info["sv_maxclients"].Atoi();
			this.GameName = info["gamename"];
			this.MinPing = info["sv_minping"].Atoi();
			this.MaxPing = info["sv_maxping"].Atoi();
			this.InfoSet = true;
			this.RawInfo = info;
		}
		public static bool operator ==(in ServerInfo serverInfo1, in ServerInfo serverInfo2) {
			return serverInfo1?.Address == serverInfo2?.Address;
		}
		public static bool operator !=(in ServerInfo serverInfo1, in ServerInfo serverInfo2) {
			return (serverInfo1 == serverInfo2) != true;
		}
		public override bool Equals(object obj) {
			return base.Equals(obj);
		}
		public override int GetHashCode() {
			return this.Address.GetHashCode();
		}
	}
	public sealed class ServerInfoComparer : EqualityComparer<ServerInfo> {
		public override bool Equals(ServerInfo x, ServerInfo y) {
			return x?.Address == y?.Address;
		}
		public override int GetHashCode(ServerInfo obj) {
			return obj?.Address.GetHashCode() ?? 0;
		}
	}
}
