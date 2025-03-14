# JKClient
An assetsless (headless) game client library for Jedi Knight: Jedi Academy, Jedi Knight II: Jedi Outcast and Quake III Arena games. The library is expandable to other id Tech 3-based games as well.

## Usage
```csharp
var jkclient = new JKClient(JKClient.GetKnownClientHandler(ProtocolVersion.Protocol26, ClientVersion.JA_v1_01));
jkclient.Start(ExceptionCallback);
jkclient.ServerCommandExecuted += ServerCommandExecuted;
jkclient.ServerInfoChanged += ServerInfoChanged;
jkclient.FrameExecuted += FrameExecuted;
await jkclient.Connect("192.168.0.1:29070");
jkclient.Disconnect();
jkclient.ServerCommandExecuted -= ServerCommandExecuted;
jkclient.ServerInfoChanged -= ServerInfoChanged;
jkclient.FrameExecuted -= FrameExecuted;
jkclient.Stop();
jkclient.Dispose();
```

```csharp
void ServerCommandExecuted(CommandEventArgs commandEventArgs) {
	Debug.WriteLine(commandEventArgs.Command.Argv(0));
}
void ServerInfoChanged(ServerInfo serverInfo) {
	Debug.WriteLine(serverInfo.HostName);
}
void FrameExecuted(long frameTime) {
	Debug.WriteLine(frameTime);
}
```

```csharp
var serverBrowser = new ServerBrowser(ServerBrowser.GetKnownBrowserHandler(ProtocolVersion.Protocol26));
serverBrowser.Start(ExceptionCallback, true);
var servers = await serverBrowser.GetNewList();
servers = await serverBrowser.RefreshList();
var serverInfo = await serverBrowser.GetServerInfo(NetAddress.FromString("192.168.0.1:29070"));
Debug.WriteLine(serverInfo.HostName);
serverBrowser.Stop();
serverBrowser.Dispose();
```

```csharp
void ExceptionCallback(JKClientException exception) {
	Debug.WriteLine(exception);
}
```

## Supported OSs
The library targets .NET Standard 2.0 (optionally .NET Standard 2.1), that means that the library can be ran on Windows, Mac, Linux, iOS, Android and others:
https://docs.microsoft.com/en-us/dotnet/standard/net-standard

## License
Dual license:
1. GPL covers most of the game-related code.
2. WTFPL covers the rest additional code that is not related to the game code.

