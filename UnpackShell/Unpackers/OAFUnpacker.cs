using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class OAFUnpacker : IUnpacker
    {
        private struct ArcEntry
        {
            public string Filename;
            public long StartOffset;
            public long CompressedLength;
            public long UncompressedLength;
            public int Flags;
        }

        public string GetName()
        {
            return "dsiege3.oaf";
        }

        public string GetDescription()
        {
            return "Dungeon Siege 3 OAF file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        private ArcEntry[] GetDirectory(Stream strm, bool CheckOnly)
        {
            BinaryReader rd = new BinaryReader(strm);
            ArcEntry[] results = null;
            byte[] idBuffer = new byte[4];
            long NameTableOffset;
            int numFiles;
            long HeaderPos, FilenamePos;
            char ch;

            strm.Read(idBuffer, 0, 4);
            if (Encoding.ASCII.GetString(idBuffer) != "OAF!")
                return null;

            rd.ReadUInt32();
            rd.ReadUInt32();
            NameTableOffset = rd.ReadInt64();
            numFiles = rd.ReadInt32();
            rd.ReadInt32();

            results = new ArcEntry[numFiles];
            if (CheckOnly)
                return results;

            FilenamePos = NameTableOffset;
            for (int i = 0; i < numFiles; i++)
            {
                results[i].StartOffset = rd.ReadInt32();
                results[i].Flags = rd.ReadInt32();
                results[i].UncompressedLength = rd.ReadInt32();
                results[i].CompressedLength = rd.ReadInt32();
                rd.ReadInt32(); // some kind of ID?
                // store current position
                HeaderPos = strm.Position;
                // jump to file name table
                strm.Seek(FilenamePos, SeekOrigin.Begin);
                results[i].Filename = "";
                do
                {
                    ch = (char)strm.ReadByte();
                    if (ch == 0)
                        break;

                    results[i].Filename += ch;
                } while (true);
                // save current position in file name table
                FilenamePos = strm.Position;
                // ...and jump back to the main entry table
                strm.Seek(HeaderPos, SeekOrigin.Begin);
            }

            return results;
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] entries = GetDirectory(strm, true);

            return entries != null;
        }

        private string NormalizeFileName(string fn)
        {
            // replace all \ with /
            string res = fn.Replace('\\', '/');

            // if path is absolute, make it relative
            if (res[1] == ':')
                res = res.Substring(3);

            return res;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] entries = GetDirectory(strm, false);
            List<FileEntry> results = new List<FileEntry>();

            for (int i = 0; i < entries.Length; i++)
            {
                FileEntry fe = new FileEntry();
                fe.Filename = NormalizeFileName(entries[i].Filename);
                fe.UncompressedSize = entries[i].UncompressedLength;
                yield return fe;
            }
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] entries = GetDirectory(strm, false);
            byte[] buf;
            long readBytes;
            IDataTransformer uncompress = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            for (int i = 0; i < entries.Length; i++)
            {
                strm.Seek(entries[i].StartOffset, SeekOrigin.Begin);
                readBytes = entries[i].Flags == 0x10000000 ? entries[i].CompressedLength : entries[i].UncompressedLength;
                buf = new byte[readBytes];

                // read (possibly compressed) data
                strm.Read(buf, 0, (int)readBytes);

                // uncompress if neccessary
                if (entries[i].Flags == 0x10000000)
                {
                    byte[] buf2 = new byte[entries[i].UncompressedLength];
                    int outlen = buf2.Length;
                    uncompress.TransformData(buf, buf2, buf.Length, ref outlen);
                    buf = buf2;
                }

                callbacks.WriteData(NormalizeFileName(entries[i].Filename), buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
