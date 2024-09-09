# AsyncTcpServer
Starting Server
```cs
public static void Main()
{
	var SRV = new AsyncTcpServer(IPAddress.Any, 33333);
	SRV.KeepAlive = false;
	SRV.OnConnected += SRV_ClientConnected;
	SRV.OnDisconnected += SRV_ClientDisconnected;
	SRV.OnDataReceived += SRV_DataReceived;
	SRV.OnStarted += SRV_OnStarted;
	SRV.OnStopped += SRV_OnStopped;
	SRV.OnError += SRV_OnError;
	SRV.Start();
}
```
Starting Client
```cs
public static void Main()
{
	var CLN = new AsyncTcpClient("127.0.0.1", 33333);
    CLN.KeepAlive = false;
    CLN.OnConnected += CLN_ServerConnected;
    CLN.OnDisconnected += CLN_ServerDisconnected;
    CLN.OnReconnected += CLN_ServerReconnected;
    CLN.OnError += CLN_ServerError;
    CLN.OnDataReceived += CLN_ServerDataReceived;
    CLN.Connect();
}
```