using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace UnpackShell.Interfaces
{
    public enum TransformationResult
    {
        OK = 0,
        DataError = 1,
        BufferTooSmall = 2,
    }

    // transforms data from one representation to another.
    // simplest usage would be a decompressor or decrypter
    public interface IDataTransformer
    {
        string GetName();
        string GetDescription();
        string GetVersion();
        void SetOption(string option, object value);
        TransformationResult TransformData(byte[] InBuffer, byte[] OutBuffer, int InLength, ref int OutLength);
    }

    // this is a registry that registers all data transformers.
    // this will be given to plugins so that they can query for their required transformers
    public interface IDataTransformerRegistry
    {
        IDataTransformer GetTransformer(string name);
    }

    [Flags]
    public enum UnpackerFlags
    {
        Experimental = 0x0001,
        SupportsSubdirectories = 0x0002,
        NoFilenames = 0x0004,
        SupportsTimestamps = 0x0008,
        SupportsPack = 0x0010,
    }

    public class FileEntry
    {
        public string Filename; // might have path separators embedded
        public long UncompressedSize;
        public DateTime Timestamp;  // only if SupportsTimestamps flag set, otherwise not defined
    }

    public struct Callbacks
    {
        public IDataTransformerRegistry TransformerRegistry;
        public delegate void WriteDataDelegate(string relativeFileName, byte[] data);
        public WriteDataDelegate WriteData;
    }

    public interface IUnpacker
    {
        string GetName();
        string GetDescription();
        string GetVersion();
        UnpackerFlags GetFlags();

        bool IsSupported(Stream strm, Callbacks callbacks);
        IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks);
        void UnpackFiles(Stream strm, Callbacks callbacks);
        void PackFiles(Stream strm, List<string> fullPathNames, Callbacks callbacks);
    }
}
