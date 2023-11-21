using System;
using System.Collections.Generic;

namespace JKClient {
	public interface IClientHandler : INetHandler {
		ClientVersion Version { get; }
		int MaxReliableCommands { get; }
		int MaxConfigstrings { get; }
		int MaxClients { get; }
		bool CanParseRMG { get; }
		bool CanParseVehicle { get; }
		string GuidKey { get; }
		bool FullByteEncoding { get; }
		GameModification Modification { get; }
		void RequestAuthorization(Action<NetAddress, string> authorize);
		void AdjustServerCommandOperations(ref ServerCommandOperations cmd);
		void AdjustGameStateConfigstring(int i, string csStr);
		bool CanParseSnapshot();
		ClientGame CreateClientGame(IJKClientImport client, int serverMessageNum, int serverCommandSequence, int clientNum);
		IList<NetField> GetEntityStateFields();
		IList<NetField> GetPlayerStateFields(bool isVehicle, Func<bool> isPilot);
		void ClearState();
		void SetExtraConfigstringInfo(in ServerInfo serverInfo, in InfoString info);
	}
}
