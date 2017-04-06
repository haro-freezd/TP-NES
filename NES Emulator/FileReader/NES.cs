﻿using NESEmu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//TODO: Fix namespaces, they are all over the place right now.
namespace NESEmu
{
    class NES
    {
        public Cartridge Cart { get; }
        public CPU6502 CPU { get; }
        public Memory RAM { get; }
        public PPU PPU { get; }
        public Mapper Mapper { get; }


        public NES()
        {
            Cart = new Cartridge();
            const string FileName = @"C:\Users\panda\Downloads\Super Mario Bros. 3 (USA).nes";
            //TODO:Fix this
            PPU ppu = new PPU();
            Cart = Cart.getCart(FileName);
            Mapper = new MMC3(Cart, ppu);
            //Create and pass the mapper to the memory
            RAM = new Memory(2048, Mapper);
            //Pass the memory to the CPU & PPU
            CPU = new CPU6502(RAM);            
        }

        public void reset()
        {
            CPU.Reset();
        }

        public void Start()
        {
            CPU.start();
        }        
    }
}
