using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Network
{
    public class Networking
    {
        const string BEGIN_PACKET = "BEGIN_PACKET";

        public bool logEnabled = false;

        private bool _isBusy = false;
        public bool IsBusy => _isBusy;

        private TcpClient tcpClient;

        public Networking(int port)
        {
            this.port = port;
        }

        ~Networking()
        {
            Free();
        }

        public void Free()
        {
            // Останавливаем клиент
            if (clientConnected) ClientDisconnect();

            // Останавливаем сервер
            if (serverRunning) StopServer();
        }

        private TcpListener tcpListener;
        private readonly int port;

        #region Events

        /// <summary>
        /// Данные отправлены
        /// </summary>
        public Action SentData;
        public Action Connected;
        public Action Disconnected;

        #endregion

        #region Client

        private bool clientConnected;
        public Action ErrorConnection;        

        // Поток подключение 
        private CancellationTokenSource clientThreadTokenSource;
        private CancellationTokenSource serverThreadTokenSource;

        public void ConnectToTcpServer(string ip)
        {
            if (!clientConnected)
            {
                try
                {
                    clientThreadTokenSource = new CancellationTokenSource();
                    CancellationToken clientThreadToken = clientThreadTokenSource.Token;
                    Task task = new Task(() => ListenForData(ip, clientThreadToken));
                    task.Start();
                }
                catch (Exception e)
                {
                    Debug.LogError("On client connect exception " + e);
                }
            }
            else
            {
                Debug.Log("Уже подключен к серверу");
            }
        }

        public void ClientDisconnect()
        {
            if (clientThreadTokenSource != null) clientThreadTokenSource.Cancel();
        }

        private static bool IsConnected(Socket socket)
        {
            try
            {
                return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary> 	
        /// Runs in background clientReceiveThread; Listens for incomming data. 	
        /// </summary>     
        private void ListenForData(object ip, CancellationToken clientThreadToken)
        {
            try
            {
                tcpClient = new TcpClient((string)ip, port);
                // Get a stream object for reading 	
                NetworkStream networkStream = tcpClient.GetStream();
                Debug.Log("Client connected");
                clientConnected = true;

                if (Connected != null)
                {
                    UnityMainThread.wkr.AddJob(() => Connected?.Invoke());
                }

                while (true)
                {
                    if (!IsConnected(tcpClient.Client))
                    {
                        clientThreadTokenSource.Cancel();
                        Debug.Log("Connection closed " + tcpClient.Client.LocalEndPoint);
                        UnityMainThread.wkr.AddJob(() => ErrorConnection?.Invoke());                        
                    }

                    if (clientThreadToken.IsCancellationRequested)
                    {
                        Debug.LogError("Disconnected");
                        tcpClient.Close();                        
                        tcpClient.Dispose();                        

                        if (Disconnected != null) Disconnected.Invoke();
                        
                        clientConnected = false;

                        return;
                    }

                    receiveData(networkStream);

                }
            }
            catch (SocketException socketException)
            {
                Debug.LogError("Socket exception: " + socketException);
                UnityMainThread.wkr.AddJob(() => ErrorConnection?.Invoke());
            }
        }

        #endregion

        #region Server

        /// <summary>
        /// Данные получены
        /// </summary>
        public Action<string> ReceivedData;
        public Action<byte[]> ReceivedRawData;

        private bool serverRunning;
        private readonly List<TcpClient> clients = new List<TcpClient>();

        public void StartServer(string localIp)
        {
            if (!serverRunning)
            {
                // Create listener on localhost.
                tcpListener = new TcpListener(IPAddress.Parse(localIp), port);
                serverThreadTokenSource = new CancellationTokenSource();

                Task.Run(StartServer, serverThreadTokenSource.Token);
            }
            else
            {
                Debug.LogError("Сервер уже запущен");
            }
        }

        private async Task StartServer()
        {
            try
            {
                tcpListener.Start();
                serverRunning = true;
                Debug.Log("Server is listening");                

                while (serverRunning)
                {                    
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();  
					Task.Run(() => AddClient(client, serverThreadTokenSource.Token));
                }
            }
            catch (SocketException socketException)
            {
                Debug.LogError("SocketException " + socketException);
            }
        }

        public void StopServer()
        {
			if(!serverRunning) return;		            

            tcpListener?.Stop();

            if(serverThreadTokenSource != null) serverThreadTokenSource.Cancel();

            foreach (var client in clients)
            {
                client.Dispose();
            }

            clients.Clear();

            serverRunning = false;

            if (Disconnected != null) Disconnected.Invoke();
        }

        private void AddClient(TcpClient client, CancellationToken serverToken)
        {
            NetworkStream stream;
            tcpClient = client;
            clients.Add(client);

            string endpoint = client.Client.RemoteEndPoint.ToString();
            Debug.Log("Connected client " + endpoint);
            using (stream = tcpClient.GetStream())
            {
                while (true)
                {
                    bool isClosed = !IsConnected(client.Client);
                    if (isClosed)
                    {                                                                        
                        Debug.Log("Client disconnected " + endpoint);
                        client.Close();
                        clients.Remove(client);

                        client.Dispose();                        
                    }

                    // Сервер остановлен, отключаем клиентов
                    if (serverToken.IsCancellationRequested)
                    {
                        client.Close();
                        clients.Remove(client);

                        client.Dispose();

                        return;
                    }

                    receiveData(stream);
                }
            }
        }

        #endregion

        public async Task SendMessage(byte[] sendData)
        {
            if (serverRunning)
            {
                foreach (var client in clients)
                {
                    NetworkStream networkStream = client.GetStream();
                    await Task.Run(() => Send(networkStream, sendData));
                }
            }
            else if (clientConnected)
            {
                NetworkStream networkStream = tcpClient.GetStream();
                await Task.Run(() => Send(networkStream, sendData));
            }
        }

        public async Task SendMessage(string sendData)
        {
            await SendMessage(Encoding.UTF8.GetBytes(sendData));
        }

        private void receiveData(NetworkStream networkStream)
        {
            if (!networkStream.DataAvailable || !networkStream.CanRead) return;

            byte[] data = null;

            //GZipStream zipStream = new GZipStream(networkStream, CompressionMode.Decompress);
            BinaryReader reader = new BinaryReader(networkStream);            

            string header_mark = reader.ReadString();
            if (header_mark != BEGIN_PACKET) return;

            int size = reader.ReadInt32();

            if (size > 10 * 1024 * 1024) return; // Размер не должен превышать 10 мб

            data = reader.ReadBytes(size);

            int len = data.Length;

            // Get bytes
            if (ReceivedRawData != null)
            {
                if (data.Length > 0)
                {
                    // wrk is null
                    UnityMainThread.wkr.AddJob(() => ReceivedRawData?.Invoke(data));
                }
            }

            // Get text
            if (ReceivedData != null)
            {
                string text = Encoding.UTF8.GetString(data);
                if (!string.IsNullOrEmpty(text))
                {
                    UnityMainThread.wkr.AddJob(() => ReceivedData?.Invoke(text));
                }
            }
        }  

        private void Send(Stream networkStream, byte[] sendData)
        {
            try
            {
                if (_isBusy) return;
                _isBusy = true;

                if (networkStream.CanWrite)
                {
                    BinaryWriter writer = new BinaryWriter(networkStream);

                    // Send Header                        
                    writer.Write(BEGIN_PACKET);
                    writer.Write(sendData.Length);

                    // Send Data
                    writer.Write(sendData);

                    if (logEnabled) Debug.Log("Server sent " + sendData.Length + " bytes");
                }
                SentData?.Invoke();
            }
            catch (SocketException socketException)
            {
                Debug.LogError("Socket exception: " + socketException);
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}