namespace JKClient {
	public struct ClientInfo {
		public int ClientNum;
		public bool InfoValid;
		public string Name;
		public int Score;
		public int Ping;
		internal Team Team;
		internal void Clear() {
			this.ClientNum = 0;
			this.InfoValid = false;
			this.Name = null;
			this.Team = Team.Free;
		}
	}
}
