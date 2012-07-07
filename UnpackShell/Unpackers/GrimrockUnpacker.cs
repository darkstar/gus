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
        UInt32 grar_magic = 0x52415247;

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
            int NumFiles;

            ArcEntry[] results = null;
            BinaryReader rd = new BinaryReader(strm);

            if (rd.ReadUInt32() != grar_magic)
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
            BinaryReader rd = new BinaryReader(strm);

            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IsCompressed)
                {
                    strm.Seek(files[i].Offset, SeekOrigin.Begin);
                    int clen = rd.ReadInt32();
                    if (clen != files[i].Length)
                        throw new Exception(String.Format("Mismatch on file #{0} offset {1:x8}: header={2:x8} file={3:x8}", i, files[i].Offset, files[i].Length, clen));
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

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            int NumFiles = filesToPack.Count;
            BinaryWriter bw = new BinaryWriter(strm);
            uint id;
            bool compressed;
            uint CurrentOffset;
            int CompressedLength = 0;
            IDataTransformer compressor = callbacks.TransformerRegistry.GetTransformer("zlib_cmp");

            bw.Write(grar_magic);
            bw.Write(NumFiles);
            
            // first pass, write "pseudo-uncompressed" file headers
            for (int i = 0; i < NumFiles; i++)
            {
                string fn = Path.GetFileNameWithoutExtension(filesToPack[i].relativePathName);
                if (!fn.StartsWith("0x"))
                    throw new Exception(String.Format("Invalid filename '{0}', must start with hex id!", fn));

                id = UInt32.Parse(fn.Substring(2, 8), System.Globalization.NumberStyles.AllowHexSpecifier);

                bw.Write(id);
                bw.Write((UInt32)0); // offset not yet defined
                bw.Write((UInt32)0); // compressed length
                bw.Write((UInt32)filesToPack[i].fileSize);
            }

            CurrentOffset = (UInt32)strm.Position;
            // second pass, actually write the files, compressing them on demand
            for (int i = 0; i < NumFiles; i++)
            {
                // first, check if file is to be compressed
                string fn = Path.GetFileNameWithoutExtension(filesToPack[i].relativePathName);
                compressed = fn.EndsWith("P");

                // read the file and optionally compress it
                byte[] buffer = callbacks.ReadData(filesToPack[i].relativePathName);
                if (compressed)
                {
                    byte[] buffer2 = new byte[buffer.Length + 256];

                    CompressedLength = buffer2.Length;
                    compressor.TransformData(buffer, buffer2, buffer.Length, ref CompressedLength);
                    buffer = buffer2;
                }
                else
                {
                    CompressedLength = 0;
                }

                strm.Seek(8 + i * 16, SeekOrigin.Begin); // header = 8 bytes, 16 bytes per entry
                strm.Seek(4, SeekOrigin.Current); // skip ID
                bw.Write(CurrentOffset);

                if (compressed)
                    bw.Write(CompressedLength + 4);

                // finally write the file data
                strm.Seek(CurrentOffset, SeekOrigin.Begin);

                // compressed files specify the uncompressed size again here
                if (compressed)
                {
                    bw.Write((UInt32)filesToPack[i].fileSize);
                    strm.Write(buffer, 0, CompressedLength);
                }
                else
                {
                    strm.Write(buffer, 0, buffer.Length);
                }
                CurrentOffset = (uint)strm.Position;
            }
        }
    }
}
