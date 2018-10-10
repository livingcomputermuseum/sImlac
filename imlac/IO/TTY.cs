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

using imlac.IO.TTYChannels;

namespace imlac.IO
{
    public class TTY : IIOTDevice
    {
        public TTY(ImlacSystem system)
        {
            _system = system;
            _dataChannel = new NullDataChannel();

            Reset();
        }

        public void Reset()
        {
            _dataSendReady = true;
            _dataReady = false;
            _clocks = 0;

            if (_dataChannel != null)
            {
                _dataChannel.Reset();
            }
        }

        public void SetChannel(ISerialDataChannel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            if (_dataChannel != null)
            {
                _dataChannel.Close();
            }

            _dataChannel = channel;
        }

        public void Clock()
        {
            _clocks++;

            if (_clocks > _dataClocks)
            {
                _clocks = 0;

                if (_dataChannel.DataAvailable && !_dataReady)
                {
                    _dataReady = true;
                    _data = _dataChannel.Read();
                    Trace.Log(LogType.TTY, "i");
                }
            }

            // Are we waiting to send something?
            if (!_dataSendReady && _dataChannel.OutputReady)
            {
                _dataChannel.Write(_data);
                Trace.Log(LogType.TTY, "o");

                // Sent, reset flag.
                _dataSendReady = true;
            }
        }

        public bool DataReady
        {
            get { return _dataReady; }
        }

        public bool DataSendReady
        {
            get { return _dataSendReady; }
        }

        public int[] GetHandledIOTs()
        {
            return _handledIOTs;
        }

        public void ExecuteIOT(int iotCode)
        {
            switch (iotCode)
            {
                case 0x19:  // RRB - TTY read     
                    Trace.Log(LogType.TTY, "TTY read {0}", Helpers.ToOctal(_data));
                    _system.Processor.AC |= _data;
                    break;

                case 0x1a:  // RCF - Clear TTY status
                    _dataReady = false;
                    break;

                case 0x1b:  // RRC - Read and clear status
                    Trace.Log(LogType.TTY, "TTY read {0}, status cleared.", Helpers.ToOctal(_data));
                    _dataReady = false;
                    _system.Processor.AC |= _data;
                    break;

                case 0x21:  // TPR - transmit
                    if (_dataSendReady) // only if transmitter is ready
                    {
                        _data = (byte)_system.Processor.AC;
                        _dataSendReady = false;
                    }
                    break;

                case 0x22:  // TCF - clear output flag
                    _dataSendReady = true;
                    break;

                case 0x23:  // TPC - print, clear flag
                    _data = (byte)_system.Processor.AC;
                    _dataSendReady = false;
                    break;

                default:
                    Trace.Log(LogType.TTY, "Stub: TTY xmit op", Helpers.ToOctal(_data));
                    break;
            }
        }

        private readonly int[] _handledIOTs = { 0x9, 0x19, 0x1a, 0x1b, 0x21, 0x22, 0x23 };

        private bool _dataReady;
        private bool _dataSendReady;
        private byte _data;

        private int _clocks;
        private readonly int _dataClocks = 100;

        private ISerialDataChannel _dataChannel;

        private ImlacSystem _system;
    }
}
