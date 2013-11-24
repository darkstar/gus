using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    //
    // based on reverse-engineering done by Malvineous
    // http://www.shikadi.net/moddingwiki/GLB_Format
    //
    [Export(typeof(IUnpacker))]
    public class GLBUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "raptor.glb";
        }

        public string GetDescription()
        {
            return "Raptor GLB file";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental;
        }

        private byte[] DecryptBuffer(byte[] buffer, string key)
        {
            byte[] result = new byte[buffer.Length];
            int keyPos = 25 % key.Length;
            byte prevByte = (byte)key[keyPos];
            byte currByte;

            for (int i = 0; i < buffer.Length; i++)
            {
                currByte = (byte)((int)buffer[i] - (int)key[keyPos]);
                keyPos = (keyPos + 1) % key.Length;
                result[i] = (byte)(((int)currByte - (int)prevByte) & 0xff);
                prevByte = buffer[i];
            }

            return result;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            BinaryReader rd = new BinaryReader(strm);

            return rd.ReadUInt32() == 0x09d19b64;
        }

        List<FileEntry> GetDirectory(Stream strm)
        {
            List<FileEntry> results = new List<FileEntry>();
            BinaryReader rd = new BinaryReader(strm);
            byte[] tocEntry = new byte[28];
            int numFiles;
            FileEntry ent;

            rd.Read(tocEntry, 0, 28);
            tocEntry = DecryptBuffer(tocEntry, "32768GLB");
            numFiles = BitConverter.ToInt32(tocEntry, 4);

            for (int i = 0; i < numFiles; i++)
            {
                rd.Read(tocEntry, 0, 28);
                tocEntry = DecryptBuffer(tocEntry, "32768GLB");

                ent = new FileEntry();
                ent.Offset = BitConverter.ToInt32(tocEntry, 4);
                ent.UncompressedSize = BitConverter.ToInt32(tocEntry, 8);
                ent.LongData["encrypted"] = BitConverter.ToInt32(tocEntry, 0);
                ent.Filename = Encoding.ASCII.GetString(tocEntry, 12, 16);
                ent.Filename = String.Format("0x{0:x8}_{1}", ent.Offset, ent.Filename.Substring(0, ent.Filename.IndexOf((char)0x00)));

                results.Add(ent);
            }

            return results;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            foreach(FileEntry ent in GetDirectory(strm))
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
                buf = new byte[ent.UncompressedSize];
                strm.Read(buf, 0, (int)ent.UncompressedSize);
                if (ent.LongData["encrypted"] == 1)
                    buf = DecryptBuffer(buf, "32768GLB");

                callbacks.WriteData(ent.Filename, buf);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
