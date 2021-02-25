namespace JKClient {
	internal enum ServerCommandOperation {
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