using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    // heavily inspired by http://aluigi.altervista.org/papers/bms/helldorado.bms
    [Export(typeof(IUnpacker))]
    public class VisionEngineUnpacker : IUnpacker
    {
        private byte m_XORValue = 0;

        public string GetName()
        {
            return "Trinigy/Havok/VisionEngine PAK file";
        }

        public string GetDescription()
        {
            return "visionengine.pak";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            byte[] header = new byte[14];

            strm.Read(header, 0, 14);
            return Encoding.ASCII.GetString(header, 0, 14) == "SBPAK V 1.0\r\n\0";
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> results = new List<FileEntry>();
            int magic;
            BinaryReader rd;
            int numFiles;
            int baseOffset;
            int totalSize;
            Stream xorStream;
            BinaryReader xrd;

            if (!IsSupported(strm, callbacks))
                return null;

            strm.Seek(2 + 4 + 4, SeekOrigin.Current);
            rd = new BinaryReader(strm);
            magic = rd.ReadInt32();
            m_XORValue = (byte)(~magic & 0xff);
            xorStream = new Shared.XORStream(strm, callbacks.TransformerRegistry, m_XORValue);
            xrd = new BinaryReader(xorStream);
            xrd.ReadInt32();
            xrd.ReadInt32();
            numFiles = xrd.ReadInt32();
            xrd.ReadInt32();
            baseOffset = xrd.ReadInt32();
            totalSize = xrd.ReadInt32();

            // read file info region
            for (int i = 0; i < numFiles; i++)
            {
                FileEntry ent = new FileEntry();
                ent.UncompressedSize = xrd.ReadInt32();
                ent.Offset = xrd.ReadInt32() + baseOffset;

                Int32 nameOffset = xrd.ReadByte();
                nameOffset |= (int)xrd.ReadByte() << 8;
                nameOffset |= (int)xrd.ReadByte() << 16;
                ent.LongData["nameoffset"] = nameOffset;
                xrd.ReadByte();
                xrd.ReadInt32();

                results.Add(ent);
            }
            // read the file name region
            byte[] fnames = new byte[baseOffset - 16 * numFiles - 0x34];            
            int xbyte = fnames.Length;

            xrd.Read(fnames, 0, fnames.Length);

            for (int i = 0; i < fnames.Length; i++)
            {
                byte by = (byte)(fnames[i] ^ 0xc4);
                by += (byte)(xbyte & 0xff);
                fnames[i] = by;
                xbyte--;
            }

            // get names for every file
            foreach (FileEntry ent in results)
            {
                int nameOffset = (int)ent.LongData["nameoffset"] + 2;
                StringBuilder sb = new StringBuilder();
                while (fnames[nameOffset] != 0)
                    sb.Insert(0, (char)fnames[nameOffset++]);

                ent.Filename = sb.ToString();
            }

            return results;
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            IDataTransformer xor = callbacks.TransformerRegistry.GetTransformer("xor");

            foreach (FileEntry ent in ListFiles(strm, callbacks))
            {
                xor.SetOption("value", m_XORValue);
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                byte[] buf = new byte[ent.UncompressedSize];
                strm.Read(buf, 0, buf.Length);
                if (ent.Filename.ToLower().EndsWith(".bik"))
                    callbacks.WriteData(ent.Filename, buf);
                else
                {
                    byte[] buf2 = new byte[buf.Length];
                    int len = buf.Length;
                    xor.TransformData(buf, buf2, len, ref len);
                    callbacks.WriteData(ent.Filename, buf2);
                }
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
