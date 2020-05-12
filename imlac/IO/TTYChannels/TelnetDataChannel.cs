using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace imlac.IO.TTYChannels
{

    public class TelnetDataChannel : ISerialDataChannel
    {
        public TelnetDataChannel(string server, int port, bool raw)
        {
            //
            // Try to open the channel.
            //
            try
            {
                _telnetStream = new TelnetStream(server, port, raw);
            }
            catch(Exception e)
            {
                Console.WriteLine("Failed to connect to {0}:{1}, error: {2}",
                    server, port, e.Message);

                _telnetStream = null;
            }
        }

        public void Reset()
        {
            // TODO: how to handle reset?
        }

        public void Close()
        {
            _telnetStream.Close();
            _telnetStream = null;
        }

        public byte Read()
        {
            byte b = (byte)_telnetStream.ReadByte();
            Trace.Log(LogType.Telnet, "r:{0:x2}({1}) ", b, (char)b);
            return b;
        }

        public void Write(byte value)
        {
            Trace.Log(LogType.Telnet, "w:{0:x2}({1})", value, (char)value);
            _telnetStream.WriteByte(value);
        }

        public bool DataAvailable
        {
            get
            {
                return _telnetStream != null ? _telnetStream.DataAvailable : false;
            }
        }

        public bool OutputReady
        {
            get
            {
                // Always return true, we can always send data
                return _telnetStream != null ? true : false;
            }
        }

        private TelnetStream _telnetStream;
    }


    public class TelnetStream
    {
        public TelnetStream(string host, int port, bool raw)
        {
            _tcpClient = new TcpClient(host, port);
            _tcpStream = _tcpClient.GetStream();
            _raw = raw;
            _abort = false;

            _asyncBuffer = new byte[2048];
            _inputBuffer = new Queue<byte>();

            _bufferLock = new ReaderWriterLockSlim();
            _streamLock = new ReaderWriterLockSlim();

            //
            // Kick off reading from the stream, asynchronously.
            _tcpStream.BeginRead(
                _asyncBuffer,
                0,
                _asyncBuffer.Length,
                new AsyncCallback(AsyncReadCallback),
                null);
        }

        public bool DataAvailable
        {
            get
            {
                bool avail = false;
                _bufferLock.EnterReadLock();
                avail = _inputBuffer.Count > 0;
                _bufferLock.ExitReadLock();

                return avail;
            }

        }

        public void Close()
        {
            _streamLock.EnterWriteLock();
            _abort = true;
            _tcpStream.Close();
            _tcpClient.Close();
            _streamLock.ExitWriteLock();
        }

        public byte ReadByte()
        {
            byte b = 0;
            _bufferLock.EnterUpgradeableReadLock();
            if (_inputBuffer.Count > 0)
            {
                _bufferLock.EnterWriteLock();
                b = _inputBuffer.Dequeue();
                _bufferLock.ExitWriteLock();
            }

            _bufferLock.ExitUpgradeableReadLock();

            return b;
        }

        public void WriteByte(byte b)
        {
            _tcpStream.WriteByte(b);
        }

        private void AsyncReadCallback(IAsyncResult ar)
        {
            _streamLock.EnterReadLock();

            if (_abort)
            {
                return;
            }

            //
            // Process incoming data
            // TODO: The telnet processing is terrible.
            //            
            int bytesRead = _tcpStream.EndRead(ar);

            for (int i = 0; i < bytesRead; )
            {
                byte b = _asyncBuffer[i++];
                
                if (!_raw && b == IAC)
                {
                    // For now we just eat all option requests.
                    b = _asyncBuffer[i++];

                    switch(b)
                    {
                        case WILL:
                        case WONT:
                        case DO:
                        case DONT:
                            i++;
                            break;

                        default:
                            break;
                    }
                }
                else 
                {
                    _bufferLock.EnterWriteLock();
                    _inputBuffer.Enqueue(b);
                    _bufferLock.ExitWriteLock();
                }
            }

            //
            // And start the next read
            //            
            _tcpStream.BeginRead(
                _asyncBuffer,
                0,
                _asyncBuffer.Length,
                new AsyncCallback(AsyncReadCallback),
                null);

            _streamLock.ExitReadLock();
        }

        private const byte IAC = 255;
        private const byte WILL = 251;
        private const byte WONT = 252;
        private const byte DO = 253;
        private const byte DONT = 254;

        private byte[] _asyncBuffer;

        private Queue<byte> _inputBuffer;

        private TcpClient _tcpClient;
        private NetworkStream _tcpStream;
        private bool _raw;
        private bool _abort;

        private ReaderWriterLockSlim _bufferLock;
        private ReaderWriterLockSlim _streamLock;
    }
}
