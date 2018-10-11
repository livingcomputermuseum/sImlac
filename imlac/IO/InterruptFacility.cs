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
    //
    // From the PDS-1 Technical manual:
    // The PDS-1 program interrupt facility, when enabled under program control (001 162),
    // allows the status flip-flop of an I/O device to force an interrupt upon the completion of the
    // current instruction, thereby alleviating the need for repeated flag checking by the main program.
    // An interrupt is the equivalent of a subroutine jump to memory location 0000.
    // ...
    // The interrupt causes the address of the next instruction of the main program to be stored in memory
    // location 0000, the next instruction to be taken from 0001, and the program interrupt facility to be
    // disabled. 
    //
    // The implementation here is overly simplistic -- on every clock, if enabled, we check the
    // implemented system devices to see if their status flags indicate pending data, if the mask enables
    // interrupts we do the ISR routine at 0000.
    //
    // The interrupt status bits are:
    // 15 - Light pen (LPA-1)
    // 14 - 40 Cycle Sync & End of Display Frame
    // 13 - Memory Protect (PMP-1)
    // 12 - TTY Receive
    // 11 - Keyboard
    // 10 - TTY send
    // 9  - Joystick, Mouse, or Trackball (JST-1, GMI-1, or TBL-1)
    // 8  - Tablet (TBI-1)
    // 7  - Punch (PTP-1)
    // 6  - Keyboard #2 (KYB-1)
    // 5  - TKA IN
    // 4  - TKA OUT (TKA-1)
    // 3  - 16 Bit input/PTR (GSI-1 or PTR-1)
    // 2  - Addressable clock w/input (ACI-1)
    // 1  - unused
    // 0  - Printer (PRT-1)
    //
    public class InterruptFacility : IIOTDevice
    {
        public InterruptFacility(ImlacSystem system)
        {
            _system = system;
        }

        public void Reset()
        {
            _interruptsEnabled = false;
            _interruptMask = 0;
            _interruptStatus = 0;
            _interruptPending = false;
        }

        public void Clock()
        {
            if (_interruptsEnabled)
            {
                // Collect up devices that want to interrupt us.
                _interruptStatus = 0;

                // bit 14: 40 cycle sync
                if (_system.DisplayProcessor.FrameLatch)
                {
                    _interruptStatus |= 0x0002;
                }

                // bit 12 - TTY rcv
                if (_system.TTY.DataReady)
                {
                    _interruptStatus |= 0x0008;
                }

                // bit 11 - keyboard
                if (_system.Keyboard.KeyReady)
                {
                    _interruptStatus |= 0x0010;
                }

                // bit 10 - TTY send
                if (_system.TTY.DataSentLatch)
                {
                    _interruptStatus |= 0x0020;
                }

                // bit 2 - ACI-1 (clock)
                if (_system.Clock.TimerTriggered)
                {
                    _interruptStatus |= 0x2000;
                }

                // mask it with our interrupt mask and if non-zero then we have a pending interrupt,
                // which we will execute at the start of the next CPU instruction.
                if ((_interruptMask & _interruptStatus) != 0)
                {
                    _interruptPending = true;
                }

                //
                // If we have an interrupt pending and the processor is starting the next instruction
                // we will interrupt now, otherwise we wait until the processor is ready.
                //
                if (_interruptPending && _system.Processor.InstructionState == ExecState.Fetch)
                {
                    // save the current PC at 0
                    _system.Memory.Store(0x0000, _system.Processor.PC);

                    // continue execution at 1
                    _system.Processor.PC = 0x0001;

                    // and disable further interrupts
                    _interruptsEnabled = false;
                    _interruptPending = false;

                    Trace.Log(LogType.Interrupt, "Interrupt triggered (for device(s) {0})", Helpers.ToOctal((ushort)(_interruptMask & _interruptStatus)));
                }
            }
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
                case 0x71:
                    _interruptsEnabled = false;
                    _interruptPending = false;
                    Trace.Log(LogType.Interrupt, "Interrupts disabled");
                    break;

                case 0x72:
                    _interruptsEnabled = true;
                    Trace.Log(LogType.Interrupt, "Interrupts enabled");
                    break;

                case 0x41:
                    _system.Processor.AC |= (ushort)(_interruptStatus);
                    Trace.Log(LogType.Interrupt, "Interrupt status {0} copied to AC", Helpers.ToOctal((ushort)_interruptStatus));
                    break;

                case 0x61:
                    _interruptMask = _system.Processor.AC;
                    Trace.Log(LogType.Interrupt, "Interrupt mask set to {0}", Helpers.ToOctal((ushort)_interruptMask));
                    break;


                default:
                    throw new NotImplementedException(String.Format("Unimplemented Interrupt IOT instruction {0:x4}", iotCode));
            }
        }

        private bool _interruptsEnabled;
        private bool _interruptPending;
        private int _interruptMask;
        private int _interruptStatus;

        private ImlacSystem _system;
        
        private readonly int[] _handledIOTs = 
            { 
                0x41,       // read interrupt status bits
                0x61,       // arm/disarm devices (set interrupt mask)
                0x71,       // IOF (disable interrupts)
                0x72,       // ION (enabled masked interrupts)
            };        
    }
}
