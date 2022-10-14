using System;
using System.Collections.Generic;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class XeenUnpacker : IUnpacker
    {
        public string GetDescription()
        {
            return "Xeen CC file";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.NoFilenames | UnpackerFlags.Experimental;
        }

        public string GetName()
        {
            return "xeen.cc";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        void Decrypt(byte[] buffer)
        {
            byte al;
            byte ah = 0xac;

            for (int i = 0; i < buffer.Length; i++)
            {
                al = buffer[i];
                al = (byte)(((int)al << 2) | ((int)al >> 6) & 0xff);
                al = (byte)((al + ah) & 0xff);
                buffer[i] = al;
                ah = (byte)((ah + 0x67) & 0xff);
            }
        }

        List<FileEntry> GetDirectory(Stream strm)
        {
            int numFiles;
            byte[] directory;
            List<FileEntry> results = new List<FileEntry>();
            byte xor = 0x35;

            BinaryReader rd = new BinaryReader(strm);

            numFiles = rd.ReadInt16();

            // arbitrary sanity check
            if (numFiles > 10000)
                return null;

            /* those are MM4/MM5 files DARK.CC, INTRO.CC, XEEN.CC. All other files are probably unscrambled */
            if ((numFiles != 0x493) && (numFiles != 0x125) && (numFiles != 0x3d7))
                xor = 0x00;

            // the stream is not large enough for the directory
            if (strm.Length < 8 * numFiles)
                return null;

            directory = new byte[8 * numFiles];
            rd.Read(directory, 0, 8 * numFiles);

            Decrypt(directory);
            for (int i = 0; i < numFiles; i++)
            {
                FileEntry fe = new FileEntry();

                int diroffset = 8 * i;
                fe.LongData["xor"] = xor;
                fe.LongData["hash"] = (directory[diroffset + 0] + (directory[diroffset + 1] << 8));
                fe.Offset = directory[diroffset + 2] + (directory[diroffset + 3] << 8) + (directory[diroffset + 4] << 16);
                fe.CompressedSize = directory[diroffset + 5] + (directory[diroffset + 6] << 8);
                fe.UncompressedSize = fe.CompressedSize;
                fe.FileIndex = i;
                fe.Filename = String.Format("file_{0:x4}.raw", fe.LongData["hash"]);

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
            IDataTransformer xor = callbacks.TransformerRegistry.GetTransformer("xor");

            foreach (FileEntry fe in GetDirectory(strm))
            {
                byte[] buf = new byte[fe.UncompressedSize];
                byte[] outbuf = new byte[fe.UncompressedSize];
                int outlen = (int)fe.UncompressedSize;

                xor.SetOption("value", fe.LongData["xor"]);
                strm.Seek(fe.Offset, SeekOrigin.Begin);
                strm.Read(buf, 0, (int)fe.UncompressedSize);
                xor.TransformData(buf, outbuf, buf.Length, ref outlen);
                callbacks.WriteData(fe.Filename, outbuf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }

    }
}
