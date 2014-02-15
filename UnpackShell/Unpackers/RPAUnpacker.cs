using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    class PickleLoader
    {        
        public static List<FileEntry> Load(byte[] buffer, int xorkey)
        {
            List<FileEntry> results = new List<FileEntry>();
            Stack<Object> stack = new Stack<object>();
            Stack<int> marks = new Stack<int>();
            int pos = 2;
            byte opcode;
            bool stop = false;

            // we only support the binary Python Pickle Protocol 2
            if ((buffer[0] != 0x80) || (buffer[1] != 0x02))
                return results;

            while (pos < buffer.Length && !stop)
            {
                opcode = buffer[pos++];
                switch (opcode)
                {
                    case 0x7d: // '}'  EMPTY_DICT
                        stack.Push(new Dictionary<string, object>());
                        break;
                    case 0x71: // 'q'  BINPUT
                        // skip 1 additional byte
                        pos++;
                        break;
                    case 0x72: // 'r'  LONG_BINPUT
                        // skip 4 bytes
                        pos += 4;
                        break;
                    case 0x28: // '('  MARK
                        // skip
                        marks.Push(stack.Count);
                        break;
                    case 0x58: // 'X'  BINUNICODE
                        Int32 strlen = (Int32)buffer[pos] + 256 * (Int32)buffer[pos + 1] + 256 * 256 * (Int32)buffer[pos + 2] + 256 * 256 * 256 * (Int32)buffer[pos + 3];
                        string s;

                        s = Encoding.UTF8.GetString(buffer, pos + 4, strlen);
                        stack.Push(s);
                        pos += 4 + strlen;

                        break;
                    case 0x5d: // ']'  EMPTY_LIST
                        stack.Push(new List<object>());
                        break;
                    case 0x4a: // 'J'  BININT
                        Int32 value = (Int32)buffer[pos] + 256 * (Int32)buffer[pos + 1] + 256 * 256 * (Int32)buffer[pos + 2] + 256 * 256 * 256 * (Int32)buffer[pos + 3];

                        stack.Push(value);
                        pos += 4;

                        break;
                    case 0x55: // 'U'  SHORT_BINSTRING
                        byte datalen = buffer[pos++];
                        byte[] data = new byte[datalen];
                        
                        for (int i = 0; i < datalen; i++)
                            data[i] = buffer[pos++];
                        stack.Push(data);

                        break;
                    case 0x87: // '.'  BUILD_TUPLE3
                        object obj3 = stack.Pop();
                        object obj2 = stack.Pop();
                        object obj1 = stack.Pop();
                        Tuple<object, object, object> tuple = new Tuple<object,object,object>(obj1, obj2, obj3);

                        stack.Push(tuple);

                        break;
                    case 0x61: // 'a'  APPEND
                        object toAppend  = stack.Pop();
                        object theList = stack.Peek();
                        if (theList is List<object>)
                            (theList as List<object>).Add(toAppend);

                        break;
                    case 0x75: // 'u'  SETITEMS
                        int numItems = stack.Count - marks.Pop();
                        List<object> itemsToSet = new List<object>();
                        for (int i = 0; i < numItems; i++)
                            itemsToSet.Add(stack.Pop());

                        itemsToSet.Reverse();

                        // check item on stack
                        if (stack.Peek() is Dictionary<string, object>)
                        {
                            Dictionary<string, object> thedict = stack.Peek() as Dictionary<string, object>;

                            for (int i = 0; i < numItems; i += 2)
                            {
                                string key = itemsToSet[i].ToString();
                                object val = itemsToSet[i + 1];

                                thedict.Add(key, val);
                            }
                        }
                        // TODO: handle other SETITEMs (for lists etc)... 

                        break;
                    case 0x2e: // '.'  STOP
                        stop = true;
                        break;
                    default:
                        System.Diagnostics.Debugger.Break();
                        throw new NotSupportedException(String.Format("Invalid Pickle opcode 0x{0:02x}", opcode));
                }
            }
            // stack contains one dictionary (at least we hope so)
            Dictionary<string, object> dict = stack.Pop() as Dictionary<string, object>;

            if (dict == null)
                return null;

            foreach (string s in dict.Keys)
            {
                FileEntry ent = new FileEntry();
                List<object> ldata = dict[s] as List<object>;
                Tuple<object, object, object> data3;

                if (ldata == null)
                {
                    System.Diagnostics.Debugger.Break();
                }
                data3 = ldata[0] as Tuple<object, object, object>;
                if (data3 == null)
                {
                    // TODO: handle 2-tuples (as soon as I find a game which uses them)
                    return null;
                }

                ent.Filename = s;
                ent.Offset = ((int)data3.Item1) ^ xorkey;
                ent.UncompressedSize = ((int)data3.Item2) ^ xorkey;
                if ((data3.Item3 as byte[]).Length > 0)
                    ent.ObjectData["prefix"] = data3.Item3;

                results.Add(ent);
            }

            return results;
        }
    };

    [Export(typeof(IUnpacker))]
    public class RPAUnpacker : IUnpacker
    {
        public string GetName()
        {
            return "renpy.rpa";
        }

        public string GetDescription()
        {
            return "Ren'py RPA file";
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
            byte[] sig = new byte[7];
            strm.Read(sig, 0, 7);

            return Encoding.ASCII.GetString(sig, 0, 7) == "RPA-3.0";
        }

        public List<FileEntry> GetDirectory(Stream strm, IDataTransformer zlibdec)
        {
            List<FileEntry> results = new List<FileEntry>();
            byte[] headerbytes = new byte[512];
            int i = 0;
            byte b;
            string[] header;
            int IndexOffset;
            int Key;
            byte[] IndexData;
            byte[] OutIndexData;
            int OutLength = 0;

            // read header
            do
            {
                b = (byte)strm.ReadByte();
                headerbytes[i++] = b;
            } while (b != 0x0a);
            header = Encoding.ASCII.GetString(headerbytes, 0, i - 1).Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (header.Length != 3)
                return null;

            IndexOffset = Convert.ToInt32(header[1], 16);
            Key = Convert.ToInt32(header[2], 16);

            // read index data
            strm.Seek(IndexOffset, SeekOrigin.Begin);
            IndexData = new byte[strm.Length - IndexOffset];
            strm.Read(IndexData, 0, IndexData.Length);
            OutIndexData = new byte[2 * IndexData.Length];
            do
            {
                TransformationResult res;
                try
                {
                    OutLength = OutIndexData.Length;
                    res = zlibdec.TransformData(IndexData, OutIndexData, IndexData.Length, ref OutLength);
                }
                catch (Exception)
                {
                    res = TransformationResult.BufferTooSmall;
                }

                if (res != TransformationResult.BufferTooSmall)
                    break;

                // if our buffer is too small, try again with a bigger one
                OutIndexData = new byte[2 * OutIndexData.Length];
            } while (true);

            results = PickleLoader.Load(OutIndexData, Key);

            return results;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            return GetDirectory(strm, callbacks.TransformerRegistry.GetTransformer("zlib_dec"));
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            List<FileEntry> files = GetDirectory(strm, callbacks.TransformerRegistry.GetTransformer("zlib_dec"));
            byte[] data;

            foreach (FileEntry ent in files)
            {
                long toCopy = ent.UncompressedSize;
                int startIdx = 0;

                if (ent.ObjectData.ContainsKey("prefix"))
                {
                    startIdx = (ent.ObjectData["prefix"] as byte[]).Length;
                    data = new byte[toCopy + startIdx];
                    (ent.ObjectData["prefix"] as byte[]).CopyTo(data, 0);
                }
                else
                {
                    data = new byte[toCopy];
                }
                strm.Seek(ent.Offset, SeekOrigin.Begin);
                strm.Read(data, startIdx, data.Length);
                callbacks.WriteData(ent.Filename, data);
            }
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
