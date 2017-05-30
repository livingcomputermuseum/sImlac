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

namespace imlac.IO
{
    public interface IIOTDevice
    {
        /// <summary>
        /// Returns an array of 9-bit IOT instructions handled by this device.  (See below)
        /// </summary>
        /// <returns></returns>
        int[] GetHandledIOTs();

        /// <summary>
        /// Executes the specified IOT opcode.
        /// 
        /// iotCode is the 9 bits describing the device number and IOP code.  All 9 are required because
        /// despite it looking like the device code might group IOT instructions by device, it doesn't
        /// actually do so in any useful manner (for example device code 06 includes IOPs for the 
        /// Paper-tape reader, the TTY interface, and the Tablet.  Using the full 9 bits allows each
        /// device implementation to register for the IOT instructions rather than the device codes.
        /// 
        /// </summary>
        /// <param name="iotCode">The 9-bit IOT code to execute</param>
        void ExecuteIOT(int iotCode);        
    }
}
