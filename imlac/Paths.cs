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

using System.IO;

namespace imlac
{
    /// <summary>
    /// Defines the paths pointing to various resources used by the emulator.
    /// </summary>
    public static class Paths
    {
        public static string BuildBootPath(string file)
        {
            return Path.Combine(_boot, file);
        }

        private static string _boot = "boot";

    }
}
