using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class AFSUnpacker : IUnpacker
    {
        public struct ArcEntry
        {
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "grandia2.afs";
        }

        public string GetDescription()
        {
            return "Grandia II AFS file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.NoFilenames;
        }

        List<FileEntry> GetDirectory(Stream strm)
        {
            List<FileEntry> results = new List<FileEntry>();
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;

            if (rd.ReadUInt32() != 0x00534641)
                return null;

            numFiles = rd.ReadInt32();

            for (int i = 0; i < numFiles; i++)
            {
                FileEntry fe = new FileEntry();
                fe.Offset = rd.ReadInt32();
                fe.UncompressedSize = rd.ReadInt32();
                fe.Filename = String.Format("{0,8:00000000}.raw", i);

                results.Add(fe);
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
            foreach (FileEntry fe in GetDirectory(strm))
            {
                byte[] buf = new byte[fe.UncompressedSize];

                strm.Seek(fe.Offset, SeekOrigin.Begin);
                strm.Read(buf, 0, (int)fe.UncompressedSize);
                callbacks.WriteData(fe.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
