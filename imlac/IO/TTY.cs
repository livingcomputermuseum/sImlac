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
            _dataBufferFull = false;
            _dataSentLatch = false;
            _rxData = 0;
            _txData = 0;            

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
                    _rxData = _dataChannel.Read();
                    Trace.Log(LogType.TTY, "i");
                }

                // Are we waiting to send something?
                if (_dataBufferFull && _dataChannel.OutputReady)
                {
                    _dataChannel.Write(_txData);
                    Trace.Log(LogType.TTY, "o {0}", Helpers.ToOctal(_txData));
                    _dataBufferFull = false;
                    _dataSentLatch = true;
                    _dataSendReady = true;
                }
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

        public bool DataSentLatch
        {
            get
            {
                bool latch = _dataSentLatch;
                _dataSentLatch = false;
                return latch;
            }
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
                    Trace.Log(LogType.TTY, "TTY read {0}", Helpers.ToOctal(_rxData));
                    _system.Processor.AC |= _rxData;
                    break;

                case 0x1a:  // RCF - Clear TTY status
                    _dataReady = false;
                    break;

                case 0x1b:  // RRC - Read and clear status
                    Trace.Log(LogType.TTY, "TTY read {0}, status cleared.", Helpers.ToOctal(_rxData));
                    _dataReady = false;
                    _system.Processor.AC |= _rxData;
                    break;

                case 0x21:  // TPR - transmit
                    if (_dataSendReady)
                    {
                        _txData = (byte)_system.Processor.AC;
                        _dataSendReady = false;
                        _dataBufferFull = true;
                    }
                    break;

                case 0x22:  // TCF - clear output flag
                    _dataSendReady = false;
                    break;

                case 0x23:  // TPC - print, clear flag
                    if (_dataSendReady)
                    {
                        _txData = (byte)_system.Processor.AC;
                        _dataSendReady = false;
                        _dataBufferFull = true;
                    }
                    break;

                default:
                    Trace.Log(LogType.TTY, "Stub: TTY xmit op", Helpers.ToOctal(_rxData));
                    break;
            }
        }

        private readonly int[] _handledIOTs = { 0x9, 0x19, 0x1a, 0x1b, 0x21, 0x22, 0x23 };

        private bool _dataReady;
        private bool _dataSendReady;
        private bool _dataBufferFull;
        private bool _dataSentLatch;
        private byte _rxData;
        private byte _txData;

        private int _clocks;
        private readonly int _dataClocks = 90;      // Appx. 50kbps

        private ISerialDataChannel _dataChannel;

        private ImlacSystem _system;
    }
}
