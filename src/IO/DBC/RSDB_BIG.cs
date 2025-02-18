﻿// * ************************************************************
// * * START:                                       rsdb_big.cs *
// * ************************************************************

// * ************************************************************
// *                      INFORMATIONS
// * ************************************************************
// * RSDB_BIG class for the library.
// * rsdb_big.cs
// * 
// * --
// *
// * Feel free to use this class in your projects, but don't
// * remove the header to keep the paternity of the class.
// * 
// * ************************************************************
// *                      CREDITS
// * ************************************************************
// * Originally created by CptSky (March 12th, 2012)
// * Copyright (C) 2012 CptSky
// *
// * ************************************************************

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CO2_CORE_DLL.IO.DBC
{
    /// <summary>
    /// DBC / RSDB_BIG
    /// Files: 3DMotion, ArmetMotion, ArmorMotion, HeadMotion, MiscMotion, MountMotion, WeaponMotion
    /// </summary>
    public unsafe class RSDB_BIG
    {
        private const Int32 RSDB_BIG_IDENTIFIER = 0x42445352;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public Int32 Identifier;
            public Int32 Amount;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Entry
        {
            public UInt64 UniqId;
            public UInt32 Offset;
            public fixed Byte Path[1];
        };

        private Dictionary<UInt64, IntPtr> Entries = null;

        /// <summary>
        /// Create a new RSDB_BIG instance to handle the TQ's RSDB_BIG file.
        /// </summary>
        public RSDB_BIG()
        {
            this.Entries = new Dictionary<UInt64, IntPtr>();
        }

        ~RSDB_BIG()
        {
            Clear();
        }

        /// <summary>
        /// Reset the dictionary and free all the used memory.
        /// </summary>
        public void Clear()
        {
            if (Entries != null)
            {
                lock (Entries)
                {
                    foreach (IntPtr Ptr in Entries.Values)
                        Kernel.free((Entry*)Ptr);
                }
                Entries.Clear();
            }
        }

        /// <summary>
        /// Load the specified RSDB_BIG file (in binary format) into the dictionary.
        /// </summary>
        public void LoadFromDat(String Path)
        {
            Clear();

            lock (Entries)
            {
                using (FileStream Stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Byte[] Buffer = new Byte[Kernel.MAX_BUFFER_SIZE];
                    Header* pHeader = stackalloc Header[1];

                    Stream.Read(Buffer, 0, sizeof(Header));
                    Kernel.memcpy(pHeader, Buffer, sizeof(Header));

                    if (pHeader->Identifier != RSDB_BIG_IDENTIFIER)
                        throw new Exception("Invalid RSDB_BIG Header in file: " + Path);

                    Int64 Address = 0;
                    Console.WriteLine(pHeader->Amount);
                    for (Int32 i = 0; i < pHeader->Amount; i++)
                    {
                        Console.Write("\r{0}", (Int32)((Double)(i + 1) / (Double)pHeader->Amount * 100.0));
                        Entry* pEntry = (Entry*)Kernel.calloc(sizeof(Entry));
                        Stream.Read(Buffer, 0, sizeof(Entry) - 1);
                        Kernel.memcpy(pEntry, Buffer, sizeof(Entry) - 1);

                        Address = Stream.Position;
                        Stream.Seek(pEntry->Offset, SeekOrigin.Begin);

                        StringBuilder Builder = new StringBuilder(Kernel.MAX_BUFFER_SIZE);
                        Int32 Read = Stream.ReadByte();
                        while (Read != '\0')
                        {
                            Builder.Append((Char)Read);
                            Read = Stream.ReadByte();
                        }
                        Builder.Append('\0');
                        Stream.Seek(Address, SeekOrigin.Begin);

                        Byte* pPath = Builder.ToString().ToPointer();
                        pEntry = (Entry*)Kernel.realloc(pEntry, sizeof(Entry) + Kernel.strlen(pPath));
                        Kernel.memcpy(pEntry->Path, pPath, Kernel.strlen(pPath) + 1);

                        if (!Entries.ContainsKey(pEntry->UniqId))
                            Entries.Add(pEntry->UniqId, (IntPtr)pEntry);

                        Kernel.free(pPath);
                    }
                }
            }
        }

        /// <summary>
        /// Load the specified RSDB_BIG file (in plain format) into the dictionary.
        /// </summary>
        public void LoadFromTxt(String Path)
        {
            Clear();

            lock (Entries)
            {
                Ini Ini = new Ini(Path);
                using (StreamReader Stream = new StreamReader(Path, Encoding.UTF8))
                {
                    String Line = null;
                    while ((Line = Stream.ReadLine()) != null)
                    {
                        String[] Parts = Line.Split('=');
                        if (Parts.Length != 2)
                            continue;

                        UInt64 UniqId = UInt64.Parse(Parts[0]);
                        Byte* pPath = (Parts[1].TrimEnd(new Char[] { ' ', '\t', '\0' }) + "\0").ToPointer();

                        Entry* pEntry = (Entry*)Kernel.calloc(sizeof(Entry) + Kernel.strlen(pPath));
                        pEntry->UniqId = UniqId;
                        pEntry->Offset = 0;
                        Kernel.memcpy(pEntry->Path, pPath, Kernel.strlen(pPath) + 1);

                        if (!Entries.ContainsKey(pEntry->UniqId))
                            Entries.Add(pEntry->UniqId, (IntPtr)pEntry);

                        Kernel.free(pPath);
                    }
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified RSDB_BIG file (in binary format).
        /// </summary>
        public void SaveToDat(String Path)
        {
            using (FileStream Stream = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                Byte[] Buffer = new Byte[Kernel.MAX_BUFFER_SIZE];
                IntPtr[] Pointers = new IntPtr[0];

                lock (Entries)
                {
                    Pointers = new IntPtr[Entries.Count];
                    Entries.Values.CopyTo(Pointers, 0);
                }

                Header* pHeader = stackalloc Header[1];
                pHeader->Identifier = RSDB_BIG_IDENTIFIER;
                pHeader->Amount = Pointers.Length;

                Kernel.memcpy(Buffer, pHeader, sizeof(Header));
                Stream.Write(Buffer, 0, sizeof(Header));

                UInt32 Offset = (UInt32)(sizeof(Header) + (pHeader->Amount * (sizeof(Entry) - 1)));
                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Entry* pEntry = (Entry*)Pointers[i];
                    pEntry->Offset = Offset;
                    Offset += (UInt32)Kernel.strlen(pEntry->Path) + 1;
                }

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Entry* pEntry = (Entry*)Pointers[i];
                    Kernel.memcpy(Buffer, pEntry, sizeof(Entry) - 1);
                    Stream.Write(Buffer, 0, sizeof(Entry) - 1);
                }

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Entry* pEntry = (Entry*)Pointers[i];
                    Kernel.memcpy(Buffer, pEntry->Path, Kernel.strlen(pEntry->Path) + 1);
                    Stream.Write(Buffer, 0, Kernel.strlen(pEntry->Path) + 1);
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified RSDB_BIG file (in plain format).
        /// </summary>
        public void SaveToTxt(String Path)
        {
            using (StreamWriter Stream = new StreamWriter(Path, false, Encoding.UTF8))
            {
                IntPtr[] Pointers = new IntPtr[0];

                lock (Entries)
                {
                    Pointers = new IntPtr[Entries.Count];
                    Entries.Values.CopyTo(Pointers, 0);
                }

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Entry* pEntry = (Entry*)Pointers[i];
                    Stream.WriteLine("{0}={1}", pEntry->UniqId, Kernel.cstring(pEntry->Path, Kernel.strlen(pEntry->Path) + 1));
                }
            }
        }
    }
}

// * ************************************************************
// * * END:                                         rsdb_big.cs *
// * ************************************************************