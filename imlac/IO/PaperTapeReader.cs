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
using System.IO;

namespace imlac.IO
{
    public class PaperTapeReader : IIOTDevice
    {
        public PaperTapeReader(ImlacSystem system)
        {
            _system = system;
        }

        public void Reset()
        {
            _tapeContents = null;
            _tapeIndex = 0;
            _dataReady = false;

            _state = ReaderState.Stopped;
            _clocks = 0;
        }

        public void Clock()
        {
            if (_tapeContents != null && _state == ReaderState.Running)
            {
                _clocks++;

                if (_clocks > _tapeAdvanceClocks)
                {
                    _clocks = 0;

                    if (_tapeIndex < _tapeContents.Length)
                    {
                        _dataReady = !_dataReady;

                        if (!_dataReady)
                        {
                            _tapeIndex++;
                            Console.Write(":");
                        }
                    }
                    else
                    {
                        _dataReady = false;
                    }
                }
            }
        }

        public void LoadTape(string path)
        {
            _state = ReaderState.Stopped;
            _tapeIndex = 0;

            FileStream fs = File.OpenRead(path);
            _tapeContents = new byte[fs.Length];

            fs.Read(_tapeContents, 0, (int)fs.Length);

            fs.Close();
        }

        public int[] GetHandledIOTs()
        {
            return _handledIOTs;
        }

        public void ExecuteIOT(int iotCode)
        {
            //
            // Dispatch the IOT instruction.
            //
            switch (iotCode)
            {
                case 0x29:      // Read -- contents of tape are OR'd into the processor's AC.
                    if (_tapeIndex < _tapeContents.Length)
                    {
                        _system.Processor.AC |= _tapeContents[_tapeIndex];

                        Trace.Log(LogType.PTR, "PTR read {0:x2}", _tapeContents[_tapeIndex]);
                    }
                    else
                    {
                        Trace.Log(LogType.PTR, "PTR read past end of tape.");
                    }
                    break;

                case 0x2a:      // Halt
                    _state = ReaderState.Stopped;
                    Trace.Log(LogType.PTR, "PTR stopped.");
                    _dataReady = false;
                    break;

                case 0x31:      // Start
                    _state = ReaderState.Running;
                    Trace.Log(LogType.PTR, "PTR started.");
                    _dataReady = false;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented PTR IOT instruction {0:x4}", iotCode));
            }
        }

        public bool DataReady()
        {
            return _dataReady;
        }       

        private ImlacSystem _system;
        private ReaderState _state;
        private byte[] _tapeContents;
        private int _tapeIndex;
        private bool _dataReady;
        private int _clocks;

        private const int _tapeAdvanceClocks = 1000; // fudged

        private readonly int[] _handledIOTs = { 0x29, 0x2a, 0x31 };

        private enum ReaderState
        {
            Stopped,
            Running
        }
    }
}
