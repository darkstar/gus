using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;


namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class YPACUnpacker : IUnpacker
    {
        struct ArcEntry
        {
            public string FileName;
            public int Offset;
            public int Length;
        }

        public string GetName()
        {
            return "ethervapor.pac";
        }

        public string GetDescription()
        {
            return "EtherVapor PAC file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.SupportsPack | UnpackerFlags.SupportsSubdirectories;
        }

        ArcEntry[] GetDirectory(Stream strm)
        {
            BinaryReader rd = new BinaryReader(strm);
            byte[] buf = new byte[4];
            Int32 ver;
            Int32 numFiles;
            ArcEntry[] results;

            rd.Read(buf, 0, 4);
            ver = rd.ReadInt32();

            if (ver != 1)
                return null;

            if (Encoding.ASCII.GetString(buf) != "YPAC")
                return null;

            numFiles = rd.ReadInt32();
            rd.ReadInt32(); // dummy read
            results = new ArcEntry[numFiles];

            for (int i = 0; i < numFiles; i++)
            {
                byte[] nameBuf = new byte[6 * 16];
                string fname;

                rd.Read(nameBuf, 0, 6*16);
                fname = Encoding.ASCII.GetString(nameBuf);
                fname = fname.Substring(0, fname.IndexOf('\0'));  // remove everything from the first \0 char onwards
                fname = fname.Substring(1); // remove leading "\"
                results[i].FileName = fname;
                results[i].Offset = (int)rd.ReadInt64();
                results[i].Length = (int)rd.ReadUInt64();
            }
            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] files = GetDirectory(strm);
            List<FileEntry> results = new List<FileEntry>();

            for (int i = 0; i < files.Length; i++)
            {
                FileEntry fe = new FileEntry();
                fe.Filename = files[i].FileName;
                fe.UncompressedSize = files[i].Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] files = GetDirectory(strm);

            foreach (ArcEntry ae in files)
            {
                byte[] buf = new byte[ae.Length];

                strm.Seek(ae.Offset, SeekOrigin.Begin);
                strm.Read(buf, 0, ae.Length);
                callbacks.WriteData(ae.FileName, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            BinaryWriter bw = new BinaryWriter(strm);
            Int32 FileOffset;

            bw.Write(new char[] { 'Y', 'P', 'A', 'C' });
            bw.Write((Int32)1);
            bw.Write((Int32)filesToPack.Count);
            bw.Write((Int32)0);

            // write the directory
            FileOffset = 16 + 7 * 16 * filesToPack.Count; // this will be the offset for the first file
            for (int i = 0; i < filesToPack.Count; i++)
            {
                string targetFileName = "\\" + filesToPack[i];
                byte[] nameBuf = new byte[6 * 16];
                Int64 Length;

                Encoding.ASCII.GetBytes(targetFileName, 0, targetFileName.Length, nameBuf, 0);
                bw.Write(nameBuf);
                bw.Write((Int64)FileOffset);
                Length = filesToPack[i].fileSize;
                bw.Write((Int64)Length);
                FileOffset += (Int32)Length;
            }

            // write the actual file data
            for (int i = 0; i < filesToPack.Count; i++)
            {
                byte[] fileData = callbacks.ReadData(filesToPack[i].relativePathName);
                strm.Write(fileData, 0, fileData.Length);
            }
        }
    }
}
