/*  
    This file is part of sImlac.

    sImlac is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    sImlac is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with sImlac.  If not, see <http://www.gnu.org/licenses/>.
*/

using System.IO.Ports;

namespace imlac.IO.TTYChannels
{
    /// <summary>
    /// An implementation of ISerialDataChannel over a real RS232 port on the host machine.
    /// </summary>
    public class SerialDataChannel : ISerialDataChannel
    {
        public SerialDataChannel(SerialPort port)
        {
            _serialPort = port;
            _serialPort.WriteTimeout = 10;
        }

        public void Reset()
        {
            // Flush our buffers
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();
        }

        public void Close()
        {
            _serialPort.Close();
        }

        public byte Read()
        {
            return (byte)_serialPort.ReadByte();
        }

        public void Write(byte value)
        {
            // Really.  You have a ReadByte function but no analog for Write?
            _serialPort.Write(new byte[] { value }, 0, 1);
        }

        public bool DataAvailable
        {
            get { return _serialPort.BytesToRead > 0; }
        }

        public bool OutputReady
        {
            // Always true
            get { return true; }
        }

        private SerialPort _serialPort;
    }
}
