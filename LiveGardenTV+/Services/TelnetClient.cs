using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace LiveGardenTVPlus.Services
{
    public class TelnetClient : IDisposable
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _lock = new object();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _readTask;
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
                {
                    _cts = new CancellationTokenSource();
                    _readTask = ReadAsync(_cts.Token);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TelnetClient: Connect error - {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
                return false;
            }
        }

        private async Task ReadAsync(CancellationToken token)
        {
            var buffer = new byte[4096];
            try
            {
                while (!token.IsCancellationRequested && IsConnected)
                {
                    int bytes = await _stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytes == 0) break;
                    string data = Encoding.UTF8.GetString(buffer, 0, bytes);
                    DataReceived?.Invoke(data);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("TelnetClient: Read cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TelnetClient: Read error - {ex.Message}");
                // Do not rethrow or disconnect here – let the caller handle it
            }
            finally
            {
                // Ensure clean disconnect if still connected
                if (IsConnected)
                    Disconnect();
            }
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            Debug.WriteLine("TelnetClient: Sending username...");
            await SendCommandAsync(username);
            await Task.Delay(300);
            Debug.WriteLine("TelnetClient: Sending password...");
            await SendCommandAsync(password);
            await Task.Delay(300);
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
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = null;
                }
                _stream?.Close();
                _client?.Close();
                _stream = null;
                _client = null;
                Debug.WriteLine("TelnetClient: Disconnected");
                ConnectionStateChanged?.Invoke(false);
            }
        }

        public void Dispose()
        {
            Disconnect();
            _cts?.Dispose();
        }
    }
}