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

namespace imlac.IO.TTYChannels
{
    public interface ISerialDataChannel
    {

        /// <summary>
        /// Resets the channel to initial state, if necessary.
        /// </summary>
        void Reset();

        /// <summary>
        /// Closes the channel.
        /// </summary>
        void Close();

        /// <summary>
        /// Reads a single byte from the channel.  
        /// Implementers may block if no data is ready.
        /// </summary>
        /// <returns></returns>
        byte Read();

        /// <summary>
        /// Writes a single byte to the channel.
        /// Implementers may block if the channel isn't ready to send.
        /// </summary>
        /// <param name="b"></param>
        void Write(byte b);

        /// <summary>
        /// Indicates that at least one byte is ready to be Read.
        /// </summary>
        bool DataAvailable { get; }

        /// <summary>
        /// Indicates that at least one byte can be transmitted.
        /// </summary>
        bool OutputReady { get; }
    }
}
