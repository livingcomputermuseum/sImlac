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

    /// <summary>
    /// ISerialDataChannel implementation that provides a bit-bucket.
    /// </summary>
    public class NullDataChannel : ISerialDataChannel
    {
        public NullDataChannel()
        {
            
        }

        public void Reset()
        {

        }

        public void Close()
        {

        }

        public byte Read()
        {
            return 0;
        }

        public void Write(byte value)
        {
            
        }

        public bool DataAvailable
        {
            get
            {
                return false;
            }
        }

        public bool OutputReady
        {
            get
            {
                // Always return true, bits just go into the bucket.
                return true;
            }
        }
    }
}
