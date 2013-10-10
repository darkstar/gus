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
            throw new NotImplementedException();
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}