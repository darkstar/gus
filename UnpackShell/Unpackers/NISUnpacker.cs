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
        public string GetName()
        {
            return "nis.dat";
        }

        public string GetDescription()
        {
            return "NipponIchi 'NISPACK' DAT file";
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

        private List<FileEntry> GetDirectory(Stream strm)
        {
            byte[] buf = new byte[8];
            List<FileEntry> results;
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;
            byte[] fname = new byte[32];

            strm.Read(buf, 0, 8);
            if (Encoding.ASCII.GetString(buf) != "NISPACK\0")
                return null;

            rd.ReadInt32(); // unknown, 0x10000000
            numFiles = FromBE(rd.ReadInt32());

            results = new List<FileEntry>();

            for (int i = 0; i < numFiles; i++)
            {
                FileEntry ent = new FileEntry();

                rd.Read(fname, 0, 32);
                ent.Filename = Encoding.GetEncoding("SJIS").GetString(fname);
                if (ent.Filename.Contains("\0"))
                    ent.Filename = ent.Filename.Substring(0, ent.Filename.IndexOf('\0') - 1);
                ent.Offset = FromBE(rd.ReadInt32());
                ent.UncompressedSize = FromBE(rd.ReadInt32());
                ent.StringData["Unknown"] = String.Format("0x{0:x8}", FromBE(rd.ReadInt32()));

                results.Add(ent);
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm);
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            byte[] buf;

            foreach (FileEntry ent in GetDirectory(strm))
            {
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                buf = new byte[ent.UncompressedSize];
                strm.Read(buf, 0, (int)ent.UncompressedSize);

                callbacks.WriteData(ent.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }

    [Export(typeof(IUnpacker))]
    public class NISPakUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "nis.pak";
        }

        public string GetDescription()
        {
            return "NipponIchi generic PAK file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.NoFilenames;
        }

        public List<FileEntry> GetDirectory(Stream strm)
        {
            List<FileEntry> results = new List<FileEntry>();
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;

            numFiles = rd.ReadInt32();
            // check if header is within file
            if (strm.Length < 8 * numFiles + 4)
                return null;

            // sanity check file count
            if (numFiles > 1000000)
                return null;

            for (int i = 0; i < numFiles; i++)
            {
                FileEntry ent = new FileEntry();
                ent.Offset = rd.ReadUInt32();
                ent.UncompressedSize = rd.ReadUInt32();
                ent.FileIndex = i;
                ent.Filename = String.Format("{0:000000}", i);
                results.Add(ent);
            }
            // sanity check
            for (int i = 0; i < results.Count; i++)
            {
                // check if size is within file
                if (results[i].Offset + results[i].UncompressedSize > strm.Length)
                    return null;
                // check if size is sane
                if ((results[i].Offset < 0) || (results[i].UncompressedSize < 0))
                    return null;
                if ((results[i].Offset > 2U * 1024U * 1024U * 1024U) || (results[i].UncompressedSize > 2U * 1024U * 1024U * 1024U))
                    return null;

                if (i > 0)
                {
                    // file must start after previous file
                    if (results[i].Offset < results[i - 1].Offset + results[i - 1].UncompressedSize)
                        return null;
                }
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm);
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            byte[] buf;

            foreach (FileEntry ent in GetDirectory(strm))
            {
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                buf = new byte[ent.UncompressedSize];
                strm.Read(buf, 0, (int)ent.UncompressedSize);
                callbacks.WriteData(ent.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }

}
