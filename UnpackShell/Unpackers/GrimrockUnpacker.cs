using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class GrimrockDATUnpacker : IUnpacker
    {
        struct ArcEntry
        {
            public UInt32 id;
            public int Offset;
            public int CompressedLength;
            public int Length;
            public bool IsCompressed
            {
                get
                {
                    return CompressedLength > 0;
                }
            }
        }

        public string GetName()
        {
            return "grimrock.dat";
        }

        public string GetDescription()
        {
            return "Grimrock DAT file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.NoFilenames | UnpackerFlags.SupportsPack | UnpackerFlags.Experimental;
        }

        ArcEntry[] GetDirectory(Stream strm)
        {
            UInt32 magic = 0x52415247;
            int NumFiles;

            ArcEntry[] results = null;
            BinaryReader rd = new BinaryReader(strm);

            if (rd.ReadUInt32() != magic)
                return null;

            NumFiles = rd.ReadInt32();
            results = new ArcEntry[NumFiles];

            for (int i = 0; i < NumFiles; i++)
            {
                results[i].id = rd.ReadUInt32();
                results[i].Offset = rd.ReadInt32();
                results[i].CompressedLength = rd.ReadInt32();
                results[i].Length = rd.ReadInt32();
            }

            return results;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm) != null;
        }

        private string GenerateFilename(ArcEntry ae)
        {
            return String.Format("0x{0:x8}{1}.raw", ae.id, ae.CompressedLength > 0 ? "P" : "");
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] files = GetDirectory(strm);
            List<FileEntry> results = new List<FileEntry>();

            for (int i = 0; i < files.Length; i++)
            {
                FileEntry fe = new FileEntry();
                fe.Filename = GenerateFilename(files[i]);
                fe.UncompressedSize = files[i].Length;
                results.Add(fe);
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            ArcEntry[] files = GetDirectory(strm);
            IDataTransformer unpacker = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IsCompressed)
                {
                    strm.Seek(files[i].Offset + 4, SeekOrigin.Begin);
                    byte[] cbuf = new byte[files[i].CompressedLength - 4];
                    byte[] buf = new byte[files[i].Length];
                    int dst_len = files[i].Length;

                    strm.Read(cbuf, 0, files[i].CompressedLength - 4);
                    unpacker.TransformData(cbuf, buf, files[i].CompressedLength - 4, ref dst_len);

                    callbacks.WriteData(GenerateFilename(files[i]), buf);
                }
                else
                {
                    strm.Seek(files[i].Offset, SeekOrigin.Begin);
                    byte[] buf = new byte[files[i].Length];
                    strm.Read(buf, 0, files[i].Length);
                    callbacks.WriteData(GenerateFilename(files[i]), buf);
                }
            }
        }

        public void PackFiles(Stream strm, List<string> fullPathNames, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
