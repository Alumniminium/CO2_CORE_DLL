﻿// * ************************************************************
// * * START:                                          simo.cs *
// * ************************************************************

// * ************************************************************
// *                      INFORMATIONS
// * ************************************************************
// * SIMO class for the library.
// * simo.cs
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
    /// DBC / SIMO
    /// Files: 3DSimpleObj
    /// </summary>
    public unsafe class SIMO
    {
        public const Int32 MAX_NAMESIZE = 0x20;

        private const Int32 SIMO_IDENTIFIER = 0x4F4D4953;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public Int32 Identifier;
            public Int32 Amount;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Entry
        {
            public Int32 UniqId;
            public Int32 Amount;
            public fixed Byte Parts[1];
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Part
        {
            public Int32 PartID;
            public Int32 Texture;
        };

        private Dictionary<Int32, IntPtr> Entries = null;

        /// <summary>
        /// Create a new SIMO instance to handle the TQ's SIMO file.
        /// </summary>
        public SIMO()
        {
            this.Entries = new Dictionary<Int32, IntPtr>();
        }

        ~SIMO()
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
        /// Load the specified SIMO file (in binary format) into the dictionary.
        /// </summary>
        public void LoadFromDat(String Path)
        {
            Clear();

            lock (Entries)
            {
                using (FileStream Stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Byte[] Buffer = new Byte[Kernel.MAX_BUFFER_SIZE];
                    Header* pHeader = (Header*)Kernel.malloc(sizeof(Header));

                    Stream.Read(Buffer, 0, sizeof(Header));
                    Kernel.memcpy(pHeader, Buffer, sizeof(Header));

                    if (pHeader->Identifier != SIMO_IDENTIFIER)
                    {
                        Kernel.free(pHeader);
                        throw new Exception("Invalid SIMO Header in file: " + Path);
                    }

                    for (Int32 i = 0; i < pHeader->Amount; i++)
                    {
                        Stream.Read(Buffer, 0, sizeof(Int32) * 2);

                        Int32 UniqId = 0;
                        Int32 Amount = 0;
                        fixed (Byte* pBuffer = Buffer)
                        {
                            UniqId = *((Int32*)pBuffer);
                            Amount = *((Int32*)pBuffer + 1);
                        }

                        Int32 Length = (sizeof(Entry)) + (Amount * sizeof(Part));

                        Entry* pEntry = (Entry*)Kernel.malloc(Length);
                        Stream.Read(Buffer, 0, Length - sizeof(Entry));
                        Kernel.memcpy(pEntry->Parts, Buffer, Length - sizeof(Entry));

                        pEntry->UniqId = UniqId;
                        pEntry->Amount = Amount;

                        if (!Entries.ContainsKey(pEntry->UniqId))
                            Entries.Add(pEntry->UniqId, (IntPtr)pEntry);
                    }
                    Kernel.free(pHeader);
                }
            }
        }

        /// <summary>
        /// Load the specified SIMO file (in plain format) into the dictionary.
        /// TODO: The function is really slow... A good optimization is needed!
        /// </summary>
        public void LoadFromTxt(String Path)
        {
            Clear();

            lock (Entries)
            {
                Ini Ini = new Ini(Path);
                using (StreamReader Stream = new StreamReader(Path, Encoding.GetEncoding("Windows-1252")))
                {
                    String Line = null;
                    while ((Line = Stream.ReadLine()) != null)
                    {
                        if (Line.StartsWith("["))
                        {
                            Int32 UniqId = Int32.Parse(Line.Substring("[ObjIDType".Length, Line.IndexOf("]") - "[ObjIDType".Length));
                            Int32 Amount = Ini.ReadInt32("ObjIDType" + UniqId, "PartAmount");

                            Int32 Length = sizeof(Entry) + Amount * sizeof(Part);
                            Entry* pEntry = (Entry*)Kernel.malloc(Length);

                            pEntry->UniqId = UniqId;
                            pEntry->Amount = Amount;

                            for (Int32 i = 0; i < Amount; i++)
                            {
                                ((Part*)pEntry->Parts)[i].PartID = Ini.ReadInt32("ObjIDType" + UniqId, "Part" + i.ToString());
                                ((Part*)pEntry->Parts)[i].Texture = Ini.ReadInt32("ObjIDType" + UniqId, "Texture" + i.ToString());
                            }

                            if (!Entries.ContainsKey(pEntry->UniqId))
                                Entries.Add(pEntry->UniqId, (IntPtr)pEntry);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified SIMO file (in binary format).
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

                Header* pHeader = (Header*)Kernel.malloc(sizeof(Header));
                pHeader->Identifier = SIMO_IDENTIFIER;
                pHeader->Amount = Pointers.Length;

                Kernel.memcpy(Buffer, pHeader, sizeof(Header));
                Stream.Write(Buffer, 0, sizeof(Header));

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Int32 Length = sizeof(Entry) + (((Entry*)Pointers[i])->Amount * sizeof(Part));
                    Kernel.memcpy(Buffer, (Entry*)Pointers[i], Length);
                    Stream.Write(Buffer, 0, Length - 1);
                }
                Kernel.free(pHeader);
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified SIMO file (in plain format).
        /// </summary>
        public void SaveToTxt(String Path)
        {
            using (StreamWriter Stream = new StreamWriter(Path, false, Encoding.GetEncoding("Windows-1252")))
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

                    Stream.WriteLine("[ObjIDType{0}]", pEntry->UniqId);
                    Stream.WriteLine("PartAmount={0}", pEntry->Amount);
                    for (Int32 x = 0; x < pEntry->Amount; x++)
                    {
                        Stream.WriteLine("Part{0}={1}", x, ((Part*)pEntry->Parts)[x].PartID);
                        Stream.WriteLine("Texture{0}={1}", x, ((Part*)pEntry->Parts)[x].Texture);
                    }
                    Stream.WriteLine();
                }
            }
        }
    }
}

// * ************************************************************
// * * END:                                             simo.cs *
// * ************************************************************