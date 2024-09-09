# AsyncTcpServer
public static void Main()
{
	GuardApplication = new AsyncTcpServer(IPAddress.Any, settingsService.Port);
	GuardApplication.KeepAlive = false;
	GuardApplication.OnConnected += GuardApplication_ClientConnected;
	GuardApplication.OnDisconnected += GuardApplication_ClientDisconnected;
	GuardApplication.OnDataReceived += GuardApplication_DataReceived;
	GuardApplication.OnStarted += GuardApplication_OnStarted;
	GuardApplication.OnStopped += GuardApplication_OnStopped;
	GuardApplication.OnError += GuardApplication_OnError;
	GuardApplication.Start();
}