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

namespace imlac
{
    public class Memory
    {
        public Memory(ImlacSystem system)
        {            
            _system = system;
            SetMemorySize(0x2000);
        }

        public void SetMemorySize(ushort size)
        {
            if (size != 0x1000 && size != 0x2000 && size != 0x4000)
            {
                throw new InvalidOperationException("Size must be 4k, 8k, or 16k.");
            }

            _size = size;
            _sizeMask = (ushort)(size - 1);

            _mem = new ushort[Size];

            if (_system.Processor != null)
            {
                _system.Processor.InitializeCache();
            }

            if (_system.DisplayProcessor != null)
            {
                _system.DisplayProcessor.InitializeCache();
            }
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
            get { return _size; }
        }

        public static ushort SizeMask
        {
            get { return _sizeMask; }
        }

        private static ushort _size;
        private static ushort _sizeMask;

        private ushort[] _mem;
        private ImlacSystem _system;
    }
}