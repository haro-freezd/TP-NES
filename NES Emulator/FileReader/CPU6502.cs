﻿using System;
using System.Threading;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Ricoh 6502 CPU
/// Author: Harman Mahal, George Lee, Steven Ma
/// Version: 1.0
/// </summary>
namespace NESEmu
{
    /// <summary>
    /// Emulation of the Ricoh 6502 CPU and its functions and capabilities
    /// </summary>
    public class CPU6502
    {
        //CPU Registers. The prototype only utilises accumulator, PC and reg x

        /*The 2-byte program counter ‘PC’ supports 65536 direct (unbanked)
         * memory locations, however not all values are sent to the cartridge.
         * It can be accessed either by allowing CPU's internal fetch logic
         *  increment the address bus, an interrupt (NMI, Reset, IRQ/BRQ),
         *   and using the RTS/JMP/JSR/Branch instructions.*/
        private ushort pc_register;

        /*The stack_point register is byte-wide and can be accessed
        using interrupts, pulls, pushes, and transfers.*/
        private byte stack_pointer;

        /*The accumulator register is byte-wide and along with the arithmetic
         * logic unit(ALU), supports using the status register for carrying,
         *  overflow detection, and so on.*/
        private byte accumulator;

        /*X and Y are byte-wide and used for several addressing modes.They
         * can be used as loop counters easily, using increment/decrement
         * and branch instructions.Not being the accumulator, they have
         * limited addressing modes themselves when loading and saving.*/
        private byte reg_x;

        /*This is a very important register. There are instructions for
         * nearly all of the transformations you can make to the accumulator,
         *  and the X register. But there are other instructions for
         *  things that only the Y register can do. Various machine
         *  language instructions allow you to copy the contents of a
         *  memory location into the Y register, copy the contents of
         *  the Y register into a memory location, and modify the contents
         *   of the Y, or some other register directly.*/
        private byte reg_y;

        //CPU Status Flags
        //Reference: http://www.zophar.net/fileuploads/2/10532krzvs/6502.txt

        /*This flag holds the carry out of the most significant
         bit in any arithmetic operation. In subtraction operations however, this
        flag is cleared - set to 0 - if a borrow is required, set to 1 - if no
        borrow is required. The carry flag is also used in shift and rotate
        logical operations.*/
        private byte carry_flag;

        /*This flag is set to 1 when any arithmetic or logical
         operation produces a zero result, and is set to 0 if the result is
         non-zero.*/
        private byte zero_flag;

        /*This flag is an interrupt enable/disable flag. If it is set,
        interrupts are disabled. If it is cleared, interrupts are enabled.*/
        private byte interrupt_flag;

        /*This flag is the decimal mode status flag. When set, and an Add with
        Carry or Subtract with Carry instruction is executed, the source values are
        treated as valid BCD (Binary Coded Decimal, eg. 0x00-0x99 = 0-99) numbers.
        The result generated is also a BCD number.*/
        private byte decimal_flag;

        /*This flag is set when a software interrupt (BRK instruction) is
        executed.*/
        private byte break_flag;

        //Byte 5 (simulated bit 5): not used.Supposed to be logical 1 at all times.
        private byte unused_flag;

        /*when an arithmetic operation produces a result
        too large to be represented in a byte, V is set.*/
        private byte overflow_flag;

        /*this is set if the result of an operation is
        negative, cleared if positive.*/
        private byte sign_flag;

        private bool inject = false;
        private byte injectVal;

        private InterruptMode interrupt;
        public int Stall { get; set; }
        private bool _running; //set to false to shut down the cpu
        private uint _cyclesToWait; //the amount of cycles this operation takes
        private Thread _cpuThread;
        public uint Cycle { get; set; } //Which cycle is the cpu on
        private byte currentInstruction;        
        public Memory RAM { get; set; }

        public int CurrentAddress { get; set; }

        private delegate void OpCodeMethods(MemoryInfo mem);


        private static CPU6502 instance;        


        /// <summary>
        /// Returns a string value from the array of instructions array. 
        /// </summary>

        public string CurrentInstruction
        {
            get
            {
                return instructions[currentInstruction];
            }
        }



        #region ArrayMaps

        /// <summary>
        /// Array of opcode instruction names.
        /// </summary>
        private string[] instructions = new string[256] {
            "BRK", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "PHP", "ORA", "ASL", "ANC", "NOP", "ORA", "ASL", "SLO",
            "BPL", "ORA", "KIL", "SLO", "NOP", "ORA", "ASL", "SLO",
            "CLC", "ORA", "NOP", "SLO", "NOP", "ORA", "ASL", "SLO",
            "JSR", "AND", "KIL", "RLA", "BIT", "AND", "ROL", "RLA",
            "PLP", "AND", "ROL", "ANC", "BIT", "AND", "ROL", "RLA",
            "BMI", "AND", "KIL", "RLA", "NOP", "AND", "ROL", "RLA",
            "SEC", "AND", "NOP", "RLA", "NOP", "AND", "ROL", "RLA",
            "RTI", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "PHA", "EOR", "LSR", "ALR", "JMP", "EOR", "LSR", "SRE",
            "BVC", "EOR", "KIL", "SRE", "NOP", "EOR", "LSR", "SRE",
            "CLI", "EOR", "NOP", "SRE", "NOP", "EOR", "LSR", "SRE",
            "RTS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "PLA", "ADC", "ROR", "ARR", "JMP", "ADC", "ROR", "RRA",
            "BVS", "ADC", "KIL", "RRA", "NOP", "ADC", "ROR", "RRA",
            "SEI", "ADC", "NOP", "RRA", "NOP", "ADC", "ROR", "RRA",
            "NOP", "STA", "NOP", "SAX", "STY", "STA", "STX", "SAX",
            "DEY", "NOP", "TXA", "XAA", "STY", "STA", "STX", "SAX",
            "BCC", "STA", "KIL", "AHX", "STY", "STA", "STX", "SAX",
            "TYA", "STA", "TXS", "TAS", "SHY", "STA", "SHX", "AHX",
            "LDY", "LDA", "LDX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "TAY", "LDA", "TAX", "LAX", "LDY", "LDA", "LDX", "LAX",
            "BCS", "LDA", "KIL", "LAX", "LDY", "LDA", "LDX", "LAX",
            "CLV", "LDA", "TSX", "LAS", "LDY", "LDA", "LDX", "LAX",
            "CPY", "CMP", "NOP", "DCP", "CPY", "CMP", "DEC", "DCP",
            "INY", "CMP", "DEX", "AXS", "CPY", "CMP", "DEC", "DCP",
            "BNE", "CMP", "KIL", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CLD", "CMP", "NOP", "DCP", "NOP", "CMP", "DEC", "DCP",
            "CPX", "SBC", "NOP", "ISC", "CPX", "SBC", "INC", "ISC",
            "INX", "SBC", "NOP", "SBC", "CPX", "SBC", "INC", "ISC",
            "BEQ", "SBC", "KIL", "ISC", "NOP", "SBC", "INC", "ISC",
            "SED", "SBC", "NOP", "ISC", "NOP", "SBC", "INC", "ISC", };

        //TODO:Change this to use the enum values
        /// <summary>
        /// Addressing mode table as defined in the addressingMode enum
        /// </summary>
        private int[] addressingMode = new int[256] {
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            1, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            6, 7, 6, 7, 11, 11, 11, 11, 6, 5, 4, 5, 8, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 13, 13, 6, 3, 6, 3, 2, 2, 3, 3,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2,
            5, 7, 5, 7, 11, 11, 11, 11, 6, 5, 6, 5, 1, 1, 1, 1,
            10, 9, 6, 9, 12, 12, 12, 12, 6, 3, 6, 3, 2, 2, 2, 2, };

        /// <summary>
        /// Number of cycles/instruction
        /// </summary>
        private uint[] instructionCycles = new uint[256] {
            7, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 3, 2, 2, 2, 3, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            6, 6, 2, 8, 3, 3, 5, 5, 4, 2, 2, 2, 5, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 6, 2, 6, 4, 4, 4, 4, 2, 5, 2, 5, 5, 5, 5, 5,
            2, 6, 2, 6, 3, 3, 3, 3, 2, 2, 2, 2, 4, 4, 4, 4,
            2, 5, 2, 5, 4, 4, 4, 4, 2, 4, 2, 4, 4, 4, 4, 4,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7,
            2, 6, 2, 8, 3, 3, 5, 5, 2, 2, 2, 2, 4, 4, 6, 6,
            2, 5, 2, 8, 4, 4, 6, 6, 2, 4, 2, 7, 4, 4, 7, 7, };

        /// <summary>
        /// # of bytes for a given instruction
        /// </summary>
        private ushort[] instructionSize = new ushort[256] {
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            3, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            1, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 0, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 0, 3, 0, 0,
            2, 2, 2, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 2, 1, 0, 3, 3, 3, 0,
            2, 2, 0, 0, 2, 2, 2, 0, 1, 3, 1, 0, 3, 3, 3, 0, };

        /// <summary>
        /// 
        /// </summary>
        private uint[] pageCrossedCycle = new uint[256] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, };

        private OpCodeMethods[] instructionAction;

        /// <summary>
        /// Intializes the instructionAction to be OpCodeMethods object array of 256.
        /// </summary>
        private void addInstructionAction()
        {
            instructionAction = new OpCodeMethods[256] {
                brk, ora, kil, slo, nop, ora, asl, slo,
                php, ora, asl, anc, nop, ora, asl, slo,
                bpl, ora, kil, slo, nop, ora, asl, slo,
                clc, ora, nop, slo, nop, ora, asl, slo,
                jsr, and, kil, rla, bit, and, rol, rla,
                plp, and, rol, anc, bit, and, rol, rla,
                bmi, and, kil, rla, nop, and, rol, rla,
                sec, and, nop, rla, nop, and, rol, rla,
                rti, eor, kil, sre, nop, eor, lsr, sre,
                pha, eor, lsr, alr, jmp, eor, lsr, sre,
                bvc, eor, kil, sre, nop, eor, lsr, sre,
                cli, eor, nop, sre, nop, eor, lsr, sre,
                rts, adc, kil, rra, nop, adc, ror, rra,
                pla, adc, ror, arr, jmp, adc, ror, rra,
                bvs, adc, kil, rra, nop, adc, ror, rra,
                sei, adc, nop, rra, nop, adc, ror, rra,
                nop, sta, nop, sax, sty, sta, stx, sax,
                dey, nop, txa, xaa, sty, sta, stx, sax,
                bcc, sta, kil, ahx, sty, sta, stx, sax,
                tya, sta, txs, tas, shy, sta, shx, ahx,
                ldy, lda, ldx, lax, ldy, lda, ldx, lax,
                tay, lda, tax, lax, ldy, lda, ldx, lax,
                bcs, lda, kil, lax, ldy, lda, ldx, lax,
                clv, lda, tsx, las, ldy, lda, ldx, lax,
                cpy, cmp, nop, dcp, cpy, cmp, dec, dcp,
                iny, cmp, dex, axs, cpy, cmp, dec, dcp,
                bne, cmp, kil, dcp, nop, cmp, dec, dcp,
                cld, cmp, nop, dcp, nop, cmp, dec, dcp,
                cpx, sbc, nop, isc, cpx, sbc, inc, isc,
                inx, sbc, nop, sbc, cpx, sbc, inc, isc,
                beq, sbc, kil, isc, nop, sbc, inc, isc,
                sed, sbc, nop, isc, nop, sbc, inc, isc,
            };
        }

        #endregion ArrayMaps

        /// <summary>
        /// Constructor for the CPU. Initializes memory object and provides default values
        /// for PC and Stack Pointer
        /// </summary>
        private CPU6502(Memory mem)
        {
            RAM = mem;
            addInstructionAction();
            Reset();          
        }

        /// <summary>
        /// Returns the static CPU instance when requested. Utilizing the Singleton pattern
        /// </summary>
        public static CPU6502 Instance
        {
            get
            {
                if (instance == null)
                {
                    throw new Exception("CPU not created");
                }
                return instance;
            }
        }

        /// <summary>
        /// Method for creating an object in memory.  If object already exists 
        /// an error is thrown.  
        /// </summary>
        /// <param name="mem"></param>
        public static void Create(Memory mem)
        {
            if (instance != null)
            {
                throw new Exception("Object already created");
            }
            instance = new CPU6502(mem);
        }

        #region Helper

        /// <summary>
        /// Sets byte value to zero.
        /// </summary>
        /// <param name="value"></param>
        private void setZero(byte value)
        {
            if (value == 0)
                zero_flag = 1;
            else
                zero_flag = 0;
        }

        /// <summary>
        /// Sets byte value to change signs.
        /// </summary>
        /// <param name="value"></param>
        private void setSign(byte value)
        {
            if ((value & 0x80) != 0)
            {
                sign_flag = 1;
            }
            else
                sign_flag = 0;
        }

        /// <summary>
        /// Method for reseting the CPU (flags, stack pointer, memory, PPU, etc.)
        /// </summary>
        public void Reset()
        {
            pc_register = Read16(0xFFFC);
            stack_pointer = 0xFD;
            setFlags(0x24);
            RAM.ClearMemory();
            PPU.Instance.reset();
        }

        /// <summary>
        /// Compares byte a and byte b.  Sets carry flag if a is greater than b.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        private void Compare(byte a, byte b)
        {
            setZero((byte)(a - b));
            setSign((byte)(a - b));

            if (a >= b)
                carry_flag = 1;
            else
                carry_flag = 0;
        }

        /// <summary>
        /// Method for writing byte value to memory at the address specified by 
        /// the stack pointer. 
        /// </summary>
        /// <param name="value"></param>
        private void push(byte value)
        {
            ushort addr = (ushort)(0x100 | stack_pointer);
            RAM.WriteMemory(addr, value);
            stack_pointer--;
        }

        /// <summary>
        /// Method for pulling the byte object from memory in the address specified.  
        /// </summary>
        /// <returns></returns>
        private byte pull()
        {
            stack_pointer++;
            ushort addr = (ushort)(0x100 | stack_pointer);
            return RAM.ReadMemory(addr);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private ushort Read16(ushort address)
        {
            byte lowByte = RAM.ReadMemory(address);
            byte highByte = RAM.ReadMemory((ushort)(address + 1));
            ushort result = (ushort)(highByte << 8 | lowByte);
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private ushort errorRead16(ushort address)
        {
            ushort tmp = address;
            ushort tmp_2 = (ushort)((tmp & 0xFF00) | ((byte)(tmp)) + 1);
            byte lowByte = RAM.ReadMemory(tmp);
            byte highByte = RAM.ReadMemory(tmp_2);
            ushort result = (ushort)(((ushort)(highByte) << 8) | lowByte);
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        private void Push16(ushort value)
        {
            byte highByte = (byte)(value >> 8);
            byte lowByte = (byte)(value & 0xFF);
            push(highByte);
            push(lowByte);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private ushort Pull16()
        {
            ushort low = pull();
            ushort high = pull();
            return (ushort)(high << 8 | low);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private bool pagesDiffer(ushort a, ushort b)
        {
            return (a & 0xFF00) != (b & 0xFF00);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mem"></param>
        private void addCycles(MemoryInfo mem)
        {
            Cycle++;
            if (pagesDiffer(mem.PC_register, mem.Address))
                Cycle++;
        }

        /// <summary>
        /// Returns the flags as bytes to simulate bit flags in actual NES.  
        /// </summary>
        /// <returns></returns>
        private byte Flags()
        {
            byte flags = 0;
            flags |= (byte)(carry_flag << 0);
            flags |= (byte)(zero_flag << 1);
            flags |= (byte)(interrupt_flag << 2);
            flags |= (byte)(decimal_flag << 3);
            flags |= (byte)(break_flag << 4);
            flags |= (byte)(unused_flag << 5);
            flags |= (byte)(overflow_flag << 6);
            flags |= (byte)(sign_flag << 7);
            return flags;
        }

        /// <summary>
        /// Sets flags as bytes to simulate the big flags used in actual NES.  
        /// </summary>
        /// <param name="flags"></param>
        private void setFlags(byte flags)
        {
            carry_flag = (byte)((flags >> 0) & 1);
            zero_flag = (byte)((flags >> 1) & 1);
            interrupt_flag = (byte)((flags >> 2) & 1);
            decimal_flag = (byte)((flags >> 3) & 1);
            break_flag = (byte)((flags >> 4) & 1);
            unused_flag = (byte)((flags >> 5) & 1);
            overflow_flag = (byte)((flags >> 6) & 1);
            sign_flag = (byte)((flags >> 7) & 1);
        }

        
        /// <summary>
        /// Sets the Interrupt mode to NMI.
        /// </summary>
        public void triggerNMI()
        {
            interrupt = InterruptMode.NMIInterrupt;
        }

        /// <summary>
        /// Sets the Interrupt mode to IRQ.
        /// </summary>
        public void triggerIRQ()
        {
            if (interrupt_flag == 0)
                interrupt = InterruptMode.IRQInterrupt;
        }

        #endregion Helper

        /// <summary>
        /// Starts the cpu, to be called after CPU is properly set up
        /// </summary>
        public void start()
        {
            string methodname = "CPU.start()";
            Debug.WriteLine("Entered: " + methodname);
            try
            {
                if (_cpuThread == null)
                {
                    _cpuThread = new Thread(new ThreadStart(run));
                    _running = true;
                    _cpuThread.Start();
                }
                else
                {
                    Debug.WriteLine("Thread already exists.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                Debug.WriteLine("Exiting: " + methodname);
            }
        }

        /// <summary>
        /// The loop that is run by the cpu thread
        /// Run as past as possible, the thread may
        /// not keep up with the real cpu cycles per second (~556ns per cycle)
        /// </summary>
        private void run()
        {
            string methodname = "CPU.run()";
            Debug.WriteLine("Entered: " + methodname);
            try
            {
                while (_running)
                {
                    Console.WriteLine(_cyclesToWait);
                    if (_cyclesToWait <= 0)
                    {
                        Tick();
                    }
                    if (Cycle == 3)
                    {
                        //do ppu
                        //do apu
                        Cycle -= 3;
                    }
                    _cyclesToWait--;
                    Cycle++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            finally
            {
                Debug.WriteLine("Exiting: " + methodname);
            }
        }

        /// <summary>
        /// used to inject a opcode into cpu for testing purposes
        /// </summary>
        /// <param name="val"></param>
        public void Inject(int val)
        {
            inject = true;
            injectVal = Convert.ToByte(val);
        }

        /// <summary>
        /// Executes the next instruction at the location of PC in the memory
        /// </summary>
        public uint Tick()
        {
            if (Stall > 0)
            {
                Stall--;
                return 1;
            }            
            uint cycles = Cycle;
            switch (interrupt)
            {
                case (InterruptMode.IRQInterrupt):
                    irq();
                    break;

                case (InterruptMode.NMIInterrupt):
                    nmi();
                    break;

                default:
                    break;
            }
            interrupt = InterruptMode.NoneInterrupt;

            byte opcode = RAM.ReadMemory(pc_register);
            //Console.Write(instructions[opcode]);
            if (inject)
            {
                opcode = injectVal;
                inject = false;
            }
            int addrMode = addressingMode[opcode];
            currentInstruction = opcode;

            ushort addr = 0;
            bool pageCrossed = false;

            switch (addrMode)
            {
                case ((int)AddressingMode.Absolute):
                    addr = Read16((ushort)(pc_register + 1));
                    break;

                case ((int)AddressingMode.AbsoluteX):
                    addr = (ushort)(Read16((ushort)(pc_register + 1)) + reg_x);
                    pageCrossed = pagesDiffer((ushort)(addr - reg_x), addr);
                    break;

                case ((int)AddressingMode.AbsoluteY):
                    addr = (ushort)(Read16((ushort)(pc_register + 1)) + reg_y);
                    pageCrossed = pagesDiffer((ushort)(addr - reg_y), addr);
                    break;

                case ((int)AddressingMode.Accumulator):
                    addr = 0;
                    break;

                case ((int)AddressingMode.Immediate):
                    addr = (ushort)(pc_register + 1);
                    break;

                case ((int)AddressingMode.Implied):
                    addr = 0;
                    break;

                case ((int)AddressingMode.IndirectX):
                    addr = errorRead16((ushort)(RAM.ReadMemory((ushort)(pc_register + 1)) + reg_x));
                    break;

                case ((int)AddressingMode.Indirect):
                    addr = errorRead16(Read16((ushort)(pc_register + 1)));
                    break;

                case ((int)AddressingMode.IndirectY):
                    addr = (ushort)(errorRead16(RAM.ReadMemory((ushort)(pc_register + 1))) + reg_y);
                    pageCrossed = pagesDiffer((ushort)(addr - reg_y), addr);
                    break;

                case ((int)AddressingMode.Relative):
                    ushort offset = RAM.ReadMemory((ushort)(pc_register + 1));

                    if (offset < 0x80)
                        addr = (ushort)(pc_register + 2 + offset);
                    else
                        addr = (ushort)(pc_register + 2 + offset - 0x100);
                    break;

                case ((int)AddressingMode.ZeroPage):
                    addr = RAM.ReadMemory((ushort)(pc_register + 1));
                    break;

                case ((int)AddressingMode.ZeroPageX):
                    addr = (ushort)(RAM.ReadMemory((ushort)(pc_register + 1)) + reg_x);
                    break;

                case ((int)AddressingMode.ZeroPageY):
                    addr = (ushort)(RAM.ReadMemory((ushort)(pc_register + 1)) + reg_y);
                    break;
            }
            //sw.WriteLine(instructions[opcode] + " " + pc_register.ToString("X4") + " " + addr.ToString("X4"));
            pc_register += instructionSize[opcode];
            Cycle += instructionCycles[opcode];
            if (pageCrossed)
                Cycle += pageCrossedCycle[opcode];
            //Console.WriteLine(addr);
            MemoryInfo mem = new MemoryInfo(addr, pc_register, addrMode);
            CurrentAddress = addr;
            instructionAction[opcode](mem);
            return Cycle - cycles;
        }

        /// <summary>
        /// Non Maskable Interrupt. 
        /// No state inside the CPU can prevent the NMI from being processed as an interrupt.
        /// </summary>
        private void nmi()
        {
            Push16(pc_register);
            php(new MemoryInfo());
            pc_register = Read16(0xFFFA);
            interrupt_flag = 1;
            Cycle += 7;
        }

        /// <summary>
        /// Method to handle Interrupt Request
        /// https://wiki.nesdev.com/w/index.php/IRQ
        /// </summary>
        private void irq()
        {
            Push16(pc_register);
            php(new MemoryInfo());
            pc_register = Read16(0xFFFE);
            interrupt_flag = 1;
            Cycle += 7;
        }

        #region OpCode Methods

        /// <summary>
        /// Add memory to accumulator with carry
        /// </summary>
        /// <param name="mem"></param>
        private void adc(MemoryInfo mem)
        {
            byte acc = accumulator;
            byte value = RAM.ReadMemory(mem.Address);
            byte carry = carry_flag;
            accumulator = (byte)(accumulator + value + carry);
            setZero(accumulator);
            setSign(accumulator);
            int sum = accumulator + value + carry;
            if (sum > 0xFF)
                carry_flag = 1;
            else
                carry_flag = 0;
            if (((acc ^ value) & 0x80) == 0 && ((acc ^ accumulator) & 0x80) != 0)
                overflow_flag = 1;
            else
                overflow_flag = 0;
        }

        /// <summary>
        /// "AND" memory with accumulator 
        /// </summary>
        /// <param name="mem"></param>
        private void and(MemoryInfo mem)
        {
            accumulator = (byte)(accumulator & RAM.ReadMemory(mem.Address));
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// ASL Shift Left One Bit (Memory or Accumulator)
        /// </summary>
        /// <param name="mem"></param>
        private void asl(MemoryInfo mem)
        {
            if (mem.Addr_mode == (int)AddressingMode.Accumulator)
            {
                carry_flag = (byte)((accumulator >> 7) & 1);
                accumulator <<= 1;
                setZero(accumulator);
                setSign(accumulator);
            }
            else
            {
                byte value = RAM.ReadMemory(mem.Address);
                carry_flag = (byte)((value >> 7) & 1);
                value <<= 1;
                RAM.WriteMemory(mem.Address, value);
                setZero(value);
                setSign(value);
            }
        }

        /// <summary>
        /// BCC Branch on Carry Clear
        /// </summary>
        /// <param name="mem"></param>
        private void bcc(MemoryInfo mem)
        {
            if (carry_flag == 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BCS Branch on carry set 
        /// </summary>
        /// <param name="mem"></param>
        private void bcs(MemoryInfo mem)
        {
            if (carry_flag != 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BEQ Branch on result zero 
        /// </summary>
        /// <param name="mem"></param>
        private void beq(MemoryInfo mem)
        {
            if (zero_flag != 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BIT Test bits in memory with accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void bit(MemoryInfo mem)
        {
            byte value = RAM.ReadMemory(mem.Address);
            overflow_flag = (byte)((value >> 6) & 1);
            setZero((byte)(value & accumulator));
            setSign(value);
        }

        /// <summary>
        /// BMI Branch on result minus
        /// </summary>
        /// <param name="mem"></param>
        private void bmi(MemoryInfo mem)
        {
            if (sign_flag != 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BNE Branch on result not zero
        /// </summary>
        /// <param name="mem"></param>
        private void bne(MemoryInfo mem)
        {
            if (zero_flag == 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BPL Branch on result plus
        /// </summary>
        /// <param name="mem"></param>
        private void bpl(MemoryInfo mem)
        {
            if (sign_flag == 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BRK Force Break
        /// </summary>
        /// <param name="mem"></param>
        private void brk(MemoryInfo mem)
        {
            Push16(pc_register);
            php(mem);
            sei(mem);
            pc_register = Read16(0xFFFE);
        }

        /// <summary>
        /// BVC Branch on overflow clear
        /// </summary>
        /// <param name="mem"></param>
        private void bvc(MemoryInfo mem)
        {
            if (overflow_flag == 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// BVS Branch on overflow set
        /// </summary>
        /// <param name="mem"></param>
        private void bvs(MemoryInfo mem)
        {
            if (overflow_flag != 0)
            {
                pc_register = mem.Address;
                addCycles(mem);
            }
        }

        /// <summary>
        /// CLC Clear carry flag
        /// </summary>
        /// <param name="mem"></param>
        private void clc(MemoryInfo mem)
        {
            carry_flag = 0;
        }

        /// <summary>
        /// CLD Clear decimal mode
        /// </summary>
        /// <param name="mem"></param>
        private void cld(MemoryInfo mem)
        {
            decimal_flag = 0;
        }

        /// <summary>
        /// CLI Clear interrupt disable bit
        /// </summary>
        /// <param name="mem"></param>
        private void cli(MemoryInfo mem)
        {
            interrupt_flag = 0;
        }

        /// <summary>
        /// CLV Clear overflow flag
        /// </summary>
        /// <param name="mem"></param>
        private void clv(MemoryInfo mem)
        {
            overflow_flag = 0;
        }

        /// <summary>
        /// CMP Compare memory and accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void cmp(MemoryInfo mem)
        {
            Compare(accumulator, RAM.ReadMemory(mem.Address));
        }

        /// <summary>
        /// CPX Compare Memory and Index X
        /// </summary>
        /// <param name="mem"></param>
        private void cpx(MemoryInfo mem)
        {
            Compare(reg_x, RAM.ReadMemory(mem.Address));
        }

        /// <summary>
        /// CPY Compare memory and index Y
        /// </summary>
        /// <param name="mem"></param>
        private void cpy(MemoryInfo mem)
        {
            Compare(reg_y, RAM.ReadMemory(mem.Address));
        }

        /// <summary>
        /// DEC Decrement memory by one
        /// </summary>
        /// <param name="mem"></param>
        private void dec(MemoryInfo mem)
        {
            byte value = (byte)(RAM.ReadMemory(mem.Address) - 1);
            RAM.WriteMemory(mem.Address, value);
            setZero(value);
            setSign(value);
        }

        /// <summary>
        /// DEX Decrement index X by one
        /// </summary>
        /// <param name="mem"></param>
        private void dex(MemoryInfo mem)
        {
            reg_x--;
            setZero(reg_x);
            setSign(reg_x);
        }

        /// <summary>
        /// DEY Decrement index Y by one
        /// </summary>
        /// <param name="mem"></param>
        private void dey(MemoryInfo mem)
        {
            reg_y--;
            setZero(reg_y);
            setSign(reg_y);
        }

        /// <summary>
        /// EOR "Exclusive-Or" memory with accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void eor(MemoryInfo mem)
        {
            accumulator = (byte)(accumulator ^ RAM.ReadMemory(mem.Address));
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// INC Increment memory by one    
        /// </summary>
        /// <param name="mem"></param>
        private void inc(MemoryInfo mem)
        {
            byte value = (byte)(RAM.ReadMemory(mem.Address) + 1);
            RAM.WriteMemory(mem.Address, value);
            setZero(value);
            setSign(value);
        }

        /// <summary>
        /// INX Increment Index X by one 
        /// </summary>
        /// <param name="mem"></param>
        private void inx(MemoryInfo mem)
        {
            reg_x++;
            setZero(reg_x);
            setSign(reg_x);
        }

        /// <summary>
        /// INY Increment Index Y by one
        /// </summary>
        /// <param name="mem"></param>
        private void iny(MemoryInfo mem)
        {
            reg_y++;
            setZero(reg_y);
            setSign(reg_y);
        }

        /// <summary>
        /// JMP Jump to new location
        /// </summary>
        /// <param name="mem"></param>
        private void jmp(MemoryInfo mem)
        {
            pc_register = mem.Address;
        }

        /// <summary>
        /// JSR Jump to new location saving return address 
        /// </summary>
        /// <param name="mem"></param>
        private void jsr(MemoryInfo mem)
        {
            Push16((ushort)(pc_register - 1));
            pc_register = mem.Address;
        }

        /// <summary>
        /// LDA Load accumulator with memory
        /// </summary>
        /// <param name="mem"></param>
        private void lda(MemoryInfo mem)
        {
            accumulator = RAM.ReadMemory(mem.Address);
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// LDX Load index X with memory
        /// </summary>
        /// <param name="mem"></param>
        private void ldx(MemoryInfo mem)
        {
            reg_x = RAM.ReadMemory(mem.Address);
            setZero(reg_x);
            setSign(reg_x);
        }

        /// <summary>
        /// LDY Load index Y with memory 
        /// </summary>
        /// <param name="mem"></param>
        private void ldy(MemoryInfo mem)
        {
            reg_y = RAM.ReadMemory(mem.Address);
            setZero(reg_y);
            setSign(reg_y);
        }

        /// <summary>
        /// LSR Shift right one bit (memory or accumulator) 
        /// </summary>
        /// <param name="mem"></param>
        private void lsr(MemoryInfo mem)
        {
            if (mem.Addr_mode == (int)AddressingMode.Accumulator)
            {
                carry_flag = (byte)(accumulator & 1);
                accumulator >>= 1;
                setZero(accumulator);
                setSign(accumulator);
            }
            else
            {
                byte value = RAM.ReadMemory(mem.Address);
                carry_flag = (byte)(value & 1);
                value >>= 1;
                RAM.WriteMemory(mem.Address, value);
                setZero(value);
                setSign(value);
            }
        }

        /// <summary>
        /// NOP No operation
        /// </summary>
        /// <param name="mem"></param>
        private void nop(MemoryInfo mem)
        {
            //Do nothing;
        }

        /// <summary>
        /// ORA "OR" memory with accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void ora(MemoryInfo mem)
        {
            accumulator = (byte)(accumulator | RAM.ReadMemory(mem.Address));
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// PHA Push accumulator on stack
        /// </summary>
        /// <param name="mem"></param>
        private void pha(MemoryInfo mem)
        {
            push(accumulator);
        }

        /// <summary>
        /// PHP Push processor status on stack 
        /// </summary>
        /// <param name="mem"></param>
        private void php(MemoryInfo mem)
        {
            push((byte)(Flags() | 0x10));
        }

        /// <summary>
        /// PLA Pull accumulator from stack 
        /// </summary>
        /// <param name="mem"></param>
        private void pla(MemoryInfo mem)
        {
            accumulator = pull();
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// PLP Pull processor status from stack
        /// </summary>
        /// <param name="mem"></param>
        private void plp(MemoryInfo mem)
        {
            setFlags((byte)(pull() & 0xEF | 0x20));
        }

        /// <summary>
        /// ROL Rotate one bit left (memory or accumulator)
        /// </summary>
        /// <param name="mem"></param>
        private void rol(MemoryInfo mem)
        {
            if (mem.Addr_mode == (int)AddressingMode.Accumulator)
            {
                byte carry = carry_flag;
                carry_flag = (byte)((accumulator >> 7) & 1);
                accumulator = (byte)((accumulator << 1) | carry);
                setZero(accumulator);
                setSign(accumulator);
            }
            else
            {
                byte carry = carry_flag;
                byte value = RAM.ReadMemory(mem.Address);
                carry_flag = (byte)((value >> 7) & 1);
                value = (byte)((value << 1) | carry);
                RAM.WriteMemory(mem.Address, value);
                setZero(value);
                setSign(value);
            }
        }

        /// <summary>
        /// ROR Rotate one bit right (memory or accumulator)
        /// </summary>
        /// <param name="mem"></param>
        private void ror(MemoryInfo mem)
        {
            if (mem.Addr_mode == (int)AddressingMode.Accumulator)
            {
                byte carry = carry_flag;
                carry_flag = (byte)(accumulator & 1);
                accumulator = (byte)((accumulator >> 1) | (carry << 7));
                setZero(accumulator);
                setSign(accumulator);
            }
            else
            {
                byte carry = carry_flag;
                byte value = RAM.ReadMemory(mem.Address);
                carry_flag = (byte)(value & 1);
                value = (byte)((value >> 1) | (carry << 7));
                RAM.WriteMemory(mem.Address, value);
                setZero(value);
                setSign(value);
            }
        }

        /// <summary>
        /// RTI Return from interrupt 
        /// </summary>
        /// <param name="mem"></param>
        private void rti(MemoryInfo mem)
        {
            setFlags((byte)(pull() & 0xEF | 0x20));
            pc_register = Pull16();
        }

        /// <summary>
        /// RTS Return from subroutine 
        /// </summary>
        /// <param name="mem"></param>
        private void rts(MemoryInfo mem)
        {
            pc_register = (ushort)(Pull16() + 1);
        }

        /// <summary>
        /// SBC Subtract memory from accumulator with borrow
        /// </summary>
        /// <param name="mem"></param>
        private void sbc(MemoryInfo mem)
        {
            byte acc = accumulator;
            byte value = RAM.ReadMemory(mem.Address);
            byte carry = carry_flag;
            accumulator = (byte)(accumulator - value - (1 - carry));
            setZero(accumulator);
            setSign(accumulator);
            int diff = accumulator - value - (1 - carry);
            if (diff >= 0)
                carry_flag = 1;
            else
                carry_flag = 0;
            if (((acc ^ value) & 0x80) != 0 && ((acc ^ accumulator) & 0x80) != 0)
                overflow_flag = 1;
            else
                overflow_flag = 0;
        }

        /// <summary>
        /// SEC Set carry flag 
        /// </summary>
        /// <param name="mem"></param>
        private void sec(MemoryInfo mem)
        {
            carry_flag = 1;
        }

        /// <summary>
        /// SED Set decimal mode
        /// </summary>
        /// <param name="mem"></param>
        private void sed(MemoryInfo mem)
        {
            decimal_flag = 1;
        }

        /// <summary>
        /// SEI Set interrupt disable status
        /// </summary>
        /// <param name="mem"></param>
        private void sei(MemoryInfo mem)
        {
            interrupt_flag = 1;
        }

        /// <summary>
        /// STA Store accumulator in memory
        /// </summary>
        /// <param name="mem"></param>
        private void sta(MemoryInfo mem)
        {
            RAM.WriteMemory(mem.Address, accumulator);
        }

        /// <summary>
        ///  STX Store index X in memory
        /// </summary>
        /// <param name="mem"></param>
        private void stx(MemoryInfo mem)
        {
            RAM.WriteMemory(mem.Address, reg_x);
        }

        /// <summary>
        /// STY Store index Y in memory 
        /// </summary>
        /// <param name="mem"></param>
        private void sty(MemoryInfo mem)
        {
            RAM.WriteMemory(mem.Address, reg_y);
        }

        /// <summary>
        /// TAX Transfer accumulator to index X 
        /// </summary>
        /// <param name="mem"></param>
        private void tax(MemoryInfo mem)
        {
            reg_x = accumulator;
            setZero(reg_x);
            setSign(reg_x);
        }

        /// <summary>
        /// TAY Transfer accumulator to index Y
        /// </summary>
        /// <param name="mem"></param>
        private void tay(MemoryInfo mem)
        {
            reg_y = accumulator;
            setZero(reg_y);
            setSign(reg_y);
        }

        /// <summary>
        /// TSX Transfer stack pointer to index X 
        /// </summary>
        /// <param name="mem"></param>
        private void tsx(MemoryInfo mem)
        {
            reg_x = stack_pointer;
            setZero(reg_x);
            setSign(reg_x);
        }

        /// <summary>
        /// TXA Transfer index X to accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void txa(MemoryInfo mem)
        {
            accumulator = reg_x;
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// Transfer the value stored at X to the stack pointer
        /// </summary>
        /// <param name="mem"></param>
        private void txs(MemoryInfo mem)
        {
            stack_pointer = reg_x;
        }

        /// <summary>
        /// TYA Transfer index Y to accumulator
        /// </summary>
        /// <param name="mem"></param>
        private void tya(MemoryInfo mem)
        {
            accumulator = reg_y;
            setZero(accumulator);
            setSign(accumulator);
        }

        /// <summary>
        /// Illegal Opcodes below
        /// These options were not implemented in the NES. However they can be 
        /// implemented to provide a complete 6502 CPU implementation.
        /// http://nesdev.com/undocumented_opcodes.txt
        /// </summary>


        private void ahx(MemoryInfo mem)
        {
        }

        private void alr(MemoryInfo mem)
        {
        }

        private void anc(MemoryInfo mem)
        {
        }

        private void arr(MemoryInfo mem)
        {
        }

        private void axs(MemoryInfo mem)
        {
        }

        private void dcp(MemoryInfo mem)
        {
        }

        private void isc(MemoryInfo mem)
        {
        }

        private void kil(MemoryInfo mem)
        {
        }

        private void las(MemoryInfo mem)
        {
        }

        private void lax(MemoryInfo mem)
        {
        }

        private void rla(MemoryInfo mem)
        {
        }

        private void rra(MemoryInfo mem)
        {
        }

        private void sax(MemoryInfo mem)
        {
        }

        private void shx(MemoryInfo mem)
        {
        }

        private void shy(MemoryInfo mem)
        {
        }

        private void slo(MemoryInfo mem)
        {
        }

        private void sre(MemoryInfo mem)
        {
        }

        private void tas(MemoryInfo mem)
        {
        }

        private void xaa(MemoryInfo mem)
        {
        }

        #endregion OpCode Methods

        /// <summary>
        /// Prints the values held by ACC and the INDEX X registers
        /// </summary>
        public void printRegisters()
        {
            Console.WriteLine("{0,20} {1,20}", "Accumulator", "X Index Register");
            Console.WriteLine("{0,20} {1,20}", accumulator.ToString("X"), reg_x.ToString("X"));
        }

        /// <summary>
        /// Converts the current values of the resgisters and flags in the CPU to strings.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string rtn = "";
            rtn += String.Format("{0,-28}{1,4}", "Program Counter: ", pc_register.ToString("X4")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Stack Pointer: ", stack_pointer.ToString("X2")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Accumulator: ", accumulator.ToString("X2")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Register X: ", reg_x.ToString("X2")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Register Y: ", reg_y.ToString("X2")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Carry Flag: ", carry_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Zero Flag: ", zero_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Interrupt Flag: ", interrupt_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Decimal Flag: ", decimal_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Break Flag: ", break_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Unused Flag: ", unused_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Overflow Flag: ", overflow_flag.ToString("X")) + "\n";
            rtn += String.Format("{0,-28}{1,4}", "Sign Flag: ", sign_flag.ToString("X")) + "\n";

            return rtn;
        }
    }
}