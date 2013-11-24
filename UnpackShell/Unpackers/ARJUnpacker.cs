using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class ARJUnpacker : IUnpacker
    {
        class ArchiveHeader
        {
            public ushort BasicHeaderSize;
            public byte FirstHeaderSize;
            public byte ArchiverVersion;
            public byte MinVersionToExtract;
            public byte HostOS;
            public byte ARJFlags;
            public byte SecurityVersion;
            public byte FileType;
            public DateTime ArchiveCreated;
            public DateTime ArchiveModified;
            public uint ArchiveSize;
            public uint SecurityEnvelopePosition;
            public ushort FilespecPosition;
            public ushort SecurityEnvelopeLength;
            public byte Encryption;
            public byte LastChapter;
            public string OriginalFileName;
            public string Comment;
            public UInt32 BasicHeaderCRC;
        }

        class FileHeader
        {
            public ushort BasicHeaderSize;
            public byte FirstHeaderSize;
            public byte ArchiverVersion;
            public byte MinVersionToExtract;
            public byte HostOS;
            public byte ARJFlags;
            public byte Method;
            public byte FileType;
            public DateTime FileModified;
            public UInt32 CompressedSize;
            public UInt32 OriginalSize;
            public UInt32 CRC;
            public UInt16 FilespecPosition;
            public UInt16 FileAccessMode;
            public byte FirstChapter;
            public byte LastChapter;
            public string Filename;
            public string Comment;
            public UInt32 BasicHeaderCRC;
            public long FileDataStartOffset; // calculated from stream position, not read from header!
        }

        // this class handles decoding methods 1-3
        class Decoder
        {
            const int CODE_BIT = 16;
            const int THRESHOLD = 3;
            const int DICSIZ = 26624;
            const int FDICSIZ = 32768;    /* decode_f() dictionary size */
            const int DICSIZ_MAX = 32750;
            const int BUFSIZ_DEFAULT = 16384;
            const int MAXDICBIT = 16;
            const int MATCHBIT = 8;
            const int MAXMATCH = 256;
            const int NC = (255 + MAXMATCH + 2 - THRESHOLD);
            const int NP = (MAXDICBIT+1);
            const int CBIT = 9;
            const int NT = (CODE_BIT+3);
            const int PBIT = 5;
            const int TBIT = 5;
            static int NPT = Math.Max(NT, NP);

            const int CTABLESIZE = 4096;
            const int PTABLESIZE = 256;

            const int STRTP = 9;
            const int STOPP = 13;

            const int STRTL = 0;
            const int STOPL = 7;

            const int PUTBIT_SIZE = 512;

            static ushort[] left = new ushort[2 * NC - 1];
            static ushort[] right = new ushort[2 * NC - 1];

            static ushort[] MakeTable(int nchar, ref byte[] bitlen, int tableBits, int tableSize)
            {
                ushort[] result = new ushort[1 << tableBits];
                ushort[] count = new ushort[17];
                ushort[] weight = new ushort[17];
                ushort[] start = new ushort[18];
                ushort jutbits, val;
                int i, k, avail, len;
                UInt32 nextcode, mask;
                // this is a crude workaround for the usage of pointers in decode.c
                int pIdx;
                char pArray; // either "l" for left, "r" for right or "x" for result

                for (i = 0; i < 17; i++)
                    count[i] = 0;
                
                for (i = 0; i < nchar; i++)
                    count[bitlen[i]]++;
                
                start[1] = 0;
                
                for (i = 1; i < 17; i++)
                    start[i + 1] = (ushort)(start[i] + ((uint)count[i] << (16 - i)));

                //if (start[17] != (ushort)(1<<16)) 
                if (start[17] != 0)
                {
                    throw new Exception("Bad decode table");
                }

                jutbits = (ushort)(16 - tableBits);
                for (i = 1; i < tableBits + 1; i++)
                {
                    start[i] >>= jutbits;
                    weight[i] = (ushort)(1 << (tableBits - i));
                }
                while (i <= 16)
                {
                    weight[i] = (ushort)(1 << (16 - i));
                    i++;
                } 

                i = start[tableBits+1] >> jutbits;
                
                //if(i != (ushort)(1<<16))
                if (i != 0)
                {
                    k = 1 << tableBits;
                    while (i != k)
                    {
                        result[i++] = 0;
                    }
                }

                avail = nchar;
                mask = (uint)(1 << (15 - tableBits));

                for (int ch = 0; ch < nchar; ch++)
                {
                    len = bitlen[ch];
                    if (len != 0)
                    {
                        k = start[nchar];
                        nextcode = (uint)k + weight[len];
                        if (len <= tableBits)
                        {
                            if (nextcode > tableSize)
                                throw new Exception("Bad decode table");

                            for (i = start[len]; i < nextcode; i++)
                            {
                                result[i] = (ushort)ch;
                            }
                        }
                        else
                        {
                            // p = &table[k >> jutbits];
                            pIdx = k >> jutbits;
                            pArray = 'x';

                            i = len - tableBits;

                            while (i != 0)
                            {
                                // if (*p == 0)
                                val = (pArray == 'l' ? left[pIdx] : pArray == 'r' ? right[pIdx] : result[pIdx]);
                                if (val == 0)
                                {
                                    right[avail] = left[avail] = 0;
                                    // *p = avail;
                                    switch (pArray)
                                    {
                                        case 'l': left[pIdx] = (ushort)avail; break;
                                        case 'r': right[pIdx] = (ushort)avail; break;
                                        default: result[pIdx] = (ushort)avail; break;
                                    }
                                    avail++;
                                }
                                if ((k & mask) != 0)
                                {
                                    // p=&right[*p]; 
                                    pIdx = (pArray == 'l' ? left[pIdx] : pArray == 'r' ? right[pIdx] : result[pIdx]);
                                    pArray = 'r';
                                }
                                else
                                {
                                    // p=&left[*p];
                                    pIdx = (pArray == 'l' ? left[pIdx] : pArray == 'r' ? right[pIdx] : result[pIdx]);
                                    pArray = 'l';
                                }
                                k <<= 1;
                                i--;
                            }
                            // *p = ch;
                            switch (pArray)
                            {
                                case 'l': left[pIdx] = (ushort)ch; break;
                                case 'r': right[pIdx] = (ushort)ch; break;
                                default: result[pIdx] = (ushort)ch; break;
                            }
                        }
                        start[len] = (ushort)nextcode;
                    }
                }

                return result;
            }

            static byte[] Decode(byte[] buffer, long outputSize)
            {
                byte[] result = new byte[outputSize];

                // TODO: implement :)
                return result;
            }
        }

        private ICRCAlgorithm CRCCalculator = null;

        private DateTime FromDOSDateTime(UInt32 dosDateTime)
        {
            return new DateTime(
                1980 + (int)((dosDateTime >> 25) & 0x7fU),
                (int)((dosDateTime >> 21) & 0xfU),
                (int)((dosDateTime >> 16) & 0x1fU),
                (int)((dosDateTime >> 11) & 0x1fU),
                (int)((dosDateTime >> 5) & 0x3fU),
                (int)(dosDateTime  & 0x1fU) * 2);
        }

        private string ReadASCIIZ(BinaryReader rd)
        {
            StringBuilder sb = new StringBuilder();
            byte b;

            do
            {
                try
                {
                    b = rd.ReadByte();
                }
                catch (EndOfStreamException)
                {
                    return sb.ToString();
                }
                if (b == 0)
                    break;
                sb.Append((char)b);
            } while (true);

            return sb.ToString();
        }

        private ArchiveHeader ReadArchiveHeader(BinaryReader rd)
        {
            UInt16 id = rd.ReadUInt16();
            byte[] BasicHeader;
            ArchiveHeader result = new ArchiveHeader();
            BinaryReader hr;

            if (id != 0xea60)
                return null;

            result.BasicHeaderSize = rd.ReadUInt16();
            if (result.BasicHeaderSize > 2600)
                return null;

            BasicHeader = new byte[result.BasicHeaderSize];
            rd.Read(BasicHeader, 0, result.BasicHeaderSize);
            result.BasicHeaderCRC = rd.ReadUInt32();
            // read/skip extended headers
            UInt16 extHdrSize = rd.ReadUInt16();
            // TODO: check if extended header occurs at all in any archive
            if (extHdrSize > 0)
            {
                for (int i = 0; i < extHdrSize; i++)
                    rd.ReadByte();
            }
            hr = new BinaryReader(new MemoryStream(BasicHeader));
            result.FirstHeaderSize = hr.ReadByte();
            result.ArchiverVersion = hr.ReadByte();
            result.MinVersionToExtract = hr.ReadByte();
            result.HostOS = hr.ReadByte();
            result.ARJFlags = hr.ReadByte();
            if ((result.ARJFlags & 0x4d) != 0)
                return null; // unsupported archive flags (protected, multi-volume, garbled, secured)
            result.SecurityVersion = hr.ReadByte();
            result.FileType = hr.ReadByte();
            if (result.FileType != 2)
                return null;
            hr.ReadByte(); // reserved
            result.ArchiveCreated = FromDOSDateTime(hr.ReadUInt32());
            result.ArchiveModified = FromDOSDateTime(hr.ReadUInt32());
            result.ArchiveSize = hr.ReadUInt32();
            result.SecurityEnvelopePosition = hr.ReadUInt32();
            result.FilespecPosition = hr.ReadUInt16();
            result.SecurityEnvelopeLength = hr.ReadUInt16();
            result.Encryption = hr.ReadByte();
            if (result.Encryption != 0)
                return null; // encrypted archives not supported
            result.LastChapter = hr.ReadByte();      
            // skip over "extra data"
            for (int i = 0; i < result.FirstHeaderSize - 0x1e; i++)
                hr.ReadByte(); // ignore extra data

            result.OriginalFileName = ReadASCIIZ(hr);
            result.Comment = ReadASCIIZ(hr);
            if (result.BasicHeaderSize != result.FirstHeaderSize + result.OriginalFileName.Length + result.Comment.Length + 2)
                return null;

            ulong crc = CRCCalculator.CalculateCRC(BasicHeader, BasicHeader.Length);
            // in the end, this should catch all false positives :)
            if (crc != result.BasicHeaderCRC)
                return null;

            return result;
        }

        private FileHeader ReadFileHeader(BinaryReader rd)
        {
            UInt16 id = rd.ReadUInt16();
            byte[] BasicHeader;
            FileHeader result = new FileHeader();
            BinaryReader hr;

            if (id != 0xea60)
                return null;

            result.BasicHeaderSize = rd.ReadUInt16();
            if (result.BasicHeaderSize > 2600)
                return null;  // invalid header

            if (result.BasicHeaderSize == 0)
                return null; // end of archive

            BasicHeader = new byte[result.BasicHeaderSize];
            rd.Read(BasicHeader, 0, result.BasicHeaderSize);
            result.BasicHeaderCRC = rd.ReadUInt32();
            // read/skip extended headers
            UInt16 extHdrSize = rd.ReadUInt16();
            // TODO: check if extended header occurs at all in any archive
            if (extHdrSize > 0)
            {
                for (int i = 0; i < extHdrSize; i++)
                    rd.ReadByte();
            }
            // store current position in header
            result.FileDataStartOffset = rd.BaseStream.Position;

            hr = new BinaryReader(new MemoryStream(BasicHeader));
            result.FirstHeaderSize = hr.ReadByte();
            result.ArchiverVersion = hr.ReadByte();
            result.MinVersionToExtract = hr.ReadByte();
            result.HostOS = hr.ReadByte();
            result.ARJFlags = hr.ReadByte();
            if ((result.ARJFlags & 0x0d) != 0)
                return null; // unsupported archive flags (garbled, multi-volume, extended)
            result.Method = hr.ReadByte();
            if (result.Method > 4 && result.Method != 8 && result.Method != 9)
                return null; // invalid compression method
            result.FileType = hr.ReadByte();
            if (result.FileType == 2 || result.FileType > 5)
                return null; // invalid file type

            hr.ReadByte(); // reserved

            result.FileModified = FromDOSDateTime(hr.ReadUInt32());
            result.CompressedSize = hr.ReadUInt32();
            result.OriginalSize = hr.ReadUInt32();
            result.CRC = hr.ReadUInt32();
            result.FilespecPosition = hr.ReadUInt16();
            result.FileAccessMode = hr.ReadUInt16();
            result.FirstChapter = hr.ReadByte();
            result.LastChapter = hr.ReadByte();

            // skip over "extra data"
            for (int i = 0; i < result.FirstHeaderSize - 0x1e; i++)
                hr.ReadByte(); // ignore extra data

            result.Filename = ReadASCIIZ(hr);
            result.Comment = ReadASCIIZ(hr);
            if (result.BasicHeaderSize != result.FirstHeaderSize + result.Filename.Length + result.Comment.Length + 2)
                return null;

            ulong crc = CRCCalculator.CalculateCRC(BasicHeader, BasicHeader.Length);
            // in the end, this should catch all false positives :)
            if (crc != result.BasicHeaderCRC)
                return null;

            return result;
        }

        public string GetName()
        {
            return "arj";
        }

        public string GetDescription()
        {
            return "generic ARJ file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.SupportsSubdirectories | UnpackerFlags.SupportsTimestamps | UnpackerFlags.Experimental;
        }

        private List<FileEntry> GetDirectory(Stream strm)
        {
            List<FileEntry> results = new List<FileEntry>();
            BinaryReader rd = new BinaryReader(strm);
            ArchiveHeader ahdr;
            FileHeader fhdr;
            int i = 0;

            ahdr = ReadArchiveHeader(rd);
            if (ahdr == null)
                return null;

            fhdr = ReadFileHeader(rd);
            while (fhdr != null)
            {
                FileEntry ent = new FileEntry();
                ent.FileIndex = i++;
                ent.Filename = fhdr.Filename;
                ent.CompressedSize = fhdr.CompressedSize;
                ent.UncompressedSize = fhdr.OriginalSize;
                ent.Timestamp = fhdr.FileModified;
                ent.ObjectData["FileHeader"] = fhdr;
                rd.BaseStream.Seek(fhdr.CompressedSize, SeekOrigin.Current);

                results.Add(ent);

                // read next header
                fhdr = ReadFileHeader(rd);
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            if (CRCCalculator == null)
                CRCCalculator = callbacks.GetCRCAlgorithm("CRC-32");
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            if (CRCCalculator == null) // should not happen, but who knows...
                CRCCalculator = callbacks.GetCRCAlgorithm("CRC-32");

            return GetDirectory(strm);
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            if (CRCCalculator == null) // should not happen, but who knows...
                CRCCalculator = callbacks.GetCRCAlgorithm("CRC-32");
            List<FileEntry> files = GetDirectory(strm);

            foreach (FileEntry ent in files)
            {
                FileHeader fh = ent.ObjectData["FileHeader"] as FileHeader;
                byte[] buf = new byte[fh.CompressedSize];

                strm.Seek(fh.FileDataStartOffset, SeekOrigin.Begin);
                if (strm.Read(buf, 0, (int)fh.CompressedSize) != fh.CompressedSize)
                    throw new Exception("Read error");

                switch (fh.Method)
                {
                    case 0:
                        // uncompressed data
                        callbacks.WriteData(fh.Filename, buf);
                        break;
                    case 1:
                    case 2:
                    case 3:
                        // decode
                        break;
                    case 4:
                        break;
                    default:
                        throw new Exception("Unknown pack method");
                }
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}