using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace imlac.IO.TTYChannels
{

    public class TelnetDataChannel : ISerialDataChannel
    {
        public TelnetDataChannel(string server, int port)
        {
            //
            // Try to open the channel.
            //
            try
            {
                _telnetStream = new TelnetStream(server, port);
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
        public TelnetStream(string host, int port)
        {
            _tcpClient = new TcpClient(host, port);
            _tcpStream = _tcpClient.GetStream();

            _asyncBuffer = new byte[2048];
            _inputBuffer = new Queue<byte>();

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
            get { return _inputBuffer.Count > 0; }
        }

        public void Close()
        {
            _tcpStream.Close();
            _tcpClient.Close();
        }

        public byte ReadByte()
        {
            if (_inputBuffer.Count > 0)
            {
                return _inputBuffer.Dequeue();
            }
            else
            {
                return 0;
            }
        }

        public void WriteByte(byte b)
        {
            _tcpStream.WriteByte(b);
        }

        private void AsyncReadCallback(IAsyncResult ar)
        {
            //
            // Process incoming data
            // TODO: this is terrible.
            //            
            int bytesRead = _tcpStream.EndRead(ar);

            for (int i = 0; i < bytesRead; )
            {
                byte b = _asyncBuffer[i++];
                
                if (b == IAC)
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
                    _inputBuffer.Enqueue(b);
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
    }
}
