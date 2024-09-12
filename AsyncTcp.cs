using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AsyncTcp
{
    /// <summary>
    /// Ассинхронный TCP сервер
    /// </summary>
    public class AsyncTcpServer : IDisposable
    {
        /// <summary>
        /// Причина отключение клиента
        /// </summary>
        public enum DisconnectReason
        {
            Normal,
            Exception,
            ServerAborted,
            ServerStopped,
            Ping,
            TimeOut
        }
        #region Events
        /// <summary>
        /// Событие запуска сервера
        /// </summary>
        public class StartedEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<StartedEventArgs> OnStarted = (_param1, _param2) => { };
        private void InvokeOnStarted(StartedEventArgs _args)
        {
            EventHandler<StartedEventArgs> _onStarted = OnStarted;
            if (_onStarted == null) return;
            _onStarted(this, _args);
        }
        /// <summary>
        /// Событие остановки сервера
        /// </summary>
        public class StoppedEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<StoppedEventArgs> OnStopped = (_param1, _param2) => { };
        private void InvokeOnStopped(StoppedEventArgs _args)
        {
            EventHandler<StoppedEventArgs> _onStopped = OnStopped;
            if (_onStopped == null) return;
            _onStopped(this, _args);
        }
        /// <summary>
        /// Событие обработка ошибок на сервере
        /// </summary>
        public class ErrorEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public Exception Exception { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError = (_param1, _param2) => { };
        private void InvokeOnError(ErrorEventArgs _args)
        {
            EventHandler<ErrorEventArgs> _onError = OnError;
            if (_onError == null) return;
            _onError(this, _args);
        }
        /// <summary>
        /// Событие при подключении клиента к серверу
        /// </summary>
        public class ConnectionRequestEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public bool Accept { get; set; } = true;
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<ConnectionRequestEventArgs> OnConnectionRequest = (_param1, _param2) => { };
        private void InvokeOnConnectionRequest(ConnectionRequestEventArgs _args)
        {
            EventHandler<ConnectionRequestEventArgs> _ConnectionRequest = OnConnectionRequest;
            if (_ConnectionRequest == null) return;
            _ConnectionRequest(this, _args);
        }
        /// <summary>
        /// Событие подключения клиента к серверу
        /// </summary>
        public class ConnectedEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public IPEndPoint IPEndPoint { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<ConnectedEventArgs> OnConnected = (_param1, _param2) => { };
        private void InvokeOnConnected(ConnectedEventArgs _args)
        {
            EventHandler<ConnectedEventArgs> _onConnected = OnConnected;
            if (_onConnected == null) return;
            _onConnected(this, _args);
        }
        /// <summary>
        /// Событие отключения клиента от сервера
        /// </summary>
        public class DisconnectedEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public DisconnectReason Reason { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<DisconnectedEventArgs> OnDisconnected = (_param1, _param2) => { };
        private void InvokeOnDisconnected(DisconnectedEventArgs _args)
        {
            EventHandler<DisconnectedEventArgs> _onDisconnected = OnDisconnected;
            if (_onDisconnected == null) return;
            _onDisconnected(this, _args);
        }
        /// <summary>
        /// Событие получение данных от клиента
        /// </summary>
        public class DataReceivedEventArgs : EventArgs
        {
            /// <summary>
            /// 
            /// </summary>
            public TcpClient Client { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public byte[] Data { get; internal set; }
        }
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> OnDataReceived = (_param1, _param2) => { };
        private void InvokeOnDataReceived(DataReceivedEventArgs _args)
        {
            EventHandler<DataReceivedEventArgs> _onDataReceived = OnDataReceived;
            if (_onDataReceived == null) return;
            _onDataReceived(this, _args);
        }
        #endregion
        #region Vars
        private bool Disposed = false;
        /// <summary>
        /// 
        /// </summary>
        public bool Listening { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public string HostPort { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public IPEndPoint EndPoint { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public bool NoDelay { get; set; } = true;
        /// <summary>
        /// 
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// 
        /// </summary>
        public int ReceiveTimeout { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int SendBufferSize { get; set; } = 8192;
        /// <summary>
        /// 
        /// </summary>
        public int SendTimeout { get; set; }
        /// <summary>
        /// 
        /// </summary>
        private long _bytesReceived;
        /// <summary>
        /// 
        /// </summary>
        public long BytesReceived { get => _bytesReceived; private set => _bytesReceived = value; }
        /// <summary>
        /// 
        /// </summary>
        private long _bytesSent;
        /// <summary>
        /// 
        /// </summary>
        public long BytesSent { get => _bytesSent; private set => _bytesSent = value; }
        /// <summary>
        /// Время ожидания сообщений от клиента если = 0 => ожидание бесконечно, иначе отключения клиента если в течении тайма нет сообщений от клиента
        /// </summary>
        public int KeepAliveMessageTime { get; set; } = 0;
        private TcpListener Listener;
        private ConcurrentDictionary<string, ConnectedClients> Clients;
        private CancellationTokenSource CancellationTokensource;
        private CancellationToken Cancellationtoken;
        #endregion
        #region Classes
        /// <summary>
        /// 
        /// </summary>
        public class ConnectedClients : IDisposable
        {
            #region Vars
            private bool Disposed = false;
            private AsyncTcpServer Server;
            private CancellationTokenSource CancellationTokensource;
            private readonly CancellationToken Cancellationtoken;
            /// <summary>
            /// 
            /// </summary>
            public TcpClient Client { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public string IpPort { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public bool Connected => Client != null && Client.Connected;
            /// <summary>
            /// 
            /// </summary>
            public bool AcceptData { get; internal set; } = true;
            /// <summary>
            /// 
            /// </summary>
            public long BytesReceived { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public long BytesSent { get; internal set; }
            /// <summary>
            /// 
            /// </summary>
            public int DisconnectTimeOut { get; set; } = 60;
            private Timer TimerDisconnect;
            #endregion
            internal ConnectedClients(AsyncTcpServer _server, TcpClient _client, string _ipPort)
            {
                Client = _client;
                IpPort = _ipPort;
                Server = _server;
                CancellationTokensource = new CancellationTokenSource();
                Cancellationtoken = CancellationTokensource.Token;
            }
            /// <summary>
            /// Начать получение данных от клиента
            /// </summary>
            internal void StartReceiving() => Task.Factory.StartNew(ReceivingTask, TaskCreationOptions.LongRunning);
            /// <summary>
            /// Остановить получние данных от клиента
            /// </summary>
            internal void StopReceiving() { CancellationTokensource.Cancel(); TimerDisconnect.Dispose(); }
            /// <summary>
            /// Стриминг данных от клиента
            /// </summary>
            private async Task ReceivingTask()
            {
                if (DisconnectTimeOut > 0) TimerDisconnect = new Timer(new TimerCallback((x) =>
                {
                    if (!Server.Clients.TryGetValue(IpPort, out ConnectedClients _client)) return;
                    _client.StopReceiving();
                }), null, new TimeSpan(0, 0, 0, DisconnectTimeOut), new TimeSpan(0, 0, 0, DisconnectTimeOut));
                NetworkStream _stream = Client.GetStream();
                byte[] _buffer = new byte[Client.ReceiveBufferSize];
                try
                {
                    int _length = 0;
                    while (!CancellationTokensource.IsCancellationRequested)
                    {
                        var _flag = (_length = await _stream.ReadAsync(_buffer, Cancellationtoken)) != 0;
                        if (!_flag) { _stream.Dispose(); _stream = null; throw new IOException("Unable to read data from the transport connection: Удаленный хост принудительно разорвал существующее подключение.."); }
                        BytesReceived += _length;
                        Server.AddReceivedBytes(_length);
                        byte[] _destinationArray = new byte[_length];
                        Array.Copy(_buffer, _destinationArray, _length);
                        Server.InvokeOnDataReceived(new DataReceivedEventArgs()
                        {
                            Client = Client,
                            IpPort = IpPort,
                            Data = _destinationArray
                        });
                        TimerDisconnect?.Change(new TimeSpan(0, 0, 0, DisconnectTimeOut) + new TimeSpan(0, 0, 0, 30), new TimeSpan(0, 0, 0, DisconnectTimeOut) + new TimeSpan(0, 0, 0, 30));
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (!Server.Clients.TryGetValue(IpPort, out ConnectedClients _client)) return;
                    _client.StopReceiving();
                    _client.Client.Close();
                    _client.Client.Dispose();
                    Server.InvokeOnError(new ErrorEventArgs() { IpPort = IpPort, Exception = ex });
                    Server.DisconnectClient(IpPort, DisconnectReason.TimeOut);
                }
                catch (IOException ex)
                {
                    if (!Server.Clients.TryGetValue(IpPort, out ConnectedClients _client)) return;
                    _client.StopReceiving();
                    _client.Client.Close();
                    _client.Client.Dispose();
                    Server.InvokeOnError(new ErrorEventArgs() { IpPort = IpPort, Exception = ex });
                    Server.DisconnectClient(IpPort, DisconnectReason.Normal);
                }
                catch (InvalidCastException ex)
                {
                    if (!Server.Clients.TryGetValue(IpPort, out ConnectedClients _client)) return;
                    _client.StopReceiving();
                    _client.Client.Close();
                    _client.Client.Dispose();
                    Server.InvokeOnError(new ErrorEventArgs() { IpPort = IpPort, Exception = ex });
                    Server.DisconnectClient(IpPort, DisconnectReason.Exception);
                }
                catch (Exception ex)
                {
                    if (!Server.Clients.TryGetValue(IpPort, out ConnectedClients _client)) return;
                    _client.StopReceiving();
                    _client.Client.Close();
                    _client.Client.Dispose();
                    Server.InvokeOnError(new ErrorEventArgs() { IpPort = IpPort, Exception = ex });
                    Server.DisconnectClient(IpPort, DisconnectReason.Exception);
                }
                finally { if (Convert.ToBoolean(_stream)) { _stream.Close(); _stream.Dispose(); } _buffer = null; TimerDisconnect = null; Dispose(); }
            }
            /// <summary>
            /// Отправка массива байт клиенту
            /// </summary>
            public long SendBytes(byte[] _bytes)
            {
                if (!Connected) return 0;
                BytesSent += _bytes.Length;
                Server.AddSentBytes(_bytes.Length);
                return Client.Client.Send(_bytes);
            }
            /// <summary>
            /// Отправка строки клиенту
            /// </summary>
            public long SendString(string _data)
            {
                if (!Connected) return 0;
                byte[] _bytes = Encoding.UTF8.GetBytes(_data);
                BytesSent += _bytes.Length;
                Server.AddSentBytes(_bytes.Length);
                return Client.Client.Send(_bytes);
            }
            /// <summary>
            /// Отправка строки клиенту
            /// </summary>
            public long SendString(string _data, Encoding _encoding)
            {
                if (!Connected) return 0;
                byte[] _bytes = _encoding.GetBytes(_data);
                BytesSent += _bytes.Length;
                Server.AddSentBytes(_bytes.Length);
                return Client.Client.Send(_bytes);
            }
            /// <summary>
            /// Отправка файла клиенту
            /// </summary>
            public long SendFile(string _filePath)
            {
                if (!this.Connected || !File.Exists(_filePath)) return 0;
                FileInfo _fileInfo = new FileInfo(_filePath);
                if (_fileInfo == null) return 0;
                Client.Client.SendFile(_filePath);
                BytesSent += _fileInfo.Length;
                Server.AddSentBytes(_fileInfo.Length);
                return _fileInfo.Length;
            }
            /// <summary>
            /// Отправка файла клиенту
            /// </summary>
            public long SendFile(string _filePath, byte[] _preBuffer, byte[] _postBuffer, TransmitFileOptions _flags)
            {
                if (!Connected || !File.Exists(_filePath)) return 0;
                FileInfo _fileInfo = new FileInfo(_filePath);
                if (_fileInfo == null) return 0;
                Client.Client.SendFile(_filePath, _preBuffer, _postBuffer, _flags);
                BytesSent += _fileInfo.Length;
                Server.AddSentBytes(_fileInfo.Length);
                return _fileInfo.Length;
            }
            /// <inheritdoc/>
            public void Dispose()
            {
                Dispose(true);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.SuppressFinalize(this);
            }
            /// <inheritdoc/>
            protected virtual void Dispose(bool _disposing)
            {
                if (!Disposed)
                {
                    if (!Cancellationtoken.IsCancellationRequested) CancellationTokensource.Cancel();
                    if (Client.Connected) { Client.Close(); Client.Dispose(); }
                    if (Client != null) Client = null;
                    if (Server != null) Server = null;
                    IpPort = null;
                    BytesReceived = 0;
                    BytesSent = 0;
                    DisconnectTimeOut = 0;
                    if (TimerDisconnect != null) TimerDisconnect = null;
                    CancellationTokensource = null;
                    Disposed = _disposing;
                }
            }
            /// <inheritdoc/>
            ~ConnectedClients() => Dispose(false);
        }
        #endregion
        /// <summary>
        /// Создать ассинхронный сервер
        /// </summary>
        public AsyncTcpServer(IPAddress _Host, int _Port)
        {
            EndPoint = new IPEndPoint(_Host, _Port);
            HostPort = EndPoint.ToString();
            Clients = new ConcurrentDictionary<string, ConnectedClients>();
        }
        /// <summary>
        /// Создать ассинхронный сервер
        /// </summary>
        public AsyncTcpServer(string _Host, int _Port)
        {
            EndPoint = new IPEndPoint(IPAddress.Parse(_Host), _Port);
            HostPort = EndPoint.ToString();
            Clients = new ConcurrentDictionary<string, ConnectedClients>();
        }
        /// <summary>
        /// Запуск прослушивание сокета
        /// </summary>
        public void Start()
        {
            Clients.Clear();
            CancellationTokensource = new CancellationTokenSource();
            Cancellationtoken = CancellationTokensource.Token;
            Task.Factory.StartNew(new Action(ListeningTask), TaskCreationOptions.LongRunning);
        }
        /// <summary>
        /// Остановка прослушивание сокета
        /// </summary>
        public void Stop()
        {
            if (Clients != null) foreach (string _IpPort in Clients.Keys.ToList()) Disconnect(_IpPort);
            Listener.Stop();
            Listening = false;
            CancellationTokensource.Cancel();
            InvokeOnStopped(new StoppedEventArgs() { IpPort = HostPort });
        }
        /// <summary>
        /// Получить клиента
        /// </summary>
        public ConnectedClients GetClient(string _IpPort) { return !Clients.TryGetValue(_IpPort, out ConnectedClients _value) ? null : _value; }
        /// <summary>
        /// Получить всех клиентов
        /// </summary>
        public List<ConnectedClients> GetClients(string _IpPort)
        {
            var _listClients = new List<ConnectedClients>();
            if (Clients.TryGetValue(_IpPort, out ConnectedClients _Client)) _listClients.Add(_Client);
            return _listClients;
        }
        /// <summary>
        /// Добавление полученных байт в взаимосвязь
        /// </summary>
        private void AddReceivedBytes(long _bytesCount) => Interlocked.Add(ref _bytesReceived, _bytesCount);
        /// <summary>
        /// Добавление отправляемых байт в взаимосвязь
        /// </summary>
        private void AddSentBytes(long _bytesCount) => Interlocked.Add(ref _bytesSent, _bytesCount);
        /// <summary>
        /// Отключение клиента
        /// </summary>
        private void DisconnectClient(string _IpPort, DisconnectReason _reason = DisconnectReason.Normal)
        {
            InvokeOnDisconnected(new DisconnectedEventArgs() { IpPort = _IpPort, Reason = _reason });
            if (Clients.TryGetValue(_IpPort, out ConnectedClients _Client)) Clients.TryRemove(_Client.IpPort, out ConnectedClients _);
        }
        /// <summary>
        /// Отключение клиента
        /// </summary>
        /// <param name="_IpPort">строка идентификатор, IP адрес:порт клиента</param>
        public void Disconnect(string _IpPort)
        {
            if (!Clients.TryGetValue(_IpPort, out ConnectedClients _Client)) return;
            _Client.Client.Close();
        }
        /// <summary>
        /// Задача прослушивания подключения клиентов
        /// </summary>
        private async void ListeningTask()
        {
            Listener = new TcpListener(EndPoint);
            Listener.Server.NoDelay = NoDelay;
            Listener.Start();
            Listening = true;
            InvokeOnStarted(new StartedEventArgs() { IpPort = HostPort });
            while (!Cancellationtoken.IsCancellationRequested)
            {
                TcpClient _Client = await Listener.AcceptTcpClientAsync();
                try
                {
                    IPEndPoint _EndPoint = (IPEndPoint)_Client.Client.RemoteEndPoint;
                    ConnectionRequestEventArgs _args = new() { IpPort = _EndPoint.ToString(), Accept = true };
                    InvokeOnConnectionRequest(_args);
                    if (!_args.Accept)
                    {
                        _Client.Client.Close();
                        continue;
                    }
                    else
                    {
                        _Client.NoDelay = NoDelay;
                        _Client.ReceiveBufferSize = ReceiveBufferSize;
                        _Client.ReceiveTimeout = ReceiveTimeout;
                        _Client.SendBufferSize = SendBufferSize;
                        _Client.SendTimeout = SendTimeout;
                        ConnectedClients _ConnectedClients = new(this, _Client, _EndPoint.ToString())
                        {
                            DisconnectTimeOut = KeepAliveMessageTime
                        };
                        Clients[_ConnectedClients.IpPort] = _ConnectedClients;
                        _ConnectedClients.StartReceiving();
                        InvokeOnConnected(new ConnectedEventArgs() { IpPort = _EndPoint.ToString(), IPEndPoint = _EndPoint });
                    }
                }
                catch (Exception) { _Client.Close(); }
            }
            Listening = false;
        }
        /// <summary>
        /// Отправить сообщение массивом байт клиенту
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_bytes">Сообщение ввиде массива байт</param>
        public long SendBytes(string _IpPort, byte[] _bytes)
        {
            ConnectedClients _client = GetClient(_IpPort);
            return _client == null ? 0L : _client.SendBytes(_bytes);
        }
        /// <summary>
        /// Отправить сообщение массивом байт клиенту асинхронно
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_bytes">Сообщение ввиде массива байт</param>
        /// <param name="_token">Токен отмены задачи</param>
        public async Task<long> SendBytesAsync(string _IpPort, byte[] _bytes, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendBytes(_IpPort, _bytes);
        }
        /// <summary>
        /// Отправить сообщение строкой клиенту
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_data">Сообщение ввиде строки</param>
        public long SendString(string _IpPort, string _data)
        {
            ConnectedClients _client = GetClient(_IpPort);
            return _client == null ? 0L : _client.SendString(_data);
        }
        /// <summary>
        /// Отправить сообщение строкой клиенту асинхронно
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_data">Сообщение ввиде строки</param>
        /// <param name="_token">Токен отмены задачи</param>
        public async Task<long> SendStringAsync(string _IpPort, string _data, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendString(_IpPort, _data);
        }
        /// <summary>
        /// Отправить сообщение строкой в пользовательском кодировании клиенту
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_data">Сообщение ввиде строки</param>
        /// <param name="_encoding">Вид кодирования</param>
        public long SendString(string _IpPort, string _data, Encoding _encoding)
        {
            ConnectedClients _client = GetClient(_IpPort);
            return _client == null ? 0L : _client.SendString(_data, _encoding);
        }
        /// <summary>
        /// Отправить сообщение строкой в пользовательском кодировании клиенту асинхронно
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_data">Сообщение ввиде строки</param>
        /// <param name="_encoding">Вид кодирования</param>
        /// <param name="_token">Токен отмены задачи</param>
        public async Task<long> SendStringAsync(string _IpPort, string _data, Encoding _encoding, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendString(_IpPort, _data, _encoding);
        }
        /// <summary>
        /// Отправить сообщение файлом клиенту
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_fileName">Путь до файла</param>
        public long SendFile(string _IpPort, string _fileName)
        {
            ConnectedClients _client = GetClient(_IpPort);
            return _client == null ? 0L : _client.SendFile(_fileName);
        }
        /// <summary>
        /// Отправить сообщение файлом клиенту асинхронно
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_fileName">Путь до файла</param>
        /// <param name="_token">Токен отмены задачи</param>
        public async Task<long> SendFileAsync(string _IpPort, string _fileName, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendFile(_IpPort, _fileName);
        }
        /// <summary>
        /// Отправить сообщение файлом клиенту
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_fileName">Путь до файла</param>
        /// <param name="_preBuffer"></param>
        /// <param name="_postBuffer"></param>
        /// <param name="_flags">Параметры передачи файлов</param>
        public long SendFile(string _IpPort, string _fileName, byte[] _preBuffer, byte[] _postBuffer, TransmitFileOptions _flags)
        {
            ConnectedClients _client = GetClient(_IpPort);
            return _client == null ? 0L : _client.SendFile(_fileName, _preBuffer, _postBuffer, _flags);
        }
        /// <summary>
        /// Отправить сообщение файлом клиенту асинхронно
        /// </summary>
        /// <param name="_IpPort">Идентификатор клиента</param>
        /// <param name="_fileName">Путь до файла</param>
        /// <param name="_preBuffer"></param>
        /// <param name="_postBuffer"></param>
        /// <param name="_flags">Параметры передачи файлов</param>
        /// <param name="_token">Токен отмены задачи</param>
        public async Task<long> SendFileAsync(string _IpPort, string _fileName, byte[] _preBuffer, byte[] _postBuffer, TransmitFileOptions _flags, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendFile(_IpPort, _fileName, _preBuffer, _postBuffer, _flags);
        }
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.SuppressFinalize(this);
        }
        /// <inheritdoc/>
        protected virtual void Dispose(bool _disposing)
        {
            if (!Disposed)
            {
                if (Listening) Stop();
                if (!Cancellationtoken.IsCancellationRequested) CancellationTokensource.Cancel();
                if (Clients != null) { Clients.Clear(); Clients = null; }
                if (Listener != null) { Listener.Dispose(); Listener = null; }
                HostPort = null;
                EndPoint = null;
                ReceiveBufferSize = 0;
                ReceiveTimeout = 0;
                SendBufferSize = 0;
                SendTimeout = 0;
                _bytesReceived = 0;
                _bytesSent = 0;
                KeepAliveMessageTime = 0;
                if (CancellationTokensource != null) { CancellationTokensource.Dispose(); CancellationTokensource = null; }
                Disposed = _disposing;
            }
        }
        /// <inheritdoc/>
        ~AsyncTcpServer() => Dispose(false);
    }
    /// <summary>
    /// 
    /// </summary>
    public class AsyncTcpClient
    {
        /// <summary>
        /// Причина отключения клиента
        /// </summary>
        public enum DisconnectReason
        {
            /// <summary>
            /// 
            /// </summary>
            Normal,
            /// <summary>
            /// 
            /// </summary>
            Exception,
            /// <summary>
            /// 
            /// </summary>
            ServerAborted,
            /// <summary>
            /// 
            /// </summary>
            ServerStopped,
            /// <summary>
            /// 
            /// </summary>
            Ping,
            /// <summary>
            /// 
            /// </summary>
            TimeOut
        }
        /// <summary>
        /// 
        /// </summary>
        public class ConnectedEventArgs : EventArgs
        {
            public IPAddress ServerIPAddress => IPAddress.Parse(ServerHost);
            public string ServerHost { get; internal set; }
            public int ServerPort { get; internal set; }
        }
        public event EventHandler<ConnectedEventArgs> OnConnected = (_param1, _param2) => { };
        internal void InvokeOnConnected(ConnectedEventArgs _args)
        {
            EventHandler<ConnectedEventArgs> _onConnected = OnConnected;
            if (_onConnected == null) return;
            _onConnected(this, _args);
        }
        public class DataReceivedEventArgs : EventArgs
        {
            public string ServerHost { get; internal set; }
            public byte[] Data { get; internal set; }
        }
        public event EventHandler<DataReceivedEventArgs> OnDataReceived = (_param1, _param2) => { };
        internal void InvokeOnDataReceived(DataReceivedEventArgs _args)
        {
            EventHandler<DataReceivedEventArgs> _onDataReceived = OnDataReceived;
            if (_onDataReceived == null) return;
            _onDataReceived(this, _args);
        }
        public class DisconnectedEventArgs : EventArgs
        {
            public string ServerHost { get; internal set; }
            public DisconnectReason Reason { get; internal set; }
        }
        public event EventHandler<DisconnectedEventArgs> OnDisconnected = (_param1, _param2) => { };
        internal void InvokeOnDisconnected(DisconnectedEventArgs _args)
        {
            EventHandler<DisconnectedEventArgs> _onDisconnected = OnDisconnected;
            if (_onDisconnected == null) return;
            _onDisconnected(this, _args);
        }
        public class ErrorEventArgs : EventArgs
        {
            public string ServerHost { get; internal set; }
            public Exception Exception { get; internal set; }
        }
        public event EventHandler<ErrorEventArgs> OnError = (_param1, _param2) => { };
        internal void InvokeOnError(ErrorEventArgs _args)
        {
            EventHandler<ErrorEventArgs> _onError = OnError;
            if (_onError == null) return;
            _onError(this, _args);
        }
        public class ReconnectedEventArgs : EventArgs
        {
            public IPAddress ServerIPAddress => IPAddress.Parse(ServerHost);
            public string ServerHost { get; internal set; }
            public int ServerPort { get; internal set; }
        }
        public event EventHandler<ReconnectedEventArgs> OnReconnected = (_param1, _param2) => { };
        internal void InvokeOnReconnected(ReconnectedEventArgs _args)
        {
            EventHandler<ReconnectedEventArgs> _onReconnected = OnReconnected;
            if (_onReconnected == null) return;
            _onReconnected(this, _args);
        }

        public string Host { get; private set; }
        public int Port { get; private set; }
        public bool NoDelay { get; set; } = true;
        public bool KeepAlive { get; set; } = false;
        public int KeepAliveTime { get; set; } = 900;
        public int KeepAliveInterval { get; set; } = 300;
        public int KeepAliveRetryCount { get; set; } = 5;
        private int _ReceiveBufferSize = 8192;
        public int ReceiveBufferSize
        {
            get => _ReceiveBufferSize;
            set
            {
                _ReceiveBufferSize = value;
                _RecvBuffer = new byte[value];
            }
        }
        public int ReceiveTimeout { get; set; }
        private int _SendBufferSize = 8192;
        public int SendBufferSize
        {
            get => _SendBufferSize;
            set
            {
                _SendBufferSize = value;
                _SendBuffer = new byte[value];
            }
        }
        public int SendTimeout { get; set; }
        public long BytesReceived { get; internal set; }
        public long BytesSent { get; internal set; }
        public bool Reconnect { get; set; }
        public int ReconnectDelayInSeconds { get; set; } = 5;
        public bool AcceptData { get; set; } = true;
        public bool Reconnecting { get; private set; }
        public bool Connected => _Socket != null && _Socket.Connected;

        private Socket _Socket;
        private byte[] _RecvBuffer;
        private byte[] _SendBuffer;
        private CancellationTokenSource _CancellationTokensource;
        private CancellationToken _Cancellationtoken;

        public AsyncTcpClient(string _Host, int _Port)
        {
            Host = _Host;
            Port = _Port;
        }

        public void Connect()
        {
            _RecvBuffer = new byte[ReceiveBufferSize];
            _SendBuffer = new byte[SendBufferSize];
            IPHostEntry _HostEntry = Dns.GetHostEntry(Host);
            if (_HostEntry.AddressList.Length == 0) throw new Exception("Unable to solve host address");
            IPAddress _Address = _HostEntry.AddressList[0];
            if (_Address.ToString() == "::1") _Address = new IPAddress(16777343L);
            IPEndPoint _RemoteEP = new IPEndPoint(_Address, Port);
            _Socket = new Socket(_Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _Socket.NoDelay = NoDelay;
            _Socket.ReceiveBufferSize = ReceiveBufferSize;
            _Socket.ReceiveTimeout = ReceiveTimeout;
            _Socket.SendBufferSize = SendBufferSize;
            _Socket.SendTimeout = SendTimeout;
            if (KeepAlive && KeepAliveInterval > 0) _Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _Socket.Connect(_RemoteEP);
            if (_CancellationTokensource != null) _CancellationTokensource.Cancel();
            _CancellationTokensource = new CancellationTokenSource();
            _Cancellationtoken = _CancellationTokensource.Token;
            Task.Factory.StartNew(ReceiverTask, TaskCreationOptions.LongRunning);
            InvokeOnConnected(new ConnectedEventArgs() { ServerHost = Host, ServerPort = Port,  });
        }
        public void Disconnect() => Disconnect(DisconnectReason.Normal);
        public long SendBytes(byte[] _bytes)
        {
            if (!Connected) return 0;
            int _num = _Socket.Send(_bytes);
            BytesSent += _num;
            return _num;
        }
        public async Task<long> SendBytesAsync(byte[] _bytes, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendBytes(_bytes);
        }
        public long SendString(string _data)
        {
            if (!Connected) return 0;
            int _num = _Socket.Send(Encoding.UTF8.GetBytes(_data));
            BytesSent += _num;
            return _num;
        }
        public async Task<long> SendStringAsync(string _data, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendString(_data);
        }
        public long SendString(string _data, Encoding _encoding)
        {
            if (!Connected) return 0;
            int _num = _Socket.Send(_encoding.GetBytes(_data));
            BytesSent += _num;
            return _num;
        }
        public async Task<long> SendStringAsync(string _data, Encoding _encoding, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendString(_data, _encoding);
        }
        public long SendFile(string _fileName)
        {
            if (!Connected || !System.IO.File.Exists(_fileName)) return 0;
            FileInfo _fileInfo = new FileInfo(_fileName);
            if (_fileInfo == null) return 0;
            _Socket.SendFile(_fileName);
            BytesSent += _fileInfo.Length;
            return _fileInfo.Length;
        }
        public async Task<long> SendFileAsync(string _fileName, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendFile(_fileName);
        }
        public long SendFile(string _fileName, byte[] _preBuffer, byte[] _postBuffer, TransmitFileOptions _flags)
        {
            if (!Connected || !System.IO.File.Exists(_fileName)) return 0;
            FileInfo _fileInfo = new FileInfo(_fileName);
            if (_fileInfo == null) return 0;
            _Socket.SendFile(_fileName, _preBuffer, _postBuffer, _flags);
            BytesSent += _fileInfo.Length;
            return _fileInfo.Length;
        }
        public async Task<long> SendFileAsync(string _fileName, byte[] _preBuffer, byte[] _postBuffer, TransmitFileOptions _flags, CancellationToken _token)
        {
            _token.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            return SendFile(_fileName, _preBuffer, _postBuffer, _flags);
        }
        private void ReceiverTask()
        {
            try
            {
                while (!_Cancellationtoken.IsCancellationRequested)
                {
                    int _length = _Socket.Receive(_RecvBuffer);
                    if (_length > 0)
                    {
                        BytesReceived += _length;
                        if (AcceptData)
                        {
                            byte[] _destinationArray = new byte[_length];
                            Array.Copy(_RecvBuffer, _destinationArray, _length);
                            InvokeOnDataReceived(new DataReceivedEventArgs() { Data = _destinationArray });
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.ConnectionAborted) Disconnect(DisconnectReason.ServerAborted);
                else Disconnect(DisconnectReason.Normal);
            }
            catch (Exception ex)
            {
                InvokeOnError(new ErrorEventArgs() { Exception = ex });
                Disconnect(DisconnectReason.Exception);
            }
        }
        private async void Disconnect(DisconnectReason _reason)
        {
            try
            {
                _CancellationTokensource.Cancel();
            }
            catch { }
            try
            {
                _Socket.Shutdown(SocketShutdown.Both);
                _Socket.Close();
            }
            catch { }
            try
            {
                _Socket.Dispose();
            }
            catch { }
            InvokeOnDisconnected(new DisconnectedEventArgs() { Reason = _reason, ServerHost = $"{Host}:{Port}" });
            if (!Reconnect || Reconnecting) return;
            Reconnecting = true;
            while (!Connected)
            {
                try
                {
                    await Task.Delay(new TimeSpan(0, 0, 0, ReconnectDelayInSeconds));
                    Connect();
                    break;
                }
                catch { }
            }
            Reconnecting = false;
            InvokeOnReconnected(new ReconnectedEventArgs() { ServerHost = Host, ServerPort = Port });
        }
    }
}
