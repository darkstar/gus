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

        ArcEntry[] GetDirectory(Stream strm, bool CheckOnly)
        {
            ArcEntry[] results;
            BinaryReader rd = new BinaryReader(strm);
            int numFiles;

            if (rd.ReadUInt32() != 0x00534641)
                return null;

            numFiles = rd.ReadInt32();
            results = new ArcEntry[numFiles];

            for (int i = 0; i < numFiles; i++)
            {
                results[i].Offset = rd.ReadInt32();
                results[i].Length = rd.ReadInt32();
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm, true) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();
            ArcEntry[] arcFiles = GetDirectory(strm, false);

            for (int i = 0; i < arcFiles.Length; i++)
            {
                FileEntry fe = new FileEntry();
                fe.Filename = String.Format("{0,8:00000000}.raw", i);
                fe.UncompressedSize = arcFiles[i].Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] arcFiles = GetDirectory(strm, false);

            for (int i = 0; i < arcFiles.Length; i++)
            {
                string FileName = String.Format("{0,8:00000000}.raw", i);
                byte[] äpfel = new byte[arcFiles[i].Length];

                strm.Seek(arcFiles[i].Offset, SeekOrigin.Begin);
                strm.Read(äpfel, 0, arcFiles[i].Length);
                callbacks.WriteData(FileName, äpfel);
            }
        }

        public void PackFiles(Stream strm, List<string> fullPathNames, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
