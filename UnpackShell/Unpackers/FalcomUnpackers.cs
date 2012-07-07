using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;
using Ionic.Zip;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class FSYMUnpacker : IUnpacker
    {
        struct ArcEntry
        {
            public string Name;
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "falcom.pac";
        }

        public string GetDescription()
        {
            return "FALCOM Farland Symphony PAC file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm, true) != null;
        }

        ArcEntry[] GetDirectory(Stream strm, bool CheckOnly)
        {
            byte[] buf = new byte[8];
            byte[] namebuf = new byte[16];
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;
            ArcEntry[] results;

            // read ID string
            rd.Read(buf, 0, 8);
            // ID string must be zero terminated
            if (rd.ReadByte() != 0)
                return null;
            // sanity check for number of files
            numFiles = rd.ReadInt32();
            if (numFiles < 0 || numFiles > 100000)
                return null;
            if (Encoding.ASCII.GetString(buf) != "PAC_FILE")
                return null;

            results = new ArcEntry[numFiles];

            if (CheckOnly)
                return results; // anything that is not-null will suffice here...

            for (int i = 0; i < numFiles; i++)
            {
                // read filename and size
                rd.Read(namebuf, 0, 16);
                // TODO: check if the file name encoding is correct! it's only a guess since it is definitely NOT US-ASCII
                results[i].Name = Encoding.GetEncoding("shift_jis").GetString(namebuf).TrimEnd('\0');
                results[i].Offset = rd.ReadInt32() + 4;
                if (i > 0)
                {
                    results[i - 1].Length = results[i].Offset - results[i - 1].Offset;
                }
            }
            // calculate the length of the final file
            results[numFiles - 1].Length = (int)(strm.Length - results[numFiles - 1].Offset);

            return results;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] entries;
            List<FileEntry> results = new List<FileEntry>();
            FileEntry ent;

            entries = GetDirectory(strm, false);

            foreach (ArcEntry ae in entries)
            {
                ent = new FileEntry();
                ent.Filename = ae.Name;
                ent.UncompressedSize = ae.Length;
                results.Add(ent);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] entries;
            byte[] buf;

            entries = GetDirectory(strm, false);

            foreach (ArcEntry ae in entries)
            {
                strm.Seek(ae.Offset, SeekOrigin.Begin);
                buf = new byte[ae.Length];
                strm.Read(buf, 0, ae.Length);
                callbacks.WriteData(ae.Name, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }

    [Export(typeof(IUnpacker))]
    public class FS2DatUnpacker : ZIPUnpacker
    {
        public FS2DatUnpacker()
        {
            SetupZIPParams();
        }

        public override string GetName()
        {
            return "falcom.fs2.dat";
        }

        public override string GetDescription()
        {
            return "FALCOM Farland Symphony 2 DAT file";
        }

        public override string GetVersion()
        {
            return "1.0";
        }

        public override UnpackerFlags GetFlags()
        {
            return UnpackerFlags.SupportsPack | UnpackerFlags.SupportsSubdirectories | UnpackerFlags.SupportsTimestamps;
        }

        protected override void SetupZIPParams()
        {
            // Farland Symphony 2 uses some slightly modified ZIP files
            ZipConstants.EndOfCentralDirectorySignature = 0x06050503;
            ZipConstants.ZipEntrySignature = 0x04030503;
            ZipConstants.ZipDirEntrySignature = 0x02010503;
        }
    }

    [Export(typeof(IUnpacker))]
    public class ZweiUnpacker : IUnpacker
    {
        const uint ID = 0x00bc614e;

        struct Extension
        {
            public string Ext;
            public int FirstEntryOffset;
            public int NumFiles;
        }

        class ArcEntry
        {
            public string Filename;
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "falcom.zwei.dat";
        }

        public string GetDescription()
        {
            return "FALCOM Zwei/Zwei2 DAT file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            BinaryReader rd = new BinaryReader(strm);

            return rd.ReadUInt32() == ID;
        }

        List<ArcEntry> ReadDirectory(Stream strm)
        {
            BinaryReader rd = new BinaryReader(strm);
            Extension[] exts;
            int numExts;
            List<ArcEntry> result = new List<ArcEntry>();
            byte[] buf = new byte[4];
            byte[] namebuf = new byte[8];

            rd.ReadInt32();
            numExts = rd.ReadInt32();
            exts = new Extension[numExts];
            for (int i = 0; i < numExts; i++)
            {
                rd.Read(buf, 0, 4);
                exts[i].Ext = Encoding.ASCII.GetString(buf).TrimEnd('\0');
                exts[i].FirstEntryOffset = rd.ReadInt32();
                exts[i].NumFiles = rd.ReadInt32();
            }

            for (int i = 0; i < numExts; i++)
            {
                strm.Seek(exts[i].FirstEntryOffset, SeekOrigin.Begin);
                for (int j = 0; j < exts[i].NumFiles; j++)
                {
                    ArcEntry ae = new ArcEntry();
                    rd.Read(namebuf, 0, 8);
                    ae.Filename = String.Format("{0}.{1}", Encoding.ASCII.GetString(namebuf).TrimEnd('\0'), exts[i].Ext);
                    ae.Length = rd.ReadInt32();
                    ae.Offset = rd.ReadInt32();
                    result.Add(ae);
                }
            }

            return result;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<ArcEntry> files = ReadDirectory(strm);
            List<FileEntry> results = new List<FileEntry>();
            FileEntry fe;

            foreach (ArcEntry ae in files)
            {
                fe = new FileEntry();
                fe.Filename = ae.Filename;
                fe.UncompressedSize = ae.Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            List<ArcEntry> files = ReadDirectory(strm);
            byte[] buf;

            foreach (ArcEntry ae in files)
            {
                strm.Seek(ae.Offset, SeekOrigin.Begin);
                buf = new byte[ae.Length];
                strm.Read(buf, 0, ae.Length);
                callbacks.WriteData(ae.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
