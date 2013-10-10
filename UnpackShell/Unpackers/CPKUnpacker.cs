using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnpackShell.Interfaces;
using System.ComponentModel.Composition;

namespace UnpackShell.Unpackers
{
    [Export(typeof(IUnpacker))]
    public class CPKUnpacker : IUnpacker
    {
        enum ColumnType : byte
        {
            COLUMN_STORAGE_MASK = 0xf0,
            COLUMN_STORAGE_PERROW = 0x50,
            COLUMN_STORAGE_CONSTANT = 0x30,
            COLUMN_STORAGE_ZERO = 0x10,
            COLUMN_TYPE_MASK = 0x0f,
            COLUMN_TYPE_DATA = 0x0b,
            COLUMN_TYPE_STRING = 0x0a,
            COLUMN_TYPE_FLOAT = 0x08,
            COLUMN_TYPE_8BYTE2 = 0x07,
            COLUMN_TYPE_8BYTE = 0x06,
            COLUMN_TYPE_4BYTE2 = 0x05,
            COLUMN_TYPE_4BYTE = 0x04,
            COLUMN_TYPE_2BYTE2 = 0x03,
            COLUMN_TYPE_2BYTE = 0x02,
            COLUMN_TYPE_1BYTE2 = 0x01,
            COLUMN_TYPE_1BYTE = 0x00,
        }

        class Column
        {
            public ColumnType Type;
            public UInt32 NameString;
            // only one of these is used, and only if Type == STORAGE_CONSTANT
            public UInt32 ConstantLong;
            public byte[] ConstantBytes;
            public UInt16 ConstantShort;
            public byte ConstantByte;
            public bool IsConstant
            {
                get
                {
                    return (byte)((byte)Type & (byte)ColumnType.COLUMN_STORAGE_MASK) == (byte)ColumnType.COLUMN_STORAGE_CONSTANT;
                }
            }
            public ColumnType RawType
            {
                get
                {
                    return (ColumnType)((byte)Type & (byte)ColumnType.COLUMN_TYPE_MASK);
                }
            }
        }

        class Table
        {
            public UInt32 TableSize;
            public UInt32 SchemaOffset = 0x20;
            public UInt32 RowsOffset;
            public UInt32 StringTableOffset;
            public UInt32 DataOffset;
            public UInt32 NameString;
            public UInt16 NumColumns;
            public UInt16 RowWidth;
            public UInt32 NumRows;
            public List<Column> Columns = new List<Column>();
        }

        public string GetName()
        {
            return "cri.cpk";
        }

        public string GetDescription()
        {
            return "CRI .cpk file";
        }

        public string GetVersion()
        {
            return "0.1";
        }

        public UnpackerFlags GetFlags()
        {
            return UnpackerFlags.Experimental | UnpackerFlags.SupportsSubdirectories;
        }

        byte m_base_xor = 0x5f;
        byte m_xor = 0x5f;
        byte m_mult = 0x15;
        bool m_encrypted = false;

        private void ResetCrypt()
        {
            m_xor = m_base_xor;
        }

        private byte ReadCPKByte(Stream strm)
        {
            byte b = (byte)strm.ReadByte();

            if (m_encrypted)
            {
                b ^= m_xor;
                m_xor = (byte)((m_xor * m_mult) & 0xff);
            }

            return b;
        }

        private UInt16 ReadCPKUShort(Stream strm) // read big endian short
        {
            byte[] buf = ReadCPKBytes(strm, 2);

            return (UInt16)(((UInt16)buf[0] << 8) | ((UInt16)buf[1]));
        }

        private UInt32 ReadCPKUInt(Stream strm) // read big endian int
        {
            byte[] buf = ReadCPKBytes(strm, 4);

            return ((UInt32)buf[0] << 24) | ((UInt32)buf[1] << 16) | ((UInt32)buf[2] << 8) | (UInt32)buf[3];
        }

        private byte[] ReadCPKBytes(Stream strm, int length)
        {
            byte[] res = new byte[length];

            if (m_encrypted)
            {
                for (int i = 0; i < length; i++)
                {
                    res[i] = ReadCPKByte(strm);
                }
            }
            else
            {
                strm.Read(res, 0, length);
            }
            return res;
        }

        private Table ReadTable(Stream strm)
        {
            long TableOffset = strm.Position;
            byte[] id;
            Table result = new Table();
            long TableStart = strm.Position;

            id = ReadCPKBytes(strm, 4);
            if (Encoding.ASCII.GetString(id) != "@UTF")
            {
                m_encrypted = true;
                // re-try the read
                strm.Seek(-4, SeekOrigin.Current);
                id = ReadCPKBytes(strm, 4);
                if (Encoding.ASCII.GetString(id) != "@UTF")
                {
                    throw new Exception("@UTF signature not found in cpk file");
                }
            }
            result.TableSize = ReadCPKUInt(strm);
            result.RowsOffset = ReadCPKUInt(strm);
            result.StringTableOffset = ReadCPKUInt(strm);
            result.DataOffset = ReadCPKUInt(strm);
            result.NameString = ReadCPKUInt(strm);
            result.NumColumns = ReadCPKUShort(strm);
            result.RowWidth = ReadCPKUShort(strm);
            result.NumRows = ReadCPKUInt(strm);

            for (int i = 0; i < result.NumColumns; i++)
            {
                Column col = new Column();
                col.Type = (ColumnType)ReadCPKByte(strm);
                col.NameString = ReadCPKUInt(strm);
                if (col.IsConstant)
                {
                    // the column is constant
                    switch (col.RawType)
                    {
                        case ColumnType.COLUMN_TYPE_STRING:
                        case ColumnType.COLUMN_TYPE_4BYTE:
                        case ColumnType.COLUMN_TYPE_4BYTE2:
                        case ColumnType.COLUMN_TYPE_FLOAT: // TODO: this is wrong, but it'll do for now
                            col.ConstantLong = ReadCPKUInt(strm);
                            break;
                        case ColumnType.COLUMN_TYPE_8BYTE:
                        case ColumnType.COLUMN_TYPE_8BYTE2:
                        case ColumnType.COLUMN_TYPE_DATA:
                            col.ConstantBytes = ReadCPKBytes(strm, 8);
                            break;
                        case ColumnType.COLUMN_TYPE_2BYTE:
                        case ColumnType.COLUMN_TYPE_2BYTE2:
                            col.ConstantShort = ReadCPKUShort(strm);
                            break;
                        case ColumnType.COLUMN_TYPE_1BYTE:
                        case ColumnType.COLUMN_TYPE_1BYTE2:
                            col.ConstantByte = ReadCPKByte(strm);
                            break;
                    }
                }
                result.Columns.Add(col);
            }
            return null;
            /*
            for (int i = 0; i < result.NumRows; i++)
            {
                long row_offset = i * result.RowWidth + 8 + result.RowsOffset;
                strm.Seek(row_offset + TableStart, SeekOrigin.Begin); // TODO: remove?

                for (int j = 0; j < result.NumColumns; j++)
                {
                    if (result.Columns[j].IsConstant)
                    {
                        switch (result.Columns[j].RawType)
                        {
                        }
                    }
                    else
                    {
                    }
                }
            }
            
            return result;*/
        }

        public bool IsSupported(Stream strm, Callbacks callbacks)
        {
            byte[] id = new byte[4];

            strm.Read(id, 0, 4);
            if (Encoding.ASCII.GetString(id) != "CPK ")
                return false;
            // skip over the next 12 bytes
            strm.Seek(12, SeekOrigin.Current);

            Table tbl = ReadTable(strm);

            return false;
        }

        public IEnumerable<FileEntry> ListFiles(Stream strm, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }

        public void UnpackFiles(Stream strm, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }

        public void PackFiles(Stream strm, List<PackFileEntry> filesToPack, Callbacks callbacks)
        {
            throw new NotImplementedException();
        }
    }
}
