using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class PACUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "nis.dwpack";
        }

        public string GetDescription()
        {
            return "NipponIchi 'DW_PACK' PAC file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        private List<FileEntry> GetDirectory(Stream strm)
        {
            byte[] idbuf = new byte[8];
            byte[] namebuf;
            BinaryReader rd;
            UInt32 numFiles;
            UInt32 OffsetDelta;
            List<FileEntry> results = new List<FileEntry>();
            FileEntry ent;

            strm.Read(idbuf, 0, 8);
            if (Encoding.ASCII.GetString(idbuf) != "DW_PACK\0")
                return null;

            rd = new BinaryReader(strm);
            if (rd.ReadUInt32() != 0)
                return null;

            numFiles = rd.ReadUInt32();
            if ((numFiles == 0) || (numFiles > 0x100000)) // arbitrary upper limit
                return null;

            rd.ReadUInt32(); // skip 8 bytes
            rd.ReadUInt32();

            OffsetDelta = numFiles * 0x120 + 0x18; // the header size

            for (uint i = 0; i < numFiles; i++)
            {
                ent = new FileEntry();

                ent.LongData["unk1"] = rd.ReadUInt32(); // lower 16 bits contain file index, upper 16 bit = 1 for compressed??

                namebuf = new byte[0x100];
                rd.Read(namebuf, 0, 0x100);
                ent.Filename = Encoding.ASCII.GetString(namebuf).Trim('\0');
                rd.ReadUInt32();
                rd.ReadUInt32();
                ent.CompressedSize = rd.ReadUInt32();
                ent.UncompressedSize = rd.ReadUInt32();

                ent.LongData["unk2"] = rd.ReadInt32();

                ent.Offset = rd.ReadInt32() + OffsetDelta;
                ent.LongData["unk3"] = rd.ReadUInt32(); // =0 except for last file which has 0x1234

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
                buf = new byte[ent.CompressedSize];
                strm.Read(buf, 0, (int)ent.CompressedSize);
                callbacks.WriteData(ent.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
