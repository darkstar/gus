using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class YSFUnpacker : IUnpacker
    {
        const string ID = "This is a YS-Online data file by 'YSO File System v0.1'\0";

        struct ArcEntry
        {
            public string Filename;
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "ys.ysf";
        }

        public string GetDescription()
        {
            return "Y's Online YSF file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        ArcEntry[] GetDirectory(Stream strm, bool CheckOnly)
        {
            byte[] buf = new byte[56];
            int numFiles;
            int DirOffset;
            BinaryReader rd;
            string id, name;
            ArcEntry[] result;

            rd = new BinaryReader(strm);

            rd.Read(buf, 0, 56);
            id = Encoding.ASCII.GetString(buf);
            if (id != ID)
                return null;

            DirOffset = rd.ReadInt32();
            if ((strm.Length - DirOffset) % 64 != 0)
                return null;

            numFiles = (int)(strm.Length - DirOffset) / 64;

            result = new ArcEntry[numFiles];
            if (CheckOnly)
                return result; // only need to return non-null here

            strm.Seek(DirOffset, SeekOrigin.Begin);
            for (int i = 0; i < numFiles; i++)
            {
                byte[] nameBuf = new byte[56];

                rd.Read(nameBuf, 0, 56);
                name = Encoding.ASCII.GetString(nameBuf);
                name = name.Substring(0, name.IndexOf('\0'));

                result[i].Filename = name;
                result[i].Offset = rd.ReadInt32();
                result[i].Length = rd.ReadInt32();
            }

            return result;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm, true) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();

            foreach (ArcEntry ae in GetDirectory(strm, false))
            {
                FileEntry fe = new FileEntry();
                fe.Filename = ae.Filename;
                fe.UncompressedSize = ae.Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            foreach (ArcEntry ae in GetDirectory(strm, false))
            {
                byte[] buf = new byte[ae.Length];
                strm.Seek(ae.Offset, SeekOrigin.Begin);
                strm.Read(buf, 0, ae.Length);
                callbacks.WriteData(ae.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<string> fullPathNames, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
