namespace JKClient {
	public struct ClientInfo {
		public int ClientNum;
		public bool InfoValid;
		public string Name;
		public int Score;
		public int Ping;
		public object ModData;
		public Team Team;
		internal void Clear() {
			this.ClientNum = 0;
			this.InfoValid = false;
			this.Name = null;
			this.Team = Team.Free;
		}
		internal ClientInfo(Command command) {
			this.ClientNum = -1;
			this.Name = command[2];
			this.Score = command[0].Atoi();
			this.Ping = command[1].Atoi();
		}
	}
}
