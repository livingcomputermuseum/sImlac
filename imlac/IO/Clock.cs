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

namespace imlac.IO
{
    /// <summary>
    /// Implements the ACI-1 "Addressable Clock With Interrupt" option
    /// </summary>
    public class AddressableClock : IIOTDevice
    {
        public AddressableClock(ImlacSystem system)
        {
            _system = system;

            Reset();
        }

        public void Reset()
        {
            _timerTriggered = false;
            _timerCount = _timerInit = 0;
        }

        public void Clock()
        {
            if (_timerInit > 0)
            {
                if (_timerCount > 0)
                {
                    _timerTriggered = false;
                    _timerCount--;
                }

                if (_timerCount == 0)
                {
                    _timerTriggered = true;
                    _timerCount = _timerInit;
                }
            }
        }

        public int[] GetHandledIOTs()
        {
            return _handledIOTs;
        }

        public bool TimerTriggered
        {
            get { return _timerTriggered; }
        }

        public void ExecuteIOT(int iotCode)
        {
            //
            // Dispatch the IOT instruction.
            //
            switch (iotCode)
            {
                case 0x51:
                    _timerCount = _timerInit = _system.Processor.AC;
                    _timerTriggered = false;
                    break;

                case 0x52:
                    _timerTriggered = false;
                    break;

                case 0x54:
                    if (_timerTriggered)
                    {
                        _system.Processor.PC++;
                    }
                    // TODO: does this reset status?
                    break;

                case 0x69:
                    _system.Processor.AC |= (ushort)_timerCount;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented ACI-1 IOT instruction {0:x4}", iotCode));
            }
        }

        private bool _timerTriggered;
        private int _timerCount;
        private int _timerInit;

        private ImlacSystem _system;
        
        private readonly int[] _handledIOTs = 
            { 
                0x51,       // load timer from AC
                0x52,       // Clear timer status
                0x54,       // Skip if timer status = 1
                0x69,       // read timer to AC
            };        
    }
}
