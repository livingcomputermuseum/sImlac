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

using System;
using System.IO;

namespace imlac.IO.TTYChannels
{
    /// <summary>
    /// Implements an ISerialDataChannel that sources data to or from a Stream.    
    /// </summary>
    public class StreamDataChannel : ISerialDataChannel
    {        
        public StreamDataChannel(Stream data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            _dataStream = data;
        }

        public void Reset()
        {
            // Try to reposition if possible.
            if (_dataStream.CanSeek)
            {
                _dataStream.Seek(0, SeekOrigin.Begin);
            }
        }

        public void Close()
        {
            _dataStream.Close();
        }

        public byte Read()
        {
            return (byte)_dataStream.ReadByte();
        }

        public void Write(byte value)
        {
            //
            // Write if we can, no-op if not.
            //
            if (_dataStream.CanWrite)
            {
                _dataStream.WriteByte(value);
            }
            else
            {
                Trace.Log(LogType.TTY, "Dropped TTY output {0}", Helpers.ToOctal(value));
            }

        }

        public bool DataAvailable
        {
            get
            {
                return _dataStream.Position < _dataStream.Length;
            }
        }

        public bool OutputReady
        {
            get
            {
                // Always return true, even if the Stream doesn't support writing.
                return true;
            }
        }

        private Stream _dataStream;
    }
}
