using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arcademia.Achievements
{
    internal sealed class LauncherTransport : IDisposable
    {
        public const string PipeVariable = "ARCADEMIA_PIPE";
        public const string NonceVariable = "ARCADEMIA_NONCE";
        public const string SessionVariable = "ARCADEMIA_SESSION_ID";

        private const int ConnectTimeoutMs = 3000;
        private const int ResponseTimeoutMs = 35000;

        private readonly string _pipeName;
        private readonly string _nonce;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private NamedPipeClientStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        public string SessionId { get; }

        private LauncherTransport(string pipeName, string nonce, string sessionId)
        {
            _pipeName = pipeName;
            _nonce = nonce;
            SessionId = sessionId;
        }

        public static LauncherTransport TryCreateFromEnvironment()
        {
            var pipe = Environment.GetEnvironmentVariable(PipeVariable);
            var nonce = Environment.GetEnvironmentVariable(NonceVariable);
            var session = Environment.GetEnvironmentVariable(SessionVariable);

            if (string.IsNullOrEmpty(pipe) || string.IsNullOrEmpty(nonce))
                return null;

            return new LauncherTransport(pipe, nonce, session);
        }

        public LauncherTransport CreateSibling() => new LauncherTransport(_pipeName, _nonce, SessionId);

        public Task<string> SendAsync(string op, string extraFields) =>
            SendAsync(op, extraFields, ResponseTimeoutMs);

        public async Task<string> SendAsync(string op, string extraFields, int responseTimeoutMs)
        {
            var line =
                "{\"id\":" + AchievementJson.Quote(Guid.NewGuid().ToString("N"))
                + ",\"nonce\":" + AchievementJson.Quote(_nonce)
                + ",\"op\":" + AchievementJson.Quote(op)
                + (string.IsNullOrEmpty(extraFields) ? "" : "," + extraFields)
                + "}";

            await _gate.WaitAsync();
            try
            {
                Exception last = null;
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        await EnsureConnectedAsync();

                        var writer = _writer;
                        var reader = _reader;
                        var exchange = Task.Run(() =>
                        {
                            writer.WriteLine(line);
                            writer.Flush();
                            return reader.ReadLine();
                        });

                        var finished = await Task.WhenAny(exchange, Task.Delay(responseTimeoutMs));
                        if (finished != exchange)
                        {
                            Reset();
                            _ = exchange.ContinueWith(t => { var _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                            throw new TimeoutException("The launcher did not respond in time.");
                        }

                        var response = await exchange;
                        if (response == null)
                        {
                            Reset();
                            last = new IOException("The launcher closed the connection.");
                            continue;
                        }

                        return response;
                    }
                    catch (IOException ex)
                    {
                        Reset();
                        last = ex;
                    }
                }

                throw last ?? new IOException("Could not reach the launcher.");
            }
            finally
            {
                _gate.Release();
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (_stream != null && _stream.IsConnected)
                return;

            Reset();

            var stream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.None);
            try
            {
                await Task.Run(() => stream.Connect(ConnectTimeoutMs));
            }
            catch (TimeoutException)
            {
                stream.Dispose();
                throw new IOException("Timed out connecting to the launcher.");
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            _stream = stream;
            _reader = new StreamReader(stream, new UTF8Encoding(false));
            _writer = new StreamWriter(stream, new UTF8Encoding(false));
        }

        private void Reset()
        {
            try { _writer?.Dispose(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
            _writer = null;
            _reader = null;
            _stream = null;
        }

        public void Dispose()
        {
            Reset();
            _gate.Dispose();
        }
    }
}
