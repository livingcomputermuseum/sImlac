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
    /// DisplayProcessor implements the Display processor found in an Imlac PDS-4.
    /// </summary>
    public class PDS4DisplayProcessor : DisplayProcessorBase
    {
        public PDS4DisplayProcessor(ImlacSystem system) : base(system)
        {

        }

        public override void Reset()
        {
            base.Reset();

            _fxyOn = false;
            _fxyBeamOn = false;
            _fxyDRJMOn = false;
        }

        public override void InitializeCache()
        {
            _instructionCache = new PDS4DisplayInstruction[Memory.Size];
        }

        public override void InvalidateCache(ushort address)
        {
            _instructionCache[address & Memory.SizeMask] = null;
        }

        public override string Disassemble(ushort address, DisplayProcessorMode mode)
        {
            //
            // Return a precached instruction if we have it due to previous execution
            // otherwise disassemble it now in the requested mode; this disassembly 
            // does not get added to the cache.
            //
            if (_instructionCache[address & Memory.SizeMask] != null)
            {
                return _instructionCache[address & Memory.SizeMask].Disassemble(mode);
            }
            else
            {
                return new PDS4DisplayInstruction((ushort)(address & Memory.SizeMask), mode).Disassemble(mode);
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
                case 0x01:      // DLA: load DPC with main processor's AC
                case 0x03:      // DLA: load DPC with main processor's AC and turn DP on.
                    PC = _system.Processor.AC;

                    // this is for debugging only, we keep track of the load address
                    // to make it easy to see where the main Display List starts
                    _dpcEntry = PC;

                    if (iotCode == 0x03)
                    {
                        State = ProcessorState.Running;
                        // MIT DADR bit gets reset when display is started.
                        _dadr = false;
                    }
                    break;

                case 0x02:      // Turn DP On.
                    State = ProcessorState.Running;
                    // MIT DADR bit gets reset when display is started.
                    _dadr = false;
                    break;

                case 0x0a:      // Halt display processor
                    State = ProcessorState.Halted;
                    break;

                case 0x39:      // Clear display 40Hz sync latch
                    _frameLatch = false;
                    break;

                case 0xc4:      // clear halt state
                    // TODO: what does this actually do?
                    //State = ProcessorState.Running;

                    // MIT DADR bit gets reset when display is started.
                    _dadr = false;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented Display IOT instruction {0:x4}", iotCode));
            }
        }

        private void ExecuteProcessor()
        {
            PDS4DisplayInstruction instruction = GetCachedInstruction(_pc, DisplayProcessorMode.Processor);
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
                        State = ProcessorState.Halted;
                    }

                    // Used to modify DSTS or DSTB operation
                    bool bit5 = (instruction.Data & 0x400) != 0;

                    if ((instruction.Data & 0x200) != 0)
                    {
                        // DIXM -- increment X DAC MSB
                        X += MSBIncrement;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DIXM, X is now {0}", X);
                    }

                    if ((instruction.Data & 0x100) != 0)
                    {
                        // DIYM -- increment Y DAC MSB
                        Y += MSBIncrement;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DIYM, Y is now {0}", Y);
                    }

                    if ((instruction.Data & 0x80) != 0)
                    {
                        // DDXM - decrement X DAC MSB
                        X -= MSBIncrement;
                        _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
                        if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "DDXM, X is now {0}", X);
                    }

                    if ((instruction.Data & 0x40) != 0)
                    {
                        // DDYM - decrement y DAC MSB
                        Y -= MSBIncrement;
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
                            // Set scale based on C and Bit 5.
                            _scale = c + (bit5 ? 4 : 0);

                            Console.WriteLine("scale {0}", _scale);
                            
                            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Scale set to {0}", _scale);
                            break;

                        case 0x2:
                            _block = (ushort)((c + (bit5 ? 4 : 0) << 12));
                            break;

                        case 0x3:
                            // TODO: light pen sensitize
                            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Light pen, stub!");
                            break;
                    }

                    _pc++;
                    break;

                case DisplayOpcode.DLXA:
                    X = instruction.Data;
                    
                    DrawingMode mode;
                    if (_fxyOn && _fxyBeamOn)
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
                    
                    if (_fxyDRJMOn)
                    {
                        ReturnFromDisplaySubroutine();
                    }
                    else
                    {
                        _pc++;
                    }
                    break;

                case DisplayOpcode.DLYA:
                    Y = instruction.Data;
                    
                    if (_fxyOn && _fxyBeamOn)
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

                    if (_fxyDRJMOn)
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

                case DisplayOpcode.DFXY:
                    _fxyOn = (instruction.Data & 0x1) != 0;
                    _fxyDRJMOn = (instruction.Data & 0x2) != 0;
                    _fxyBeamOn = (instruction.Data & 0x4) != 0;

                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "SGR-1 instruction: Enter {0} BeamOn {1} DRJM {2}", 
                        _fxyOn, 
                        _fxyBeamOn, 
                        _fxyDRJMOn);

                    _pc++;
                    break;

                default:
                    throw new NotImplementedException(String.Format("Unimplemented Display Processor Opcode {0}, ({1}), operands {1}", 
                        instruction.Opcode, 
                        Helpers.ToOctal((ushort)instruction.Opcode), 
                        Helpers.ToOctal(instruction.Data)));
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
            
            // translate the half word to vector movements or escapes
            if ((halfWord & 0x80) == 0)
            {
                // Control Byte:

                if ((halfWord & 0x40) != 0)
                {
                    // Escape code
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment mode escape on halfword {0}", _immediateHalf);
                    _mode = DisplayProcessorMode.Processor;
                    _pc++;  // move to next word
                    
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
                    X += MSBIncrement;
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment X MSB, X is now {0}", X);
                }

                if ((halfWord & 0x08) != 0)
                {
                    X = X & (MSBMask);
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Reset X LSB, X is now {0}", X);
                }

                if ((halfWord & 0x02) != 0)
                {
                    Y += MSBIncrement;
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Increment Y MSB, Y is now {0}", Y);
                }

                if ((halfWord & 0x01) != 0)
                {
                    Y = Y & (MSBMask);
                    if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Reset Y LSB, Y is now {0}", Y);
                }
                
                _system.Display.MoveAbsolute(X, Y, DrawingMode.Off);
            }
            else
            {
                // Drawing Byte:

                int xSign = ((halfWord & 0x20) == 0) ? 1 : -1;
                int xMag = (int)(((halfWord & 0x18) >> 3) * _scale);

                int ySign = (int)(((halfWord & 0x04) == 0) ? 1 : -1);
                int yMag = (int)((halfWord & 0x03) * _scale);

                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "Inc mode ({0}:{1}), x={2} y={3} dx={4} dy={5} beamon {6}", Helpers.ToOctal((ushort)_pc), Helpers.ToOctal((ushort)halfWord), X, Y, xSign * xMag, ySign * yMag, (halfWord & 0x40) != 0);

                X = (uint)(X + xSign * xMag);
                Y = (uint)(Y + ySign * yMag);
                _system.Display.MoveAbsolute(X, Y, (halfWord & 0x40) == 0 ? DrawingMode.Off : DrawingMode.Normal);

                MoveToNextHalfWord();
            }

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
                PDS4DisplayInstruction instruction = GetCachedInstruction(_pc, DisplayProcessorMode.Increment);                
            }
            else
            {
                _immediateHalf = ImmediateHalf.Second;
            }
        }

        private void DrawLongVector(ushort word0)
        {
            //
            // A Long Vector instruction is 2 words long on the PDS-4:
            // Word 0: upper 5 bits indicate the opcode, lower 11 specify sign and magnitude M of larger of X and Y
            // Word 1: 
            // - lower 10 bits indicate magnitude of shorter deflection (N)
            // - bit 0 : return jump after vector drawn
            // - bit 1 : draw dashed line
            // - bit 2 : draw dotted line
            // - bit 3 : beam on
            // - bit 4 : 1 = M is Y, 0 = M is X
            // - bit 5 : sign of N
            
            ushort word1 = _mem.Fetch(++_pc);

            // Not documented, but from empirical evidence from PDS-4 games
            // (pong, crash) the magnitude is scaled by 2 (i.e. specified LSBN is bit 1 of X and Y AC's)
            uint M = (uint)(word0 & 0x3ff) << 1;
            uint N = (uint)(word1 & 0x3ff) << 1;

            bool ret = (word1 & 0x8000) != 0;
            bool dotted = (word1 & 0x4000) != 0;
            bool dashed = (word1 & 0x2000) != 0;
            bool beamOn = (word1 & 0x1000) != 0;
            bool dyGreater = (word1 & 0x0800) != 0;

            int mSign = (word0 & 0x0400) != 0 ? -1 : 1;
            int nSign = (word1 & 0x0400) != 0 ? -1 : 1;
             
            uint dx = 0;
            uint dy = 0;

            int dxSign = 0;
            int dySign = 0;

            if (dyGreater)
            {
                dy = M;
                dx = N;

                dySign = mSign;
                dxSign = nSign;
            }
            else
            {
                dx = M;
                dy = N;
                dxSign = mSign;
                dySign = nSign;
            }

            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "LongVector x={0} y={1} dx={2} dy={3} beamOn {4} dotted {5}", X, Y, dx * dxSign, dy * dySign, beamOn, dotted);
            
            // The docs don't call this out, but the scale setting used in increment mode appears to apply
            // to the LVH vectors as well.  (Maze appears to rely on this.)
            X = (uint)(X + (dx * dxSign) * _scale);
            Y = (uint)(Y + (dy * dySign) * _scale);

            if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "LongVector, move complete - x={0} y={1}", X, Y, dx * dxSign, dy * dySign, beamOn, dotted);

            _system.Display.MoveAbsolute(X, Y, beamOn ? (dotted ? DrawingMode.Dotted : DrawingMode.Normal) : DrawingMode.Off);

            _pc++;

            if (ret)
            {
                if (Trace.TraceOn) Trace.Log(LogType.DisplayProcessor, "LongVector, return from subroutine.");
                ReturnFromDisplaySubroutine();
            }
        }

        private PDS4DisplayInstruction GetCachedInstruction(ushort address, DisplayProcessorMode mode)
        {
            if (_instructionCache[address & Memory.SizeMask] == null)
            {
                _instructionCache[address & Memory.SizeMask] = new PDS4DisplayInstruction(_mem.Fetch(address), mode);
            }

            return _instructionCache[address & Memory.SizeMask];
        }

        // SGR-1 mode switches
        private bool _fxyOn; 
        private bool _fxyDRJMOn; 
        private bool _fxyBeamOn;

        /// <summary>
        /// TODO: All available PDS-4 docs insist that the X/Y AC LSB is four bits wide.
        /// This was 5 bits on the PDS-1...
        /// </summary>
        private const int MSBIncrement = 0x10;
        private const int MSBMask = 0xfff0;

        private PDS4DisplayInstruction[] _instructionCache;

        private readonly int[] _handledIOTs = { 0x1, 0x2, 0x3, 0xa, 0x39, 0xc4 };


        /// <summary>
        /// PDS-4 Display instruction decoder and disassembler.
        /// </summary>
        private class PDS4DisplayInstruction : DisplayInstructionBase
        {
            public PDS4DisplayInstruction(ushort word, DisplayProcessorMode mode) : base (word, mode)
            {
            }

            public override string Disassemble(DisplayProcessorMode mode)
            {
                if (mode == DisplayProcessorMode.Indeterminate)
                {
                    mode = _usageMode;
                }

                switch (mode)
                {
                    case DisplayProcessorMode.Increment:
                        return DisassembleIncrement();

                    case DisplayProcessorMode.Processor:
                        return DisassembleProcessor();

                    case DisplayProcessorMode.Indeterminate:
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
                    case 0x0:
                        // opr code
                        _opcode = DisplayOpcode.DOPR;
                        _data = (ushort)(_word & 0xfff);
                        break;

                    case 0x1:
                        _opcode = DisplayOpcode.DLXA;
                        if (!Configuration.MITMode)
                        {
                            // Just 11 bits
                            _data = (ushort)(_word & 0x7ff);
                        }
                        else
                        {
                            // All 13 bits (11 bits + 2 bits of scissor)
                            _data = (ushort)(_word & 0x1fff);
                        }
                        break;

                    case 0x2:
                        _opcode = DisplayOpcode.DLYA;
                        if (!Configuration.MITMode)
                        {
                            // Just 11 bits
                            _data = (ushort)(_word & 0x7ff);
                        }
                        else
                        {
                            // All 13 bits (11 bits + 2 bits of scissor)
                            _data = (ushort)(_word & 0x1fff);
                        }
                        break;

                    case 0x3:
                        _opcode = DisplayOpcode.DEIM;
                        _data = (ushort)(_word & 0xff);

                        if ((_word & 0x0800) != 0)
                        {
                            Console.Write("PPM-1 not implemented (instr {0})", Helpers.ToOctal(_word));
                        }
                        break;

                    case 0x4:
                        // This is either LVM (0 100 0xx ...) [040...] or MVM (0 100 1xx ...) [044...]
                        if ((_word & 0x0800) == 0)
                        {
                            // Long vector
                            _opcode = DisplayOpcode.DLVH;

                            // low 11 bits specify the sign (bit 5)
                            // and M (10-bits) - greater deflection of X and Y.
                            _data = (ushort)(_word & 0x07ff);
                        }
                        else
                        {
                            _opcode = DisplayOpcode.DMVM;
                            _data = (ushort)(_word & 0xff);
                        }
                        break;

                    case 0x5:
                        _opcode = DisplayOpcode.DJMS;
                        _data = (ushort)(_word & 0xfff);

                        if (Configuration.MITMode && (_word & 0x8000) != 0)
                        {
                            // MIT's mod takes the MSB of the address from the MSB of the instruction word
                            _data |= 0x1000;
                        }
                        break;

                    case 0x6:
                        _opcode = DisplayOpcode.DJMP;
                        _data = (ushort)(_word & 0xfff);

                        if (Configuration.MITMode && (_word & 0x8000) != 0)
                        {
                            // MIT's mod takes the MSB of the address from the MSB of the instruction word
                            _data |= 0x1000;
                        }
                        break;

                    case 0x7:
                        DecodeExtendedInstruction(_word);
                        break;

                    default:
                        throw new NotImplementedException(String.Format("Unhandled Display Processor Mode instruction {0}", Helpers.ToOctal(_word)));
                }
            }

            void DecodeExtendedInstruction(ushort word)
            {
                //
                // Decode "extended" operations which are all prefixed with "07" (octal).
                // There is no real method to the encoding madness here so we start with 
                // more specific encodings and work down.
                // Ugh.

                //
                // below, "x" means "octal digit, don't care"
                //        in braces, "b" means "binary digit, don't care"
                //

                // TODO: here and in general: move magic numbers to constants
                if (word == 0x7f91) // DCAM (Compact Addressing Mode) - 077621
                {
                    _opcode = DisplayOpcode.DCAM;
                    _data = 0;
                }
                else if ((word & 0x7ffe) == 0x7f92) // DBLI (Blinking) 07762 [01b]
                {
                    _opcode = DisplayOpcode.DBLI;
                    _data = (ushort)(word & 0x1);
                }
                else if ((word & 0x7ff8) == 0x7ff8) // FXY (Suppressed Grid) - 07777x
                {
                    _opcode = DisplayOpcode.DFXY;
                    _data = (ushort)(word & 0x7);
                }
                else if ((word & 0x7ff0) == 0x7fd0) // DVIC (Variable Intensity Control) 0777 [01b bbbb]
                {
                    _opcode = DisplayOpcode.DVIC;
                    _data = (ushort)(word & 0xf);
                }                        
                else if ((word & 0x7ff0) == 0x7ff0) // DASG (Auto increment & intensify) - 0776 [11b bbb]
                {
                    _opcode = DisplayOpcode.DASG;
                    _data = (ushort)(word & 0xf);
                }
                else if ((word & 0x7ff0) == 0x7fa0) // DROR (Character rotation/reflection) - 0776 [10b bbb]
                {
                    _opcode = DisplayOpcode.DROR;
                    _data = (ushort)(word & 0xf);
                }                
                else if ((word & 0x7e00) == 0x7c00) // DARX, DARY (Add Relative) - 076xxx
                {
                    _opcode = (word & 0x0100) == 0 ? DisplayOpcode.DARX : DisplayOpcode.DARY;
                    _data = (ushort)(word & 0xff);
                }
                else
                { 
                    throw new NotImplementedException(String.Format("Unhandled extended Display Processor Mode instruction {0}", Helpers.ToOctal(word)));
                }
            }

            private string DisassembleIncrement()
            {
                return DisassembleIncrementHalf(ImmediateHalf.First) + " | " + DisassembleIncrementHalf(ImmediateHalf.Second);
            }

            private string DisassembleIncrementHalf(ImmediateHalf half)
            {
                string ret = string.Empty;
                int halfWord = half == ImmediateHalf.First ? (_word & 0xff00) >> 8 : (_word & 0xff);

                // translate the half word to vector movements or escapes
                // special case for "Enter Immediate mode" halfword (030) in first half.
                if (half == ImmediateHalf.First && halfWord == 0x30)
                {
                    ret += "E";
                }
                else if ((halfWord & 0x80) == 0)
                {
                    if ((halfWord & 0x10) != 0)
                    {
                        ret += "IX ";
                    }

                    if ((halfWord & 0x08) != 0)
                    {
                        ret += "ZX ";
                    }

                    if ((halfWord & 0x02) != 0)
                    {
                        ret += "IY ";
                    }

                    if ((halfWord & 0x01) != 0)
                    {
                        ret += "ZY ";
                    }

                    if ((halfWord & 0x40) != 0)
                    {
                        if ((halfWord & 0x20) != 0)
                        {
                            // escape and return
                            ret += "F RJM";
                        }
                        else
                        {
                            // Escape
                            ret += "F";
                        }
                    }
                }
                else
                {
                    int xSign = ((halfWord & 0x20) == 0) ? 1 : -1;
                    int xMag = (int)(((halfWord & 0x18) >> 3));

                    int ySign = (int)(((halfWord & 0x04) == 0) ? 1 : -1);
                    int yMag = (int)((halfWord & 0x03));

                    ret += String.Format("{0},{1} {2}", xMag * xSign, yMag * ySign, (halfWord & 0x40) == 0 ? "OFF" : "ON");
                }

                return ret;
            }

            private void DecodeImmediate()
            {
                // TODO: eventually actually precache movement calculations.
            }

            private string DisassembleProcessor()
            {
                string ret = String.Empty;
                if (_opcode == DisplayOpcode.DOPR)
                {                    
                    string[] codes = { "INV0 ", "INV1 ", "INV2 ", "INV3 ", "DDSP ", "DRJM ", "DDYM ", "DDXM ", "DIYM ", "DIXM ", "DHVC ", "DHLT " };

                    for (int i = 4; i < 12; i++)
                    {
                        if ((_data & (0x01) << i) != 0)
                        {
                            if (!string.IsNullOrEmpty(ret))
                            {
                                ret += ",";
                            }

                            ret += codes[i];
                        }
                    }

                    // F/C ops:
                    int f = (_data & 0xc) >> 2;
                    int c = _data & 0x3;

                    switch (f)
                    {
                        case 0x0:
                            // nothing
                            if (c == 1)
                            {
                                ret += String.Format("DADR");
                            }
                            break;

                        case 0x1:
                            ret += String.Format("DSTS {0}", c);
                            break;

                        case 0x2:
                            ret += String.Format("DSTB {0}", c);
                            break;

                        case 0x3:
                            ret += String.Format("DLPN {0}", c);
                            break;
                    }
                }
                else
                {
                    // keep things simple -- should add special support for extended instructions at some point...
                    ret = String.Format("{0} {1} ", _opcode, Helpers.ToOctal(_data));
                }

                return ret;
            }
        }
    }
}
