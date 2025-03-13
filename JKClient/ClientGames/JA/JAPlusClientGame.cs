namespace JKClient {
	public class JAPlusClientGame : JAClientGame {
		public JAPlusClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum)
			: base(client, serverMessageNum, serverCommandSequence, clientNum) {
			client.SetUserInfoKeyValue("cjp_client", "1.4B4");
		}
		protected override void ParseScores(Command command) {
			int numScores = command[1].Atoi();
			if (numScores > MaxClientScoreSend)
				numScores = MaxClientScoreSend;
			if (numScores > this.Client.MaxClients)
				numScores = this.Client.MaxClients;
			for (int i = 0; i < numScores; i++) {
				int clientNum = command[i*15+4].Atoi();
				if (clientNum < 0 || clientNum > this.Client.MaxClients)
					continue;
				int score = command[i*15+5].Atoi();
				int ping = command[i*15+6].Atoi();
				int deaths = command[i*15+18].Atoi();
				this.ClientsInfo[clientNum].Score = score;
				this.ClientsInfo[clientNum].Ping = ping;
				this.ClientsInfo[clientNum].ModData = deaths;
			}
			this.NeedNotifyClientInfoChanged = true;
		}
	}
}
