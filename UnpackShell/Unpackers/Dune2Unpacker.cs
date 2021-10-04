using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class Dune2Unpacker : IUnpacker
    {
        public string GetName()
        {
            return "dune2.pak";
        }

        public string GetDescription()
        {
            return "Dune II PAK file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        private bool IsValid(string name)
        {
            string valid = "abcdefghijklmnopqrstuvwxyz" +
                    "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                    "01234567890.+-()$!";

            foreach (char c in name)
            {
                if (valid.IndexOf(c) == -1)
                    return false;                
            }

            return true;
        }

        private List<FileEntry> GetDirectory(Stream strm)
        {
            byte[] namebuf;
            BinaryReader rd;
            UInt32 startOffset;
            UInt32 offset;
            List<FileEntry> results = new List<FileEntry>();
            FileEntry ent;

            rd = new BinaryReader(strm);
            startOffset = rd.ReadUInt32();
            if ((startOffset < 2) || (startOffset > Math.Min(16 * 256, strm.Length))) // arbitrary upper limit
                return null;

            offset = startOffset;

            // read entire directory
            while (true)
            {
                if (strm.Position >= startOffset)
                    break;

                ent = new FileEntry();

                int n = 0;
                byte b;
                namebuf = new byte[13];
                // read 0-terminated name
                while (true)
                {
                    b = rd.ReadByte();
                    if (b == 0)
                        break;
                    namebuf[n++] = b;

                    if (n > 12)
                        return null;
                }
                ent.Filename = Encoding.ASCII.GetString(namebuf).Trim('\0');
                if (!IsValid(ent.Filename))
                    return null;

                ent.Offset = offset;

                offset = rd.ReadUInt32();

                results.Add(ent);
            }

            // fix file lengths
            for (int i = 0; i < results.Count - 1; i++)
            {
                results[i].UncompressedSize = results[i + 1].Offset - results[i].Offset;
            }
            results[results.Count - 1].UncompressedSize = strm.Length - results[results.Count - 1].Offset;

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            foreach (FileEntry ent in GetDirectory(strm))
            {
                yield return ent;
            }
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
