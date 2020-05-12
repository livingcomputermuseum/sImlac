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
    public class Helpers
    {
        public static string ToOctal(ushort value)
        {
            return ToOctal(value, 6);
        }

        public static string ToOctal(ushort value, int digits)
        {
            string octalString = Convert.ToString(value, 8);
            return new String('0', digits - octalString.Length) + octalString;
        }

        public static ushort GetUshortForOctalString(string octal)
        {
            ushort value = Convert.ToUInt16(octal, 8);
            return value;
        }

        public static void SignalError(LogType logtype, string format, params object[] args)
        {
            if (Configuration.HaltOnInvalidOpcodes)
            {
                throw new NotImplementedException(String.Format(format, args));
            }
            else
            {
                if (Trace.TraceOn) Trace.Log(logtype, format, args);
            }
        }
    }
}
