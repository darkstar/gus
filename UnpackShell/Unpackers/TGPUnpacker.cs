using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class TGPUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "freesia.tgp";
        }

        public string GetDescription()
        {
            return "Fairy Bloom Freesia TGP file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            byte[] buf = new byte[4];
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;

            rd.Read(buf, 0, 4);  // magic check
            if (Encoding.ASCII.GetString(buf, 0, 4) != "TGP0")
                return false;

            if (rd.ReadInt32() != 1)  // version check
                return false;

            if (rd.ReadInt32() != 0)  // should be zero
                return false;

            numFiles = rd.ReadInt32();
            if ((numFiles <= 0) || (numFiles > 100000))  // some sanity checks
                return false;

            return true;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            byte[] buf = new byte[4];
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;
            List<FileEntry> results = new List<FileEntry>();

            rd.Read(buf, 0, 4);
            if (Encoding.ASCII.GetString(buf, 0, 4) != "TGP0")
                return results;

            if (rd.ReadInt32() != 1)  // version check
                return results;

            if (rd.ReadInt32() != 0)  // should be zero
                return results;

            numFiles = rd.ReadInt32();
            buf = new byte[0x60];
            for (int i = 0; i < numFiles; i++)
            {
                FileEntry ent = new FileEntry();

                rd.Read(buf, 0, 0x60);
                ent.Filename = Encoding.ASCII.GetString(buf);
                ent.Filename = ent.Filename.Substring(0, ent.Filename.IndexOf('\0'));
                ent.Offset = rd.ReadInt64();
                ent.UncompressedSize = rd.ReadInt64();

                results.Add(ent);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            foreach (FileEntry ent in ListFiles(strm, callbacks))
            {
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                byte[] buf = new byte[ent.UncompressedSize];
                strm.Read(buf, 0, buf.Length);
                callbacks.WriteData(ent.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
