using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.ComponentModel.Composition;
using UnpackShell.Interfaces;
using Ionic.Zlib;

namespace UnpackShell.DataTransformers
{
    [Export(typeof(IDataTransformer))]
    public class ZLibDecompressor : IDataTransformer
    {
        public string GetName()
        {
            return "zlib_dec";
        }

        public string GetDescription()
        {
            return "zlib decompressor";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public void SetOption(string option, object value)
        {
        }

        public TransformationResult TransformData(byte[] InBuffer, byte[] OutBuffer, int InLength, ref int OutLength)
        {
            using (MemoryStream ostrm = new MemoryStream(OutBuffer, 0, OutLength))
            {
                using (ZlibStream strm = new ZlibStream(ostrm, CompressionMode.Decompress, true))
                {
                    MemoryStream istrm = new MemoryStream(InBuffer, 0, InLength);
                    try
                    {
                        istrm.CopyTo(strm);
                    }
                    catch
                    {
                        return TransformationResult.BufferTooSmall;
                    }
                }
                OutLength = (int)ostrm.Position;
            }
            return TransformationResult.OK;
        }
    }

    [Export(typeof(IDataTransformer))]
    public class ZLibCompressor : IDataTransformer
    {
        public string GetName()
        {
            return "zlib_cmp";
        }

        public string GetDescription()
        {
            return "zlib compressor";
        }

        public string GetVersion()
        {
            return "1.0";
        }

        public void SetOption(string option, object value)
        {
            // tbd
        }

        public TransformationResult TransformData(byte[] InBuffer, byte[] OutBuffer, int InLength, ref int OutLength)
        {
            using (MemoryStream ostrm = new MemoryStream(OutBuffer, 0, OutLength))
            {
                using (ZlibStream strm = new ZlibStream(ostrm, CompressionMode.Compress, true))
                {
                    MemoryStream istrm = new MemoryStream(InBuffer, 0, InLength);
                    try
                    {
                        istrm.CopyTo(strm);
                    }
                    catch
                    {
                        return TransformationResult.BufferTooSmall;
                    }
                }
                OutLength = (int)ostrm.Position;
            }
            return TransformationResult.OK;
        }
    }
}
