using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class LODUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "nwc.lod";
        }

        public string GetDescription()
        {
            return "New World Computing LOD file";
        }

        public string GetVersion()
        {
            return "0.9";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        private bool ReadUInt32(Stream strm, ref UInt32 result)
        {
            result = 0;

            for (int i = 0; i < 4; i++)
            {
                int b = strm.ReadByte();
                if (b < 0)
                    return false;

                result = (result >> 8) | (((UInt32)b) << 24);
            }

            return true;
        }

        private List<FileEntry> GetDirectory(Stream strm)
        {
            List<FileEntry> results = new List<FileEntry>();
            byte[] buf;
            string subdir;
            UInt32 DirectoryStart = 0, DirectoryLength = 0, NumFiles = 0;

            // check for "LOD\0" signature
            buf = new byte[4];
            strm.Read(buf, 0, 4);
            if (Encoding.ASCII.GetString(buf, 0, 3) != "LOD" || buf[3] != 0)
                return null;

            // check if the 'game name' is ASCII
            buf = new byte[12];
            strm.Read(buf, 0, 12);
            string name = Encoding.ASCII.GetString(buf).TrimEnd('\0');
            foreach (char c in name)
            {
                if (c < 32 || c > 127)
                    return null;
            }

            // skip some unknown bytes
            strm.Seek(256-16, SeekOrigin.Current);

            // get the subdirectory name and trim it
            buf = new byte[16];
            strm.Read(buf, 0, 16);
            subdir = Encoding.ASCII.GetString(buf);
            if (subdir.Contains("\0"))
                subdir = subdir.Substring(0, subdir.IndexOf('\0'));

            if (!ReadUInt32(strm, ref DirectoryStart))
                return null;

            if (!ReadUInt32(strm, ref DirectoryLength))
                return null;

            strm.Seek(4, SeekOrigin.Current);

            if (!ReadUInt32(strm, ref NumFiles))
                return null;

            strm.Seek(DirectoryStart, SeekOrigin.Begin);

            // read the file directory
            for (int i = 0; i < NumFiles; i++)
            {
                FileEntry ent = new FileEntry();
                UInt32 tmp = 0;
                
                buf = new byte[16];
                strm.Read(buf, 0, 16);
                string fname = Encoding.ASCII.GetString(buf);
                if (fname.Contains("\0"))
                    fname = fname.Substring(0, fname.IndexOf('\0'));
                ent.Filename = String.Format("{0}/{1}", subdir, fname);
                // read the start offset
                if (!ReadUInt32(strm, ref tmp))
                    return null;
                ent.Offset = DirectoryStart + tmp; // make sure the offset is relative to the file

                // read the size
                if (!ReadUInt32(strm, ref tmp))
                    return null;
                ent.UncompressedSize = tmp;

                // skip the next 8 bytes
                strm.Seek(8, SeekOrigin.Current);

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
            IDataTransformer decompressor = callbacks.TransformerRegistry.GetTransformer("zlib_dec");

            foreach (FileEntry ent in GetDirectory(strm))
            {
                buf = new byte[ent.UncompressedSize];
                bool decompressionFailed = false;
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                strm.Read(buf, 0, (int)ent.UncompressedSize);
                if (ent.Filename.ToLower().EndsWith(".blv") || ent.Filename.ToLower().EndsWith(".dlv") 
                    || ent.Filename.ToLower().EndsWith(".odm") || ent.Filename.ToLower().EndsWith(".ddm"))
                {
                    // transparently uncompress the 3D map files in Games.LOD if possible (some files don't pass zlib's CRC checksum)
                    Int32 compressedSize = (Int32)((UInt32)(buf[0]) | ((UInt32)(buf[1]) << 8) | ((UInt32)(buf[2]) << 16) | ((UInt32)(buf[3]) << 24));
                    Int32 uncompressedSize = (Int32)((UInt32)(buf[4]) | ((UInt32)(buf[5]) << 8) | ((UInt32)(buf[6]) << 16) | ((UInt32)(buf[7]) << 24));
                    if (compressedSize + 8 != ent.UncompressedSize)
                        throw new Exception("Invalid compressed data in LOD subfile");

                    byte[] cdata = new byte[compressedSize];
                    Array.Copy(buf, 8, cdata, 0, compressedSize);                    
                    byte[] udata = new byte[uncompressedSize];
                    try
                    {
                        decompressor.TransformData(cdata, udata, compressedSize, ref uncompressedSize);
                        buf = udata;
                    }
                    catch
                    {
                        decompressionFailed = true;
                    }
                }

                callbacks.WriteData(ent.Filename + (decompressionFailed ? ".BAD" : ""), buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
