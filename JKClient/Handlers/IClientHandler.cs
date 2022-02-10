namespace JKClient {
	public interface IClientHandler : INetHandler {
		ClientVersion Version { get; }
		int MaxReliableCommands { get; }
		int MaxConfigstrings { get; }
		int MaxClients { get; }
		bool CanParseRMG { get; }
		bool CanParseVehicle { get; }
		string GuidKey { get; }
		bool RequiresAuthorization { get; }
		bool FullByteEncoding { get; }
		void AdjustServerCommandOperations(ref ServerCommandOperations cmd);
		void AdjustGameStateConfigstring(int i, string csStr);
		bool CanParseSnapshot();
		ClientGame CreateClientGame(/*IJKClientImport*/JKClient client, int serverMessageNum, int serverCommandSequence, int clientNum);
		int GetEntityFieldOverride(int index, int defaultValue);
		int GetPlayerFieldOverride(int index, int defaultValue);
		void ClearState();
		void SetExtraConfigstringInfo(ServerInfo serverInfo, InfoString info);
	}
}
