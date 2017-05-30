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
using System.Collections.Generic;

namespace imlac.Debugger
{
    [Flags]
    public enum BreakpointType
    {
        None = 0,
        Execution = 1,
        Read = 2,
        Write = 4,
        Display = 8,
    }

    public struct BreakpointEntry
    {
        public BreakpointEntry(BreakpointType type, ushort address)
        {
            Type = type;
            Address = address;
        }

        public BreakpointType Type;
        public ushort Address;
    }

    public static class BreakpointManager
    {
        static BreakpointManager()
        {
            // Allocate enough breakpoint entries for a fully-stocked
            // 32KW machine.  We use a flat array to make lookup as
            // cheap as possible.
            _breakPoints = new BreakpointType[0x8000];
            _enableBreakpoints = false;
        }

        public static bool BreakpointsEnabled
        {
            get { return _enableBreakpoints; }
            set { _enableBreakpoints = value; }
        }

        public static void SetBreakpoint(BreakpointEntry entry)
        {
            if (entry.Type == BreakpointType.None)
            {
                _breakPoints[entry.Address & Memory.SizeMask] = BreakpointType.None;
            }
            else
            {
                _breakPoints[entry.Address & Memory.SizeMask] |= entry.Type;
            }
        }

        public static BreakpointType GetBreakpoint(ushort address)
        {
            return _breakPoints[address & Memory.SizeMask];
        }

        public static bool TestBreakpoint(BreakpointEntry entry)
        {
            if (!_enableBreakpoints)
            {
                return false;
            }

            return (_breakPoints[entry.Address & Memory.SizeMask] & entry.Type) != 0;
        }

        public static bool TestBreakpoint(BreakpointType type, ushort address)
        {
            if (!_enableBreakpoints)
            {
                return false;
            }

            return (_breakPoints[address & Memory.SizeMask] & type) != 0;
        }

        public static List<BreakpointEntry> EnumerateBreakpoints()
        {
            List<BreakpointEntry> breakpoints = new List<BreakpointEntry>();

            for(ushort i=0;i<_breakPoints.Length;i++)
            {
                if (_breakPoints[i] != BreakpointType.None)
                {
                    breakpoints.Add(new BreakpointEntry(_breakPoints[i], i));
                }
            }

            return breakpoints;
        }

        private static BreakpointType[] _breakPoints;
        private static bool _enableBreakpoints;
    }
}
