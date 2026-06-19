using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LiveGardenTVPlus.Services
{
    public class TelnetClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _lock = new object();
        public event Action<string> DataReceived;
        public event Action<string> ErrorOccurred;
        public event Action<bool> ConnectionStateChanged;

        public bool IsConnected => _client?.Connected == true;

        public async Task<bool> ConnectAsync(string host, int port, bool startReader = true)
        {
            try
            {
                Debug.WriteLine($"TelnetClient: Connecting to {host}:{port}");
                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                Debug.WriteLine("TelnetClient: Connected");
                ConnectionStateChanged?.Invoke(true);
                if (startReader)
                    _ = ReadAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TelnetClient: Connect error - {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
                return false;
            }
        }

        private async Task ReadAsync()
        {
            var buffer = new byte[4096];
            while (IsConnected)
            {
                try
                {
                    int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytes == 0) break;
                    string data = Encoding.UTF8.GetString(buffer, 0, bytes);
                    DataReceived?.Invoke(data);
                }
                catch (Exception ex)
                {
                    // Log the error but do NOT disconnect or propagate
                    Debug.WriteLine($"TelnetClient: Read error - {ex.Message}");
                    // Do NOT call Disconnect() here – let the connection close naturally
                    break;
                }
            }
            // Only disconnect if the stream is still open
            if (_stream != null && _client != null && _client.Connected)
            {
                Disconnect();
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            Debug.WriteLine("TelnetClient: Waiting for login prompt...");
            await Task.Delay(1000); // Wait for the "login:" prompt
            await SendCommandAsync(username);
            await Task.Delay(800);
            await SendCommandAsync(password);
            await Task.Delay(800);
            Debug.WriteLine("TelnetClient: Login sequence completed");
            return true;
        }

        public async Task SendCommandAsync(string command)
        {
            if (!IsConnected) return;
            byte[] data = Encoding.UTF8.GetBytes(command + "\r\n");
            await _stream.WriteAsync(data, 0, data.Length);
            Debug.WriteLine($"TelnetClient: Sent command '{command}'");
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _stream?.Close();
                _client?.Close();
                _stream = null;
                _client = null;
                Debug.WriteLine("TelnetClient: Disconnected");
                ConnectionStateChanged?.Invoke(false);
            }
        }

        public void Dispose() => Disconnect();
    }
}