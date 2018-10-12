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

namespace imlac
{
    // TODO: make memory size configurable.
    public class Memory
    {
        public Memory(ImlacSystem system)
        {
            _mem = new ushort[Size];
            _system = system;
        }

        public ushort Fetch(ushort address)
        {
            ushort word = _mem[address & SizeMask];

            return word;
        }

        public void Store(ushort address, ushort word)
        {
            _mem[address & SizeMask] = word;

            // Invalidate processor caches
            _system.Processor.InvalidateCache(address);
            _system.DisplayProcessor.InvalidateCache(address);
        }

        public static ushort Size
        {
            get { return 0x4000; }
        }

        public static ushort SizeMask
        {
            get { return 0x3fff; }
        }       

        private ushort[] _mem;
        private ImlacSystem _system;
    }
}