sImlac v0.2 README - (c) 2016-2018 Living Computers: Museum+Labs
-----------------------------------------------------------------

1. Overview
-----------
sImlac is an attempt to emulate/simulate the oft neglected Imlac PDS-1 
computer/terminal.  The Imlac combined a 16-bit CPU (very PDP-8 like) with
a simple (but fairly flexible) Display Processor which drove a vector
display.

sImlac currently emulates the current hardware:

    - Standard Imlac Processor / Display Processor (with 1.8us cycle timings)
    - 8KW of core memory
    - Vector display (with long-persistence phosphor)
    - PTR and TTY interfaces (using physical serial ports or files as inputs)
    - Keyboard
    - Interrupt facility
    - Long Vector (LVH-1 option) instruction support
    - 8-level DT stack (MDS-1 option)

This is enough to have fun with the small amount of archived software that's out
there.  Support for additional hardware is planned, but is mostly dependent on
finding software that requires it.

Since this is v0.2, there are still likely to be bugs.

Questions, comments, or bug reports can be directed at 
joshd@livingcomputers.org.

2. System Requirements
----------------------

sImlac is a .NET application and should run on will run on any Windows PC running 
Windows Vista or later, with version 4.5.3 or later of the .NET Framework installed.
.NET should be present by default on Windows Vista and later; if it is not installed 
on your computer it can be obtained at https://www.microsoft.com/net.

As ContrAlto is a .NET application it will also run under Mono 
(http://www.mono-project.com/) on Unix and OS X.

sImlac uses SDL 2.0 for display and input.  On Windows the appropriate SDL.dll is 
included in the distribution package.  On Linux, use your system's package manager 
to install SDL 2.0; on OS X the easiest way to get SDL 2.0 is to use Homebrew
(https://brew.sh/), via the "brew install sdl2" command.


3. Getting Started
------------------

Bootstrapping an Imlac is a pretty straightforward process:
a bootstrap ROM is typically located at 40(8) (or is toggled in manually)
and this is executed to bring in code from a variety of sources -- in most
cases either a Paper Tape or over the TTY (serial) port.

sImlac comes with three boot loaders, and the correct one must be chosen for
the image you are loading (see software.txt for a listing that describes the
available software and the loader to use).

A loader can be loaded using the "set bootstrap" command.  Let's say we want 
to play a game of "Space War!."  The image named "war" uses the STTY (special
TTY) loader, so we issue the following command:

> set bootstrap stty

Now we need to attach the image to the TTY port, this is done by:

> attach tty file <path-to-images>\war

Once this is done, we can start the CPU running.  By default, the PC is set
to 40(8), the start of the boot loader, so we can just do:

> go

And the loader will run.  This will take a few seconds to complete, after which
the title screen for "Space War!" will appear in the display window.

Once loading is done, some programs will automatically start, while some may
halt the CPU.  If the CPU halts, the CPU must manually be started at the proper
address -- this is usually 100(8); software.txt attempts to document this.

You may also need to set the Data Switch register (DS) -- many programs will
halt if bit 0 is not set.

> set data switch register 100000
> go 100

Will usually set you right.

4. Usage
--------

4.1 Command line arguments
--------------------------

sImlac accepts one optional command line argument, which specifies a file to
use as a startup script.  A script consists of one or more commands (See section
3.3) which are executed in sequence.  A '#' character denotes a comment, and an
'@' symbol allows including other scripts (i.e. "@otherscript.txt" causes) 
the contents of "otherscript.txt" to be loaded and executed as a script.)

Whitespace is ignored.


4.2 The sImlac console/debugger
-------------------------------

After startup, you will be at the sImlac debugger prompt (a '>' character).

sImlac provides a somewhat-context-sensitive input line.  Press TAB at any
point during input to see possible completions for the command you're entering.

The "show commands" command provides a brief synopsis of available commands,
these are described in greater detail in Section 3.4.

All numeric arguments are specified in Octal by default.  A number may be
prefixed with 'b', 'o', 'd', or 'x' to specify binary, octal, decimal or
hexadecimal, respectively.

All numeric outputs are presented in Octal.

While the simulated Imlac is running (via the 'go' or other commands) the
console is inactive; press Ctrl-C to stop the Imlac and return to the command
prompt.

4.3 The sImlac display
----------------------

sImlac creates a window that simulates the Imlac's vector display and allows
keyboard input to the Imlac.  When Imlac programs are running, their output 
will be displayed here.  By default, the display is shown in a window, pressing
the "Insert" key will toggle between window and fullscreen modes.

4.4 Commands
------------

reset               :  Resets the Imlac, but does not clear its memory.

set bootstrap <boot>:  Loads the specified bootstrap into memory at 40(8).
                       Options are PTR (paper tape), TTY (standard TTY), and
                       STTY (alternate TTY).

attach ptr [file]   :  Specifies a Paper Tape file to attach to the paper tape
                       reader (PTR).  Use in conjunction with the PTR 
                       bootstrap.  This file is currently read-only.

attach tty file [file] : Specifies a TTY or STTY-format file to attach to the TTY
                       interface (TTY).  Use in conjunction with the TTY or STTY
                       bootstraps.

attach tty port [port] [rate] [parity] [data bits] [stop bits] :
                      Specifies a physical port on the host machine to connect
                      to the emulated TTY port.  Allows the emulated Imlac to
                      talk to real serial devices.

go <addr>           :  Starts the system running at the current PC, or <addr> if 
                       specified.

step <addr>         :  Single-cycles the system (runs one clock cycle).  If <addr>
                       is specified, cycles starting at that address.

step frame end      :  Runs the current 40hz frame to its completion.  If the 
                       display is not running, this may never automatically 
                       complete.  (Hit Ctrl-C to return to the debugger.)

step frame start    :  Runs until the start of the next frame.

edit memory [addr]  :  Begins a very simple memory editor starting at address 
                       'addr'.  You will be shown the current address\contents,
                       and prompted for new contents.  Press ENTER to keep the
                       current contents, enter a new value to replace it, or 
                       an invalid input to quit.

save memory [file] [start] [len] : Saves the specified memory contents to the 
                       specified file.

load memory [file] [start] [len] : Loads memory contents from the specified file.

display memory [start] [len] : Shows the specified memory contents onscreen.


disassemble <start> length> :  Disassembles instructions in the specified address 
                       range.  Attempts to automatically determine the 
                       appropriate instruction type based on previous execution.

disassemble <mode> <start> <length> : Disassembles instructions in the specified 
                       address range, forcing the type to that specified by 'mode':
                        - Processor: Disassembles as processor instructions
                        - DisplayProcessor: Disassembles as display processor 
                          instructions
                        - DisplayIncrement: Disassembles as display increment
                          instructions
                        - DisplayAuto: Attempts to disassemble based on the
                          type last used during execution.

set data switch register <value> : Sets the Data Switch register to the
                       specified value.  Note that if Data Switch mapping is
                       enabled (see Section 3.5) this will have no effect.

show data switch register : Shows the current value in the Data Switch Register.

enable data swtich mappings : Enables mapping of keys to data switches (See
                       Section 3.5)

disable data switch mappings : Disables mapping of keys to data switches (See
                       Section 3.5)

set data switch mapping <bit> <key> : Maps the specified keyboard key to the
                       specified Data Switch bit.  (See Section 3.5)

show data switch mapping <bit> : Displays the current mapping for the specified
                       Data Switch bit.  (See Section 3.5)

set data switch mode : Sets the mapping mode used for Data Switches.  (Again,
                       see Section 3.5)

set logging <value> :  Enables diagnostic output for the specified components.
                       Possible values are one or more of the below:
                        
                         None, Processor, DisplayProcessor, Display, Keyboard,
                         Interrupt, TTY, PTR, All

show logging        :  Shows the current logging settings.
   
set display scale [scalefactor] : Sets the current scaling factor applied to
                      the Imlac's display.  By default this is 0.5 (resulting
                      in a 1024x1024 window.  A value of 1.0 reflects the
                      Imlac's true native resolution of 2048x2048 but is too
                      large for most displays.)

set framerate throttle [true|false] : Enables or disables speed throttling.
                      If enabled, sImlac will run at 40 frames/sec, if disabled
                      sImlac will run as fast as possible.

set breakpoint execution/read/write/display [address] :
                      Enables the specified type of breakpoint on the specified
                      address.  An Execution or Display breakpoint breaks when 
                      the instruction at the specified address is about to be
                      executed.  Read/Write breakpoints break after execution
                      of the instruction doing the read or write to the 
                      specified address.

                      A single address may contain any or all of the possible
                      breakpoint types set.

clear breakpoint [address] : Clears all breakpoints from the given address.

enable breakpoints : Enables breakpoints globally.

disable breakpoints : Disables breakpoints globally.

show breakpoints   : Lists the defined breakpoints.

show commands      : Displays a synopsis of available console commands.


4.5 Data Switch Mappings
------------------------

Some Imlac software (mostly games) use the Data Switches on the Imlac's front
panel to control things (like spaceships).  Since modern computers don't 
usually have control panels with rows of lights and toggle switches, sImlac
provides a facility allowing keys on the keyboard to simulate the Data Switches
on a real Imlac's front panel.

Data Switch mapping can be enabled with the "enable data switch mappings" 
command and disabled with "disable data switch mappings." 

There are 16 Data Switches, numbered 0 through 15, each corresponding to a 
single bit in the 16-bit Data Switch Register.  In Imlac convention, bit 0 is 
the most-significant bit, while bit 15 is the least-significant.

The actual mappings themselves can be defined via the "set data switch mapping"
command:

> set data switch mapping 5 F5

for example, maps Data Switch 5 to the F5 key on your keyboard.

Because different software uses the Data Switches in different ways, sImlac 
provides three different mapping modes:

    - Toggle:  Each keypress toggles the corresponding Data Switch.  That is,
               the first press turns the switch on, the next turns it off, and
               so on.
    - Momentary: A keypress turns the corresponding Data Switch on, but only
                 so long as the key remains pressed.  When the key is released,
                 the Data Switch is turned off.
    - MomentaryInverted: Same as above, but inverted -- a keypress turns the 
                 switch off, when the key is released the switch is on.

The console command "set data switch mode" is used to select the mapping mode
to use.

The following values are provided for keyboard keys:

    Shift Ctrl Alt End DownArrow RightArrow UpArrow LeftArrow Tab Return 
    PageUp PageDown Home Pause Escape Space Comma Plus Period QuestionMark Zero 
    One Two Three Four Five Six Seven Eight Nine Minus Semicolon DoubleQuote A 
    B C D E F G H I J K L M N O P Q R S T U V W X Y Z Delete F1 F2 F3 F4 F5 F6 
    F7 F8 F9 F10 F11 F12 Keypad0 Keypad1 Keypad2 Keypad3 Keypad4 Keypad5 
    Keypad6 Keypad7 Keypad8 Keypad9 Insert

Additionally, two special values exist:  None0 and None1.  These indicate that
no key is to be mapped to the specified Data Switch bit, but instead that bit
should be forced to 0 (for None0) or 1 (for None1).


5. Getting Software
-------------------

See Tom Uban's archive at:  http://www.ubanproductions.com/imlac_sw.html.

software.txt (included with this distribution) attempts to describe this software
in greater detail.


6. Thanks
---------

Thanks go out to:

- Tom Uban for making sure the software and documentation he had got archived;
  without this, not only would there be no hardware information left (making
  an emulator difficult to write) there wouldn't even be any software to run
  on it!

- Bitsavers.org for making documentation for the Imlac available.


7. Revision History
-------------------

v0.2 - Updated to use SDL-CS for better cross-platform support.

v0.1 - Initial Release

