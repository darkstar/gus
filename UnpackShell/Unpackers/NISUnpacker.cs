using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class NISUnpacker : IUnpacker
    {
        private class NisPackEntry
        {
            public string FileName;
            public int StartOffset;
            public int Length;
            public int Checksum; // not sure
        }

        public string GetName()
        {
            return "nis.dat";
        }

        public string GetDescription()
        {
            return "NipponIchi NISPACK .dat file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        private int FromBE(int val)
        {
            return System.Net.IPAddress.NetworkToHostOrder(val);
        }

        private NisPackEntry[] GetDirectory(Stream strm)
        {
            byte[] buf = new byte[8];
            NisPackEntry[] results;
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;
            byte[] fname = new byte[32];

            strm.Read(buf, 0, 8);
            if (Encoding.ASCII.GetString(buf) != "NISPACK\0")
                return null;

            rd.ReadInt32(); // unknown, 0x10000000
            numFiles = FromBE(rd.ReadInt32());

            results = new NisPackEntry[numFiles];

            for (int i = 0; i < numFiles; i++)
            {
                rd.Read(fname, 0, 32);
                results[i] = new NisPackEntry();
                results[i].FileName = Encoding.ASCII.GetString(fname);
                if (results[i].FileName.Contains("\0"))
                    results[i].FileName = results[i].FileName.Substring(0, results[i].FileName.IndexOf('\0') - 1);
                results[i].StartOffset = FromBE(rd.ReadInt32());
                results[i].Length = FromBE(rd.ReadInt32());
                results[i].Checksum = FromBE(rd.ReadInt32());
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            NisPackEntry[] dir = GetDirectory(strm);

            foreach (NisPackEntry ent in dir)
            {
                FileEntry fe = new FileEntry();
                fe.Filename = ent.FileName;
                fe.UncompressedSize = ent.Length;
                yield return fe;
            }
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            NisPackEntry[] dir = GetDirectory(strm);
            byte[] buf;

            foreach (NisPackEntry ent in dir)
            {
                strm.Seek(ent.StartOffset, SeekOrigin.Begin);
                buf = new byte[ent.Length];
                strm.Read(buf, 0, ent.Length);

                callbacks.WriteData(ent.FileName, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
