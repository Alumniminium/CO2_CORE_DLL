// * ************************************************************
// * * START:                                          matr.cs *
// * ************************************************************

// * ************************************************************
// *                      INFORMATIONS
// * ************************************************************
// * MATR class for the library.
// * matr.cs
// * 
// * --
// *
// * Feel free to use this class in your projects, but don't
// * remove the header to keep the paternity of the class.
// * 
// * ************************************************************
// *                      CREDITS
// * ************************************************************
// * Originally created by CptSky (January 22th, 2012)
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
    /// DBC / MATR
    /// Files: Material
    /// </summary>
    public unsafe class MATR
    {
        public const Int32 MAX_NAMESIZE = 0x20;

        private const Int32 MATR_IDENTIFIER = 0x5254414D;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Header
        {
            public Int32 Identifier;
            public Int32 Amount;
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Entry
        {
            public fixed Byte Name[MAX_NAMESIZE];
            public UInt32 Param0;
            public UInt32 Param1;
            public UInt32 Param2;
            public UInt32 Param3;
            public UInt32 Param4;
        };

        private List<IntPtr> Entries = null;

        /// <summary>
        /// Create a new MATR instance to handle the TQ's MATR file.
        /// </summary>
        public MATR()
        {
            this.Entries = new List<IntPtr>();
        }

        ~MATR()
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
                    foreach (IntPtr Ptr in Entries)
                        Kernel.free((Entry*)Ptr);
                }
                Entries.Clear();
            }
        }

        /// <summary>
        /// Load the specified MATR file (in binary format) into the dictionary.
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

                    if (pHeader->Identifier != MATR_IDENTIFIER)
                        throw new Exception("Invalid MATR Header in file: " + Path);

                    for (Int32 i = 0; i < pHeader->Amount; i++)
                    {
                        Entry* pEntry = (Entry*)Kernel.malloc(sizeof(Entry));
                        Stream.Read(Buffer, 0, sizeof(Entry));
                        Kernel.memcpy(pEntry, Buffer, sizeof(Entry));

                        Entries.Add((IntPtr)pEntry);
                    }
                }
            }
        }

        /// <summary>
        /// Load the specified MATR file (in plain format) into the dictionary.
        /// </summary>
        public void LoadFromTxt(String Path)
        {
            Clear();

            lock (Entries)
            {
                using (StreamReader Stream = new StreamReader(Path, Encoding.UTF8))
                {
                    String Line = null;
                    Int32 LineC = 0;
                    while ((Line = Stream.ReadLine()) != null)
                    {
                        if (LineC == 0) //material = X
                        {
                            LineC++;
                            continue;
                        }
                        LineC++;

                        String[] Parts = Line.Split(' ');
                        Entry* pEntry = (Entry*)Kernel.calloc(sizeof(Entry));

                        try
                        {
                            Byte* pName = stackalloc Byte[Parts[0].Length + 1];
                            Parts[0].ToPointer(pName);
                            Kernel.memcpy(pEntry->Name, pName, Math.Min(MAX_NAMESIZE - 1, Kernel.strlen(pName)));

                            pEntry->Param0 = UInt32.Parse(Parts[1], System.Globalization.NumberStyles.HexNumber);
                            pEntry->Param1 = UInt32.Parse(Parts[2], System.Globalization.NumberStyles.HexNumber);
                            pEntry->Param2 = UInt32.Parse(Parts[3], System.Globalization.NumberStyles.HexNumber);
                            pEntry->Param3 = UInt32.Parse(Parts[4], System.Globalization.NumberStyles.HexNumber);
                            pEntry->Param4 = UInt32.Parse(Parts[5], System.Globalization.NumberStyles.HexNumber);

                            Entries.Add((IntPtr)pEntry);
                        }
                        catch (Exception Exc)
                        {
                            Console.WriteLine("Error at line {0}.\n{1}", LineC, Exc);
                            Kernel.free(pEntry);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified MATR file (in binary format).
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
                    Entries.CopyTo(Pointers, 0);
                }

                Header* pHeader = stackalloc Header[1];
                pHeader->Identifier = MATR_IDENTIFIER;
                pHeader->Amount = Pointers.Length;

                Kernel.memcpy(Buffer, pHeader, sizeof(Header));
                Stream.Write(Buffer, 0, sizeof(Header));

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Kernel.memcpy(Buffer, (Entry*)Pointers[i], sizeof(Entry));
                    Stream.Write(Buffer, 0, sizeof(Entry));
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified MATR file (in plain format).
        /// </summary>
        public void SaveToTxt(String Path)
        {
            using (StreamWriter Stream = new StreamWriter(Path, false, Encoding.UTF8))
            {
                IntPtr[] Pointers = new IntPtr[0];

                lock (Entries)
                {
                    Pointers = new IntPtr[Entries.Count];
                    Entries.CopyTo(Pointers, 0);
                }

                Stream.WriteLine("material={0}", Pointers.Length);
                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    Entry* pEntry = (Entry*)Pointers[i];

                    StringBuilder Builder = new StringBuilder(Kernel.MAX_BUFFER_SIZE);
                    Builder.Append(Kernel.cstring(pEntry->Name, MAX_NAMESIZE) + " ");
                    Builder.Append(pEntry->Param0.ToString("X2") + " ");
                    Builder.Append(pEntry->Param1.ToString("X2") + " ");
                    Builder.Append(pEntry->Param2.ToString("X2") + " ");
                    Builder.Append(pEntry->Param3.ToString("X2") + " ");
                    Builder.Append(pEntry->Param4.ToString("X2"));
                    Stream.WriteLine(Builder.ToString());
                }
            }
        }
    }
}

// * ************************************************************
// * * END:                                             matr.cs *
// * ************************************************************