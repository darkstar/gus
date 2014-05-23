using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class LeafUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "leaf.pak";
        }

        public string GetDescription()
        {
            return "Leaf PAK file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        List<FileEntry> ReadDirectory(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();
            BinaryReader rd = new BinaryReader(strm);
            Int32 numFiles;

            if (rd.ReadUInt32() != 0x5041434b)
                return null;

            numFiles = rd.ReadInt32();
            if (numFiles > 100000)
                return null;

            for (int i = 0; i < numFiles; i++)
            {
                FileEntry ent = new FileEntry();
                byte[] namebuf = new byte[24];

                ent.LongData["type"] = rd.ReadUInt32(); // 0x0, 0x1 = file, 0xcccccccc = dir?
                if ((ent.LongData["type"] != 1) && (ent.LongData["type"] != 0) && (ent.LongData["type"] != 0xcccccccc))
                    return null;

                rd.Read(namebuf, 0, 24);
                ent.Filename = Encoding.GetEncoding("SJIS").GetString(namebuf).TrimEnd('\0');
                ent.Offset = rd.ReadUInt32();
                ent.UncompressedSize = rd.ReadUInt32();
                if (ent.UncompressedSize > 1024 * 1024 * 1024)
                    return null;
                if (ent.Offset > 1024 * 1024 * 1024)
                    return null;

                results.Add(ent);
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return ReadDirectory(strm, callbacks) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            return ReadDirectory(strm, callbacks);
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            byte[] buffer;

            foreach (FileEntry ent in ReadDirectory(strm, callbacks))
            {
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                if (ent.LongData["type"] == 0xcccccccc)
                {
                    // skip for now
                }
                else 
                {
                    buffer = new byte[ent.UncompressedSize];
                    strm.Read(buffer, 0, (int)ent.UncompressedSize);
                    // TODO: This is not the whole story. Some files are compressed by some form of RLE
                    // Need to figure this out
                    callbacks.WriteData(ent.Filename, buffer);
                }
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
