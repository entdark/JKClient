# JKClient
An assetsless (headless) game client library for Jedi Knight: Jedi Academy and Jedi Knight II: Jedi Outcast games.

## Usage
```csharp
var jkclient = new JKClient();
jkclient.Start(ExceptionCallback);
jkclient.Connect("192.168.0.1", ProtocolVersion.Protocol26);
jkclient.ServerCommandExecutedCallback = ServerCommandExecutedCallback;
jkclient.Disconnect();
jkclient.Stop();
jkclient.Dispose();
```

```csharp
void ServerCommandExecutedCallback(Command command) {
	Debug.WriteLine(command.Argv(0));
}
```

```csharp
var serverBrowser = new ServerBrowser();
serverBrowser.Start(ExceptionCallback);
var servers = serverBrowser.GetNewList();
servers = serverBrowser.RefreshList();
serverBrowser.Stop();
serverBrowser.Dispose();
```

```csharp
void Task ExceptionCallback(JKClientException exception) {
	Debug.WriteLine(exception);
}
```

## Supported OSs
The library targets .NET Standard 2.1 (optionally .NET Standard 2.0), that means that the library can be ran on Windows, Mac, Linux, iOS, Android and others:
https://docs.microsoft.com/ru-ru/dotnet/standard/net-standard

## License
Dual license:
1. GPL covers most of the game-related code.
2. WTFPL covers the rest additional code that is not related to the game code.

