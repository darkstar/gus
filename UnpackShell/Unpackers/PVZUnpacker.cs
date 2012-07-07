using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using UnpackShell.Shared;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class PVZUnpacker : IUnpacker
    {
        const UInt32 ID = 0x4d37bd37;
        class ArcEntry
        {
            public string FileName;
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "pvz.pak";
        }

        public string GetDescription()
        {
            return "Plats vs. Zombies PAK file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        List<ArcEntry> GetDirectory(Stream strm, bool CheckOnly)
        {
            List<ArcEntry> results = new List<ArcEntry>();
            BinaryReader rd = new BinaryReader(strm);
            uint id;
            byte fnLength;
            byte[] nameBuf;
            int offset;

            id = rd.ReadUInt32();
            if (id != 0xbac04ac0)
                return null;

            if (rd.ReadInt32() != 0)
                return null;

            if (CheckOnly)
                return results;

            while (rd.ReadByte() == 0)
            {
                ArcEntry ae = new ArcEntry();
                fnLength = rd.ReadByte();
                nameBuf = new byte[fnLength];
                rd.Read(nameBuf, 0, fnLength);
                ae.FileName = Encoding.ASCII.GetString(nameBuf);
                ae.Length = rd.ReadInt32();
                rd.ReadInt32(); // skip
                rd.ReadInt32(); // skip
                results.Add(ae);
            }

            offset = (int)strm.Position;

            for (int i = 0; i < results.Count; i++)
            {
                results[i].Offset = offset;
                offset += results[i].Length;
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            XORStream xstrm = new XORStream(strm, callbacks.TransformerRegistry, 0xf7);
            return GetDirectory(xstrm, true) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();
            XORStream xstrm = new XORStream(strm, callbacks.TransformerRegistry, 0xf7);

            foreach (ArcEntry ae in GetDirectory(xstrm, false))
            {
                FileEntry fe = new FileEntry();
                fe.Filename = ae.FileName;
                fe.UncompressedSize = ae.Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            XORStream xstrm = new XORStream(strm, callbacks.TransformerRegistry, 0xf7);

            foreach (ArcEntry ae in GetDirectory(xstrm, false))
            {
                xstrm.Seek(ae.Offset, SeekOrigin.Begin);
                byte[] buf = new byte[ae.Length];
                xstrm.Read(buf, 0, ae.Length);
                callbacks.WriteData(ae.FileName, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
