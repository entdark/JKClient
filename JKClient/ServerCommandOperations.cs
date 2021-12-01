namespace JKClient {
	internal enum ServerCommandOperations {
		Bad,
		Nop,
		Gamestate,
		Configstring,
		Baseline,
		ServerCommand,
		Download,
		Snapshot,
		SetGame,
		MapChange,
		EOF
	}
}