using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnpackShell.Shared
{
    // based on implementation from here
    // http://svn.jimblackler.net/jimblackler/trunk/Visual%20Studio%202005/Projects/PersistentObjects/CRCTool.cs
    // Original Copyright:
    /// Copyright (c) 2003 Thoraxcentrum, Erasmus MC, The Netherlands.
    /// 
    /// Written by Marcel de Wijs with help from a lot of others, 
    /// especially Stefan Nelwan
    /// 
    /// This code is for free. I ported it from several different sources to C#.
    /// 
    /// JB mods: made private functions that are not used externally, March 07.
    /// 
    /// For comments: Marcel_de_Wijs@hotmail.com

    public class CRC : UnpackShell.Interfaces.ICRCAlgorithm
    {
        // this holds all neccessary parameters for the various CRC algorithms
        public class CRCParameters
        {
            public string[] Names { get; private set; }
            public int Width { get; private set; }
            public ulong Polynom { get; private set; }
            public ulong Init { get; private set; }
            public bool ReflectIn { get; private set; }
            public bool ReflectOut { get; private set; }
            public ulong XOROut { get; private set; }

            public ulong CheckValue { get; private set; }

            public CRCParameters(int width, ulong poly, ulong init, bool refIn, bool refOut, ulong xorOut, ulong check, params string[] names)
            {
                Names = names;
                Width = width;
                Polynom = poly;
                Init = init;
                ReflectIn = refIn;
                ReflectOut = refOut;
                XOROut = xorOut;
                CheckValue = check;
            }
        }

        // source: http://reveng.sourceforge.net/crc-catalogue
        private static CRCParameters[] s_CRCParams = new CRCParameters[]  {
            // CRC 3
            new CRCParameters(3, 0x3, 0x7, true, true, 0x0, 0x6, 
                "CRC-3/ROHC"),

            // CRC 4
            new CRCParameters(4, 0x3, 0x0, true, true, 0x0, 0x7,
                "CRC-4/ITU"),

            // CRC 5
            new CRCParameters(5, 0x09, 0x09, false, false, 0x00, 0x00, 
                "CRC-5/EPC"),
            new CRCParameters(5, 0x15, 0x00, true, true, 0x00, 0x07,
                "CRC-5/ITU"),
            new CRCParameters(5, 0x05, 0x1f, true, true, 0x1f, 0x19, 
                "CRC-5/USB"),

            // CRC 6
            new CRCParameters(6, 0x19, 0x00, true, true, 0x00, 0x26,
                "CRC-6/DARC"),
            new CRCParameters(6, 0x03, 0x00, true, true, 0x00, 0x06,
                "CRC-6/ITU"),

            // CRC 7
            new CRCParameters(7, 0x09, 0x00, false, false, 0x00, 0x75,
                "CRC-7"),
            new CRCParameters(7, 0x4f, 0x7f, true, true, 0x00, 0x53,
                "CRC-7/ROHC"),

            // CRC 8
            new CRCParameters(8, 0x07, 0x00, false, false, 0x00, 0xf4,
                "CRC-8"),
            new CRCParameters(8, 0x39, 0x00, true, true, 0x00, 0x15,
                "CRC-8/DARC"),
            new CRCParameters(8, 0x1d, 0xff, true, true, 0x00, 0x97,
                "CRC-8/EBU"),
            new CRCParameters(8, 0x1d, 0xfd, false, false, 0x00, 0x7e,
                "CRC-8/I-CODE"),
            new CRCParameters(8, 0x07, 0x00, false, false, 0x55, 0xa1,
                "CRC-8/ITU"),
            new CRCParameters(8, 0x31, 0x00, true, true, 0x00, 0xa1,
                "CRC-8/MAXIM", "DOW-CRC"),
            new CRCParameters(8, 0x07, 0xff, true, true, 0x00, 0xd0,
                "CRC-8/ROHC"),
            new CRCParameters(8, 0x9b, 0x00, true, true, 0x00, 0x25,
                "CRC-8/WCDMA"),

            // CRC 10
            new CRCParameters(10, 0x233, 0x000, false, false, 0x000, 0x199,
                "CRC-10"),

            // CRC 11
            new CRCParameters(11, 0x385, 0x01a, false, false, 0x000, 0x5a3,
                "CRC-11"),

            // CRC 12
            new CRCParameters(12, 0x80f, 0x000, false, true, 0x000, 0xdaf,
                "CRC-12/3GPP"),
            new CRCParameters(12, 0x80f, 0x000, false, false, 0x000, 0xf5b,
                "CRC-12/DECT", "X-CRC-12"),

            // CRC 14
            new CRCParameters(14, 0x0805, 0x0000, true, true, 0x0000, 0x082d,
                "CRC-14/DARC"),

            // CRC 15
            new CRCParameters(15, 0x4599, 0x0000, false, false, 0x0000, 0x059e,
                "CRC-15"),
            new CRCParameters(15, 0x6815, 0x0000, false, false, 0x0001, 0x2566,
                "CRC-15/MPT1327"),

            // CRC 16
            new CRCParameters(16, 0x8005 , 0x0000, true, true, 0x0000, 0xbb3d,
                "CRC-16", "ARC", "CRC-IBM", "CRC-16/ARC", "CRC-16/LHA"),
            new CRCParameters(16, 0x1021, 0x1d0f, false, false, 0x0000, 0xe5cc,
                "CRC-16/AUG-CCITT", "CRC-16/SPI-FUJITSU"),
            new CRCParameters(16, 0x8005, 0x0000, false, false, 0x0000, 0xfee8,
                "CRC-16/BUYPASS", "CRC-16/VERIFONE"),
            new CRCParameters(16, 0x1021, 0xffff, false, false, 0x0000, 0x29b1,
                "CRC-16/CCITT-FALSE"),
            new CRCParameters(16, 0x8005, 0x800d, false, false, 0x0000, 0x9ecf,
                "CRC-16/DDS-110"),
            new CRCParameters(16, 0x0589, 0x0000, false, false, 0x0001, 0x007e,
                "CRC-16/DECT-R", "R-CRC-16"),
            new CRCParameters(16, 0x0589, 0x0000, false, false, 0x0000, 0x007f,
                "CRC-16/DECT-X", "X-CRC-16"),
            new CRCParameters(16, 0x3d65, 0x0000, true, true, 0xffff, 0xea82,
                "CRC-16/DNP"),
            new CRCParameters(16, 0x3d65, 0x0000, false, false, 0xffff, 0xc2b7,
                "CRC-16/EN-13757"),
            new CRCParameters(16, 0x1021, 0xffff, false, false, 0xffff, 0xd64e,
                "CRC-16/GENIBUS", "CRC-16/EPC", "CRC-16/I-CODE", "CRC-16/DARC"),
            new CRCParameters(16, 0x8005, 0x0000, true, true, 0xffff, 0x44c2,
                "CRC-16/MAXIM"),
            new CRCParameters(16, 0x1021, 0xffff, true, true, 0x0000, 0x6f91,
                "CRC-16/MCRF4XX"),
            new CRCParameters(16, 0x1021, 0xb2aa, true, true, 0x0000, 0x63d0,
                "CRC-16/RIELLO"),
            new CRCParameters(16, 0x8bb7, 0x0000, false, false, 0x0000, 0xd0db,
                "CRC-16/T10-DIF"),
            new CRCParameters(16, 0x1021, 0x89ec, true, true, 0x0000, 0x26b1,
                "CRC-16/TMS37157"),
            new CRCParameters(16, 0x8005, 0xffff, true, true, 0xffff, 0xb4c8,
                "CRC-16/USB"),
            new CRCParameters(16, 0x1021, 0xc6c6, true, true, 0x0000, 0xbf05, 
                "CRC-A"),
            new CRCParameters(16, 0x1021, 0x0000, true, true, 0x0000, 0x2189,
                "KERMIT", "CRC-16/CCITT", "CRC-16/CCITT-TRUE", "CRC-CCITT"),
            new CRCParameters(16, 0x8005, 0xffff, true, true, 0x0000, 0x4b37,
                "MODBUS"),
            new CRCParameters(16, 0x1021, 0xffff, true, true, 0xffff, 0x906e,
                "X-25", "CRC-16/IBM-SDLC", "CRC-16/ISO-HDLC", "CRC-B"),
            new CRCParameters(16, 0x1021, 0x0000, false, false, 0x0000, 0x31c3,
                "XMODEM", "ZMODEM", "CRC-16/ACORN"),

            // CRC 24
            new CRCParameters(24, 0x864cfb, 0xb704ce, false, false, 0x000000, 0x21cf02,
                "CRC-24", "CRC-24/OPENPGP"),
            new CRCParameters(24, 0x5d6dcb, 0xfedcba, false, false, 0x000000, 0x7979bd,
                "CRC-24/FLEXRAY-A"),
            new CRCParameters(24, 0x5d6dcb, 0xabcdef, false, false, 0x000000, 0x1f23b8,
                "CRC-24/FLEXRAY-B"),

            // CRC 31
            new CRCParameters(31, 0x04c11db7, 0x7fffffff, false, false, 0x7fffffff, 0x0ce9e46c,
                "CRC-31/PHILLIPS"),

            // CRC 32
            new CRCParameters(32, 0x04c11db7, 0xffffffff, true, true, 0xffffffff, 0xcbf43926, 
                "CRC-32", "CRC-32/ADCCP", "PKZIP"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, false, false, 0xffffffff, 0xfc891918, 
                "CRC-32/BZIP2", "CRC-32/AAL5", "CRC-32/DECT-B", "B-CRC-32"),
            new CRCParameters(32, 0x1edc6f41, 0xffffffff, true, true, 0xffffffff, 0xe3069283,
                "CRC-32C", "CRC-32/ISCSI", "CRC-32/CASTAGNOLI"),
            new CRCParameters(32, 0xa833982b, 0xffffffff, true, true, 0xffffffff, 0x87315576,
                "CRC-32D"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, false, false, 0x00000000, 0x0376e6e7,
                "CRC-32/MPEG-2"),
            new CRCParameters(32, 0x814141ab, 0x00000000, false, false, 0x00000000, 0x3010bf7f,
                "CRC-32Q"),
            new CRCParameters(32, 0x04c11db7, 0xffffffff, true, true, 0x00000000, 0x340bc6d9,
                "JAMCRC"),
            new CRCParameters(32, 0x000000af, 0x00000000, false, false, 0x00000000, 0xbd0be338,
                "XFER"),

            // CRC 40
            new CRCParameters(40, 0x0004820009, 0x0000000000, false, false, 0x0000000000, 0x2be9b039b9,
                "CRC-40/GSM"),
        };

        public static void DoCRCTests()
        {
            byte[] checkdata = Encoding.ASCII.GetBytes("123456789");

            foreach (CRCParameters p in s_CRCParams)
            {
                CRC foo = new CRC(p);

                if (p.Width > 7)
                {
                    // do some additional sanity checks with random data, to check if direct and table-driven algorithms match
                    Random rnd = new Random();
                    for (int i = 0; i < 1000; i++)
                    {
                        int len = rnd.Next(256);
                        byte[] buf = new byte[len];
                        for (int j = 0; j < len; j++)
                        {
                            buf[j] = (byte)(rnd.Next(256) & 0xff);
                        }
                        ulong crc1 = foo.CalculateCRCbyTable(buf, len);
                        ulong crc2 = foo.CalculateCRCdirect(buf, len);
                        if (crc1 != crc2)
                        {
                            Console.WriteLine("CRC '{0}': Table-driven and direct algorithm mismatch: table=0x{0:x8}, direct=0x{0:x8}", crc1, crc2);
                            break;
                        }
                    }
                }

                ulong crc = foo.CalculateCRC("123456789");
                if (crc != p.CheckValue)
                    Console.WriteLine("CRC '{0}': failed sanity check, expected {1:x8}, got {2:x8}", p.Names[0], p.CheckValue, crc);
                else
                    Console.WriteLine("CRC '{0}': passed", p.Names[0]);
            }
        }

        // create a well-known CRC Algorithm
        public static CRC Create(string name)
        {
            foreach (CRCParameters param in s_CRCParams)
            {
                if (param.Names.Contains(name.ToUpper()))
                    return new CRC(param);
            }

            return null;
        }

        // enumerate all CRC methods
        public static IEnumerable<string> AllCRCMethods
        {
            get
            {
                foreach (CRCParameters p in s_CRCParams)
                {
                    yield return p.Names[0];
                }
            }
        }

        private ulong m_CRCMask;
        private ulong m_CRCHighBitMask;
        private CRCParameters m_Params;
        private ulong[] m_CRCTable;

        // Construct a new CRC algorithm object
        public CRC(CRCParameters param)
        {
            m_Params = param;

            // initialize some bitmasks
            m_CRCMask = ((((ulong)1 << (m_Params.Width - 1)) - 1) << 1) | 1;
            m_CRCHighBitMask = (ulong)1 << (m_Params.Width - 1);

            if (m_Params.Width > 7)
            {
                GenerateTable();
            }
        }

        public static ulong Reflect(ulong value, int width)
        {
            // reflects the lower 'width' bits of 'value'

            ulong j = 1;
            ulong result = 0;

            for (ulong i = 1UL << (width - 1); i != 0; i >>= 1)
            {
                if ((value & i) != 0)
                {
                    result |= j;
                }
                j <<= 1;
            }
            return result;
        }

        private void GenerateTable()
        {
            ulong bit;
            ulong crc;

            m_CRCTable = new ulong[256];

            for (int i = 0; i < 256; i++)
            {
                crc = (ulong)i;
                if (m_Params.ReflectIn)
                {
                    crc = Reflect(crc, 8);
                }
                crc <<= m_Params.Width - 8;

                for (int j = 0; j < 8; j++)
                {
                    bit = crc & m_CRCHighBitMask;
                    crc <<= 1;
                    if (bit != 0) crc ^= m_Params.Polynom;
                }

                if (m_Params.ReflectIn)
                {
                    crc = Reflect(crc, m_Params.Width);
                }
                crc &= m_CRCMask;
                m_CRCTable[i] = crc;
            }
        }

        // tables work only for 8, 16, 24, 32 bit CRC
        private ulong CalculateCRCbyTable(byte[] data, int length)
        {
            ulong crc = m_Params.Init;

            if (m_Params.ReflectIn)
                crc = Reflect(crc, m_Params.Width);

            if (m_Params.ReflectIn)
            {
                for (int i = 0; i < length; i++)
                {
                    crc = (crc >> 8) ^ m_CRCTable[(crc & 0xff) ^ data[i]];
                }
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    crc = (crc << 8) ^ m_CRCTable[((crc >> (m_Params.Width - 8)) & 0xff) ^ data[i]];
                }
            }

            if (m_Params.ReflectIn ^ m_Params.ReflectOut)
            {
                crc = Reflect(crc, m_Params.Width);
            }

            crc ^= m_Params.XOROut;
            crc &= m_CRCMask;

            return crc;
        }

        private ulong CalculateCRCdirect(byte[] data, int length)
        {
            // fast bit by bit algorithm without augmented zero bytes.
            // does not use lookup table, suited for polynom orders between 1...32.
            ulong c, bit;
            ulong crc = m_Params.Init;

            for (int i = 0; i < length; i++)
            {
                c = (ulong)data[i];
                if (m_Params.ReflectIn)
                {
                    c = Reflect(c, 8);
                }

                for (ulong j = 0x80; j > 0; j >>= 1)
                {
                    bit = crc & m_CRCHighBitMask;
                    crc <<= 1;
                    if ((c & j) > 0) bit ^= m_CRCHighBitMask;
                    if (bit > 0) crc ^= m_Params.Polynom;
                }
            }

            if (m_Params.ReflectOut)
            {
                crc = Reflect(crc, m_Params.Width);
            }
            crc ^= m_Params.XOROut;
            crc &= m_CRCMask;

            return crc;
        }

        public ulong CalculateCRC(byte[] data, int length)
        {
            // table driven CRC reportedly only works for 8, 16, 24, 32 bits
            // HOWEVER, it seems to work for everything > 7 bits, so use it
            // accordingly

            /*if (m_Params.Width % 8 == 0)*/
            if (m_Params.Width > 7)
                return CalculateCRCbyTable(data, length);
            else
                return CalculateCRCdirect(data, length);
        }

        public ulong CalculateCRC(string data)
        {
            return CalculateCRC(Encoding.ASCII.GetBytes(data), data.Length);
        }
    }
}
