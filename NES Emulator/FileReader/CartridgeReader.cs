﻿using System;
using System.IO;

namespace NESEmu
{
    /// <summary>
    /// Cartridge reader is a helper class used to read from the .nes files and 
    /// put the data stored in the file in appropriate arrays to be accessed by the console.
    /// </summary>
    public class CartridgeReader
    {

        private const byte _firstBits = 240;
        private const int _prgromConst = 16384;
        private const int _chrromConst = 8192;
        private const int _trainerConst = 512;

        private string _filePath;
        private Cartridge cart;        
        private byte flags6;
        private byte flags7;

        public CartridgeReader(string FilePath)
        {
            this._filePath = FilePath;
        }

        /// <summary>
        /// Method for reading a Cartridge object.  It will attempt to read in Binary
        /// and read the first 16 bytes.  It will throw an error if it can't read the file or if
        /// the first  byte headers are not 'N', 'E', 'S'.  This sepcifies that we are reading an 
        /// official NES file.  It will then attempt to read the rest of the file in
        /// binary.  
        /// </summary>
        /// <returns></returns>
        public Cartridge readCart()
        {
            cart = new Cartridge();
            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(_filePath, FileMode.Open)))
                {
                    cart.Header = reader.ReadBytes(16);
                }
            }
            catch (FileNotFoundException filenotfound)
            {
                throw filenotfound;
            }
            if (cart.Header == null || cart.Header[0] != 'N' || cart.Header[1] != 'E' 
                || cart.Header[2] != 'S' || cart.Header[3] != 0x1A)
            {
                throw new Exception("Unable to open file due to incorrect format or corruption.");
            }

            //TODO: Fix reading offsets by including trainer and SRAM
            cart.Prgrom = new byte[_prgromConst * cart.Header[4]];

            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(_filePath, FileMode.Open)))
                {
                    reader.BaseStream.Position = 16;
                    cart.Prgrom = reader.ReadBytes(cart.Prgrom.Length);

                }
            }
            catch (FileNotFoundException filenotfound)
            {
                throw filenotfound;
            }

            cart.Chrrom = new byte[_chrromConst * cart.Header[5]];

            try
            {
                using (BinaryReader reader = new BinaryReader(File.Open(_filePath, FileMode.Open)))
                {
                    reader.BaseStream.Position = cart.Prgrom.Length + 16;
                    cart.Chrrom = reader.ReadBytes(cart.Chrrom.Length);

                }
            }
            catch (FileNotFoundException filenotfound)
            {
                throw filenotfound;
            }

            flags6 = cart.Header[6];
            byte mirroringByte1 = (byte)(flags6 & 1);
            byte mirroringByte2 = (byte)((flags6 >> 3) & 1);

            cart.Mirroring = (byte)(mirroringByte1 | mirroringByte2 << 1);
            cart.Save_RAM = (flags6 & 2) == 2;
            cart.Trainer_Present = (flags6 & 4) == 4;
            cart.Four_Screen_Mirroring = (flags6 & 8) == 8;

            cart.mapper = flags6 & _firstBits;
            cart.mapper = cart.mapper >> 4;

            this.flags7 = cart.Header[7];
            cart.mapper |= flags7 & _firstBits;

            if (cart.Trainer_Present)
            {
                cart.Trainer = new byte[_trainerConst];
            }

            //In order to implement the memory mapper, Save RAM has to be implemented regardless
            //of what is stated in the header. This should be handled more gracefully.
            //if (cart.Save_RAM)
            //{
            //    cart.Sram = new byte[0x2000];
            //}
            cart.Sram = new byte[0x2000];
                                 
            return cart;
        }
    }
}