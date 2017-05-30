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

using imlac.IO;

namespace imlac
{
    public enum DrawingMode
    {
        Off,
        Normal,
        Dotted,
        Hidden,
        SGR1,      // reduced intensity
        Point,     // increased intensity
        Debug,     // For debugging purposes
    }    

    /// <summary>
    /// IImlacConsole provides the interface for the components making up a standard
    /// "console" setup, which at the moment includes only the Display and Keyboard,
    /// but could be extended.
    /// </summary>
    public interface IImlacConsole
    {
        /// <summary>
        /// Indicates whether a key is currently pressed.
        /// </summary>
        bool IsKeyPressed
        {
            get;
        }

        /// <summary>
        /// Indicates the currently pressed key (if IsKeyPressed is true)
        /// </summary>
        ImlacKey Key
        {
            get;
        }

        /// <summary>
        /// Indicates any modifier bits (ctrl, shift) being pressed.
        /// </summary>
        ImlacKeyModifiers KeyModifiers
        {
            get;
        }

        /// <summary>
        /// Indicates the status of the front-panel data switches
        /// </summary>
        ushort DataSwitches
        {
            get;
        }

        bool ThrottleFramerate
        {
            get;
            set;
        }

        bool DataSwitchMappingEnabled
        {
            get;
            set;
        }

        DataSwitchMappingMode DataSwitchMode
        {
            get;
            set;
        }

        bool FullScreen
        {
            get;
            set;
        }

        void UnlatchKey();

        void ClearDisplay();

        void MoveAbsolute(uint x, uint y, DrawingMode mode);

        void DrawPoint(uint x, uint y);

        void RenderCurrent(bool completeFrame);

        void FrameDone();

        void SetScale(float scale);

        void MapDataSwitch(uint switchNumber, VKeys key);

        VKeys GetDataSwitchMapping(uint switchNumber);
    }
}
