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

using imlac.IO;
using imlac.Debugger;

namespace imlac
{

    /// <summary>
    /// DisplayProcessor implements the Display processor found in an Imlac PDS-1 with long vector hardware.
    /// </summary>
    public class PDS1DisplayProcessor : DisplayProcessorBase
    {
        public PDS1DisplayProcessor(ImlacSystem system) : base(system)
        {

        }

        public override void Reset()
        {
            base.Reset();

            _sgrModeOn = false;
            _sgrBeamOn = false;
            _sgrDJRMOn = false;
        }

        public override void InitializeCache()
        {
            _instructionCache = new PDS1DisplayInstruction[Memory.Size];
        }

        public override void InvalidateCache(ushort address)
        {
            _instructionCache[address & Memory.SizeMask] = null;
        }

        public override string Disassemble(ushort address, DisplayProcessorMode mode, out int length)
        {
            //
            // Return a precached instruction if we have it due to previous execution
            // otherwise disassemble it now in the requested mode; this disassembly 
            // does not get added to the cache.
            //
            if (_instructionCache[address & Memory.SizeMask] != null)
            {
                return _instructionCache[address & Memory.SizeMask].Disassemble(mode, _mem, out length);
            }
            else
            {
                return new PDS1DisplayInstruction(_mem.Fetch(address), address, mode).Disassemble(mode, _mem, out length);
            }
        }

        public override void Clock()
        {
            _clocks++;

            if (_clocks > _frameClocks40Hz)
            {
                _clocks = 0;
                _frameLatch = true;
                _system.Display.FrameDone();
            }

            if (_state == ProcessorState.Halted)
            {
                return;
            }

            switch (_mode)
            {
                case DisplayProcessorMode.Processor:
                    ExecuteProcessor();
                    break;

                case DisplayProcessorMode.Increment:
                    ExecuteIncrement();
                    break;
            }
        }

        public override int[] GetHandledIOTs()
        {
            return _handledIOTs;
        }

        public override void ExecuteIOT(int iotCode)
        {
            //
            // Dispatch the IOT instruction.
            //
            switch (iotCode)
            {
                case 0x03:      // DLA: 
                    PC = _system.Processor.AC;

                    // this is for debugging only, we keep track of the load address
                    // to make it easy to see where the main Display List starts
                    _dpcEntry = PC;
                    break;

                case 0x0a:      // Halt display processor
                    HaltProcessor();
                    break;

                case 0x39:      // Clear display 40Hz sync latch
                    _frameLatch = false;
                    break;

                case 0xc4:      // clear halt state
                    _halted = false;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented Display IOT instruction {0:x4}", iotCode));
            }
        }

        private void ExecuteProcessor()
        {
            PDS1DisplayInstruction instruction = GetCachedInstruction(_pc, DisplayProcessorMode.Processor);
            instruction.UsageMode = DisplayProcessorMode.Processor;

            switch (instruction.Opcode)
            {
                case DisplayOpcode.DEIM:
                    _mode = DisplayProcessorMode.Increment;
                    _immediateWord = instruction.Data;
                    _immediateHalf = ImmediateHalf.Second;
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Enter increment mode");
                    break;

                case DisplayOpcode.DJMP:
                    if (!_dadr)
                    {
                        // DADR off, use only 12 bits
                        _pc = (ushort)((instruction.Data & 0xfff) | _block);
                    }
                    else
                    {
                        _pc = (ushort)(instruction.Data | _block);
                    }
                    break;

                case DisplayOpcode.DJMS:
                    Push();                    

                    if (!_dadr)
                    {
                        // DADR off, use only 12 bits
                        _pc = (ushort)((instruction.Data & 0xfff) | _block);
                    }
                    else
                    {
                        _pc = (ushort)(instruction.Data | _block);
                    }
                    break;

                case DisplayOpcode.DOPR:
                    // Each of bits 4-11 can be combined in any fashion
                    // to do a number of operations simultaneously; we walk the bits
                    // and perform the operations as set.
                    if ((instruction.Data & 0x800) == 0)
                    {
                        // DHLT -- halt the display processor.  other micro-ops in this
                        // instruction are still run.
                        HaltProcessor();
                    }

                    if ((instruction.Data & 0x400) != 0)
                    {
                        // HV Sync; this is currently a no-op, not much to do in emulation.
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "HV Sync");

                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                    }

                    if ((instruction.Data & 0x200) != 0)
                    {
                        // DIXM -- increment X DAC MSB
                        X += 0x20;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DIXM, X is now {0}", X);
                    }

                    if ((instruction.Data & 0x100) != 0)
                    {
                        // DIYM -- increment Y DAC MSB
                        Y += 0x20;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DIYM, Y is now {0}", Y);
                    }

                    if ((instruction.Data & 0x80) != 0)
                    {
                        // DDXM - decrement X DAC MSB
                        X -= 0x20;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DDXM, X is now {0}", X);
                    }

                    if ((instruction.Data & 0x40) != 0)
                    {
                        // DDYM - decrement y DAC MSB
                        Y -= 0x20;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DDYM, Y is now {0}", Y);
                    }

                    if ((instruction.Data & 0x20) != 0)
                    {
                        // DRJM - return from display subroutine
                        ReturnFromDisplaySubroutine();
                        _pc--;  // hack (we add +1 at the end...)
                    }

                    if ((instruction.Data & 0x10) != 0)
                    {
                        // DDSP -- intensify point on screen for 1.8us (one instruction)
                        // at the current position.
                        _system.Display.DrawPoint(X, Y);

                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DDSP at {0},{1}", X, Y);
                    }

                    // F/C ops:
                    int f = (instruction.Data & 0xc) >> 2;
                    int c = instruction.Data & 0x3;

                    switch (f)
                    {
                        case 0x0:
                            // if bit 15 is set, the MIT mods flip the DADR bit.
                            if (Configuration.MITMode && (c == 1))
                            {
                                _dadr = !_dadr;
                            }
                            break;

                        case 0x1:
                            // Set scale based on C
                            switch (c)
                            {
                                case 0:
                                    _scale = 1.0f;
                                    break;

                                default:
                                    _scale = c;
                                    break;
                            }
                            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Scale set to {0}", _scale);
                            break;

                        case 0x2:
                            if (!Configuration.MITMode)
                            {
                                _block = (ushort)(c << 12);
                            }
                            break;

                        case 0x3:
                            // TODO: light pen sensitize
                            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Light pen, stub!");
                            break;
                    }

                    _pc++;
                    break;

                case DisplayOpcode.DLXA:
                    X = instruction.Data << 1;

                    DrawingMode mode;
                    if (_sgrModeOn && _sgrBeamOn)
                    {
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "SGR-1 X set to {0}", X);
                        mode = DrawingMode.SGR1;
                    }
                    else
                    {
                        mode = DrawingMode.Off;
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "X set to {0}", X);
                    }

                    _system.Display.MoveAbsolute(X, Y, mode);
                    
                    if (_sgrDJRMOn)
                    {
                        ReturnFromDisplaySubroutine();
                    }
                    else
                    {
                        _pc++;
                    }
                    break;

                case DisplayOpcode.DLYA:                    
                    Y = instruction.Data << 1;
                    
                    if (_sgrModeOn && _sgrBeamOn)
                    {
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "SGR-1 Y set to {0}", Y);
                        mode = DrawingMode.SGR1;
                    }
                    else
                    {
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Y set to {0}", Y);
                        mode = DrawingMode.Off;
                    }

                    _system.Display.MoveAbsolute(X, Y, mode);

                    if (_sgrDJRMOn)
                    {
                        ReturnFromDisplaySubroutine();
                    }
                    else
                    {
                        _pc++;
                    }
                    break;
                
                case DisplayOpcode.DLVH:
                    DrawLongVector(instruction.Data);
                    break;

                case DisplayOpcode.SGR1:
                    _sgrModeOn = (instruction.Data & 0x1) != 0;
                    _sgrDJRMOn = (instruction.Data & 0x2) != 0;
                    _sgrBeamOn = (instruction.Data & 0x4) != 0;

                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "SGR-1 instruction: Enter {0} BeamOn {1} DRJM {2}", _sgrModeOn, _sgrBeamOn, _sgrDJRMOn);
                    _pc++;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented Display Processor Opcode {0}, operands {1}", Helpers.ToOctal((ushort)instruction.Opcode), Helpers.ToOctal(instruction.Data)));
            }

            // If the next instruction has a breakpoint set we'll halt at this point, before executing it.
            if (BreakpointManager.TestBreakpoint(BreakpointType.Display, _pc))
            {
                _state = ProcessorState.BreakpointHalt;
            }
        }

        private void ExecuteIncrement()
        {
            int halfWord = _immediateHalf == ImmediateHalf.First ? (_immediateWord & 0xff00) >> 8 : (_immediateWord & 0xff);

            int newX = (int)X;
            int newY = (int)Y;

            // translate the half word to vector movements or escapes
            if ((halfWord & 0x80) == 0)
            {
                if ((halfWord & 0x40) != 0)
                {
                    // Escape code
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment mode escape on halfword {0}", _immediateHalf);
                    _mode = DisplayProcessorMode.Processor;
                    _pc++;  // move to next word

                    // Moved this into this check (not sure it makes sense to do a DJMS when not escaped from Increment mode)
                    if ((halfWord & 0x20) != 0)
                    {
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment mode return from subroutine.");
                        ReturnFromDisplaySubroutine();
                    }
                }
                else
                {
                    // Stay in increment mode.
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment instruction, non-drawing.");
                    MoveToNextHalfWord();
                }
                
                if ((halfWord & 0x10) != 0)
                {
                    newX += 0x20;
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment X MSB, X is now {0}", X);
                }

                if ((halfWord & 0x08) != 0)
                {
                    newX = newX & (0xffe0);
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Reset X LSB, X is now {0}", X);
                }

                if ((halfWord & 0x02) != 0)
                {
                    newY += 0x20;
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment Y MSB, Y is now {0}", Y);
                }

                if ((halfWord & 0x01) != 0)
                {
                    newY = newY & (0xffe0);
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Reset Y LSB, Y is now {0}", Y);
                }
                
                _system.Display.MoveAbsolute(newX, newY, DrawingMode.Off);
                
            }
            else
            {
                int xSign = ((halfWord & 0x20) == 0) ? 1 : -1;
                int xMag = (int)(((halfWord & 0x18) >> 3) * _scale);

                int ySign = (int)(((halfWord & 0x04) == 0) ? 1 : -1);
                int yMag = (int)((halfWord & 0x03) * _scale);

                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, 
                    "Inc mode ({0}:{1}), x={2} y={3} dx={4} dy={5} beamon {6}", 
                    Helpers.ToOctal((ushort)_pc), 
                    Helpers.ToOctal((ushort)halfWord), 
                    newX, 
                    newY, 
                    xSign * xMag, 
                    ySign * yMag, 
                    (halfWord & 0x40) != 0);

                newX = (int)(newX + xSign * xMag * 2);
                newY = (int)(newY + ySign * yMag * 2);

                _system.Display.MoveAbsolute(newX, newY, (halfWord & 0x40) == 0 ? DrawingMode.Off : DrawingMode.Dotted);

                MoveToNextHalfWord();
            }

            // Assign back to X, Y registers; clipping into range.
            X = newX;
            Y = newY;

            // If the next instruction has a breakpoint set we'll halt at this point, before executing it.
            if (_immediateHalf == ImmediateHalf.First && BreakpointManager.TestBreakpoint(BreakpointType.Display, _pc))
            {
                _state = ProcessorState.BreakpointHalt;
            }
        }

        private void MoveToNextHalfWord()
        {           
            if (_immediateHalf == ImmediateHalf.Second)
            {
                _pc++;
                _immediateWord = _mem.Fetch(_pc);
                _immediateHalf = ImmediateHalf.First;

                // Update the instruction cache with the type of instruction (to aid in debugging).
                PDS1DisplayInstruction instruction = GetCachedInstruction(_pc, DisplayProcessorMode.Increment);                
            }
            else
            {
                _immediateHalf = ImmediateHalf.Second;
            }
        }

        private void DrawLongVector(ushort word0)
        {
            //
            // A Long Vector instruction is 3 words long:
            // Word 0: upper 4 bits indicate the opcode (4), lower 12 specify N-M
            // Word 1: upper 3 bits specify beam options (dotted, solid, etc) and the lower 12 specify the larger increment "M"
            // Word 2: upper 3 bits specify signs, lower 12 specify the smaller increment "N"
            // M is the larger absolute value between dX and dY
            // N is the smaller.

            //
            // Unsure at the moment what the N-M bits are for (I'm guessing they're there to help the processor figure things out).
            // Also unsure what bits are used in the 12 bits for N and M (the DACs are only 11-bits, but normally only 10 can be specified)...
            //
            ushort word1 = _mem.Fetch(++_pc);
            ushort word2 = _mem.Fetch(++_pc);

            uint M = (uint)(word1 & 0x3ff);
            uint N = (uint)(word2 & 0x3ff);

            bool beamOn = (word1 & 0x2000) != 0;
            bool dotted = (word1 & 0x4000) != 0;

            int dySign = (word2 & 0x2000) != 0 ? -1 : 1;
            int dxSign = (word2 & 0x4000) != 0 ? -1 : 1;
            bool dyGreater = (word2 & 0x1000) != 0;
 
            uint dx = 0;
            uint dy = 0;

            if (dyGreater)
            {
                dy = M;
                dx = N;
            }
            else
            {
                dx = M;
                dy = N;
            }

            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "LongVector x={0} y={1} dx={2} dy={3} beamOn {4} dotted {5}", X, Y, dx * dxSign, dy * dySign, beamOn, dotted);

            // * 2 for translation to 11-bit space
            // The docs don't call this out, but the scale setting used in increment mode appears to apply
            // to the LVH vectors as well.  (Maze appears to rely on this.)
            int newX = (int)(X + (dx * dxSign) * 2 * _scale);
            int newY = (int)(Y + (dy * dySign) * 2 * _scale);

            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "LongVector, move complete - x={0} y={1}", newX, newY, dx * dxSign, dy * dySign, beamOn, dotted);

            _system.Display.MoveAbsolute(newX, newY, beamOn ? (dotted ? DrawingMode.Dotted : DrawingMode.Normal) : DrawingMode.Off);

            // Assign back to X, Y registers; clipping into range.
            X = newX;
            Y = newY;

            _pc++;
        }

        private void ReturnFromDisplaySubroutine()
        {
            Pop();
        }

        private PDS1DisplayInstruction GetCachedInstruction(ushort address, DisplayProcessorMode mode)
        {
            if (_instructionCache[address & Memory.SizeMask] == null)
            {
                _instructionCache[address & Memory.SizeMask] = new PDS1DisplayInstruction(_mem.Fetch(address), address, mode);
            }

            return _instructionCache[address & Memory.SizeMask];
        }

        // SGR-1 mode switches
        private bool _sgrModeOn; 
        private bool _sgrDJRMOn; 
        private bool _sgrBeamOn;

        protected const int _frameClocks40Hz = 13889;     // cycles per 1/40th of a second (rounded up) for a 1.8uS clock speed.

        private PDS1DisplayInstruction[] _instructionCache;

        private readonly int[] _handledIOTs = { 0x3, 0xa, 0x39, 0xc4 };


        /// <summary>
        /// PDS-1 Display instruction decoder and disassembler.
        /// </summary>
        private class PDS1DisplayInstruction : DisplayInstructionBase
        {
            public PDS1DisplayInstruction(ushort word, ushort address, DisplayProcessorMode mode) : base (word, address, mode)
            {
            }

            public override string Disassemble(DisplayProcessorMode mode, Memory mem, out int length)
            {
                if (mode == DisplayProcessorMode.Indeterminate)
                {
                    mode = _usageMode;
                }

                switch (mode)
                {
                    case DisplayProcessorMode.Increment:
                        length = 1;
                        return DisassembleIncrement();

                    case DisplayProcessorMode.Processor:
                        return DisassembleProcessor(mem, out length);

                    case DisplayProcessorMode.Indeterminate:
                        length = 1;
                        return "Indeterminate";

                    default:
                        throw new InvalidOperationException(String.Format("{0} is not a supported disassembly mode for this processor.", mode));
                }
            }

            protected override void Decode()
            {
                if (_usageMode == DisplayProcessorMode.Processor)
                {
                    DecodeProcessor();
                }
                else
                {
                    DecodeImmediate();
                }
            }

            private void DecodeProcessor()
            { 
                int op = (_word & 0x7000) >> 12;

                switch (op)
                {
                    case 0x00:
                        // opr code
                        _opcode = DisplayOpcode.DOPR;
                        _data = (ushort)(_word & 0xfff);
                        break;

                    case 0x01:
                        _opcode = DisplayOpcode.DLXA;
                        _data = (ushort)(_word & 0x3ff);
                        break;

                    case 0x02:
                        _opcode = DisplayOpcode.DLYA;
                        _data = (ushort)(_word & 0x3ff);
                        break;

                    case 0x03:
                        _opcode = DisplayOpcode.DEIM;
                        _data = (ushort)(_word & 0xff);

                        if ((_word & 0xff00) == 0x3800)
                        {
                            Console.WriteLine("PPM-1 not implemented (instr {0})", Helpers.ToOctal(_word));
                        }
                        break;

                    case 0x04:
                        _opcode = DisplayOpcode.DLVH;
                        _data = (ushort)(_word & 0xfff);
                        break;

                    case 0x05:
                        _opcode = DisplayOpcode.DJMS;
                        _data = (ushort)(_word & 0xfff);

                        if (Configuration.MITMode && (_word & 0x8000) != 0)
                        {
                            // MIT's mod takes the MSB of the address from the MSB of the instruction word
                            _data |= 0x1000;
                        }
                        break;

                    case 0x06:
                        _opcode = DisplayOpcode.DJMP;
                        _data = (ushort)(_word & 0xfff);

                        if (Configuration.MITMode && (_word & 0x8000) != 0)
                        {
                            // MIT's mod takes the MSB of the address from the MSB of the instruction word
                            _data |= 0x1000;
                        }
                        break;

                    case 0x07:
                        DecodeExtendedInstruction(_word);
                        break;

                    default:
                        throw new NotImplementedException(String.Format("Unhandled Display Processor Mode instruction {0}", Helpers.ToOctal(_word)));
                }
            }

            void DecodeExtendedInstruction(ushort word)
            {
                int op = (word & 0x1f8) >> 3;

                switch (op)
                {
                    case 0x36:
                    case 0x37:
                        _opcode = DisplayOpcode.ASG1;
                        break;

                    case 0x3a:
                    case 0x3b:
                        _opcode = DisplayOpcode.VIC1;
                        break;

                    case 0x3c:
                    case 0x3d:
                        _opcode = DisplayOpcode.MCI1;
                        break;

                    case 0x3e:
                        _opcode = DisplayOpcode.STI1;
                        break;

                    case 0x3f:
                        _opcode = DisplayOpcode.SGR1;
                        break;

                    default:
                        throw new NotImplementedException(String.Format("Unhandled extended Display Processor Mode instruction {0}", Helpers.ToOctal(word)));
                }

                _data = (ushort)(word & 0x7);
                
            }

            private void DecodeImmediate()
            {
                // TODO: eventually actually precache movement calculations.
            }

            protected override string DisassembleExtended(Memory mem, out int length)
            {
                string ret = String.Empty;
                switch (_opcode)
                {
                    case DisplayOpcode.DLVH:
                        length = 3;
                        ret = DisassembleLongVector(mem);
                        break;

                    default:
                        length = 1;
                        // Handle as yet not-special-cased opcodes
                        ret = String.Format("{0} {1}", _opcode, Helpers.ToOctal(_data));
                        break;
                }

                return ret;
            }

            private string DisassembleLongVector(Memory mem)
            {
                //
                // A Long Vector instruction is 3 words long:
                // Word 0: upper 4 bits indicate the opcode (4), lower 12 specify N-M
                // Word 1: upper 3 bits specify beam options (dotted, solid, etc) and the lower 12 specify the larger increment "M"
                // Word 2: upper 3 bits specify signs, lower 12 specify the smaller increment "N"
                // M is the larger absolute value between dX and dY
                // N is the smaller.
                //
                // TODO: Would make sense to precache this during decoding, would require
                // modifications to cache invalidation logic.
                ushort word1 = mem.Fetch((ushort)(_address + 1));
                ushort word2 = mem.Fetch((ushort)(_address + 2));

                uint M = (uint)(word1 & 0x3ff);
                uint N = (uint)(word2 & 0x3ff);

                bool beamOn = (word1 & 0x2000) != 0;
                bool dotted = (word1 & 0x4000) != 0;

                int dySign = (word2 & 0x2000) != 0 ? -1 : 1;
                int dxSign = (word2 & 0x4000) != 0 ? -1 : 1;
                bool dyGreater = (word2 & 0x1000) != 0;

                uint dx = 0;
                uint dy = 0;

                if (dyGreater)
                {
                    dy = M;
                    dx = N;
                }
                else
                {
                    dx = M;
                    dy = N;
                }

                return String.Format("DLVH ({0},{1}) {2} {3}",
                    dx * dxSign, dy * dySign,
                    beamOn ? "ON" : "OFF",
                    dotted ? "DOTTED" : String.Empty);
            }
        }
    }
}
