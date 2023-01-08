// * ************************************************************
// * * START:                                        chatlog.cs *
// * ************************************************************

// * ************************************************************
// *                      INFORMATIONS
// * ************************************************************
// * ChatLog class for the library.
// * chatlog.cs
// * 
// * --
// *
// * Feel free to use this class in your projects, but don't
// * remove the header to keep the paternity of the class.
// * 
// * ************************************************************
// *                      CREDITS
// * ************************************************************
// * Originally created by CptSky (January 16th, 2012)
// * Copyright (C) 2012 CptSky
// *
// * ************************************************************

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CO2_CORE_DLL.IO
{
    /// <summary>
    /// ChatLog
    /// </summary>
    public unsafe class ChatLog
    {
        public const Int32 MAX_NAMESIZE = 0x10;
        public const Int32 MAX_SERVERSIZE = 0x100;
        public const Int32 MAX_TXTSIZE = 0x200;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ChatLogLine
        {
            public fixed Byte Sender[MAX_NAMESIZE];
            public fixed Byte Txt[MAX_TXTSIZE];
        };

        private List<IntPtr> Entries = null;
        private Byte* pName = null;
        private Byte* pServer = null;

        private Int32 NameLength = 0;
        private Int32 ServerLength = 0;

        public String GetName() { return Kernel.cstring(pName, MAX_NAMESIZE); }
        public String GetServer() { return Kernel.cstring(pServer, MAX_SERVERSIZE); }

        /// <summary>
        /// Create a new ChatLog instance to handle the TQ's ChatLog file.
        /// </summary>
        public ChatLog(String Name, String Server)
        {
            if (Name == null || Server == null)
                throw new NullReferenceException();

            this.Entries = new List<IntPtr>();
            this.pName = Name.ToPointer();
            this.pServer = Server.ToPointer();

            NameLength = Math.Min(Kernel.strlen(pName), MAX_NAMESIZE - 1);
            if (NameLength <= 1)
                NameLength = 2;

            ServerLength = Math.Min(Kernel.strlen(pServer), MAX_SERVERSIZE - 1);
        }

        ~ChatLog()
        {
            Clear();
            if (pName != null)
                Kernel.free(pName);
            if (pServer != null)
                Kernel.free(pServer);
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
                        Kernel.free((ChatLogLine*)Ptr);
                }
                Entries.Clear();
            }
        }

        /// <summary>
        /// Load the specified chatlog file (in binary format) into the dictionary.
        /// </summary>
        public void LoadFromDat(String Path)
        {
            Clear();

            lock (Entries)
            {
                using (FileStream Stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Byte[] Buffer = new Byte[Kernel.MAX_BUFFER_SIZE];

                    while (true)
                    {
                        if (sizeof(ChatLogLine) != Stream.Read(Buffer, 0, sizeof(ChatLogLine)))
                            break;

                        ChatLogLine* pInfo = (ChatLogLine*)Kernel.malloc(sizeof(ChatLogLine));

                        Kernel.memcpy(pInfo, Buffer, sizeof(ChatLogLine));
                        Decrypt(pInfo->Txt, MAX_TXTSIZE);

                        Entries.Add((IntPtr)pInfo);
                    }
                }
            }
        }

        /// <summary>
        /// Load the specified chatlog file (in custom plain format) into the dictionary.
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
                        LineC++;

                        String[] Parts = Line.Split('\t');
                        ChatLogLine* pInfo = (ChatLogLine*)Kernel.calloc(sizeof(ChatLogLine));

                        Byte[] Buffer = null;

                        try
                        {
                            Buffer = Encoding.UTF8.GetBytes(Parts[0]);
                            Kernel.memcpy(pInfo->Sender, Buffer, MAX_NAMESIZE);

                            Buffer = Encoding.UTF8.GetBytes(Parts[1]);
                            Kernel.memcpy(pInfo->Txt, Buffer, MAX_TXTSIZE);

                            Entries.Add((IntPtr)pInfo);
                        }
                        catch (Exception Exc)
                        {
                            Console.WriteLine("Error at line {0}.\n{1}", LineC, Exc);
                            Kernel.free(pInfo);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified chatlog file (in binary format).
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

                Byte* pTemp = stackalloc Byte[MAX_TXTSIZE];
                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    ChatLogLine* pInfo = (ChatLogLine*)Pointers[i];

                    Kernel.memcpy(Buffer, pInfo->Sender, MAX_NAMESIZE);
                    Stream.Write(Buffer, 0, MAX_NAMESIZE);

                    Kernel.memcpy(pTemp, pInfo->Txt, MAX_TXTSIZE);
                    Encrypt(pTemp, MAX_TXTSIZE);

                    Kernel.memcpy(Buffer, pTemp, MAX_TXTSIZE);
                    Stream.Write(Buffer, 0, MAX_TXTSIZE);
                }
            }
        }

        /// <summary>
        /// Save all the dictionary to the specified chatlog file (in custom plain format).
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

                for (Int32 i = 0; i < Pointers.Length; i++)
                {
                    ChatLogLine* pInfo = (ChatLogLine*)Pointers[i];

                    StringBuilder Builder = new StringBuilder(Kernel.MAX_BUFFER_SIZE);
                    Builder.Append(Kernel.cstring(pInfo->Sender, MAX_NAMESIZE) + "\t");
                    Builder.Append(Kernel.cstring(pInfo->Txt, MAX_TXTSIZE));
                    Stream.WriteLine(Builder.ToString());
                }
            }
        }

        /// <summary>
        /// Encrypt the data using the character's name and the server's name.
        /// </summary>
        private void Encrypt(Byte* pBuf, Int32 Length)
        {
            for (Int32 i = 0; i < Length; i++)
            {
                SByte cCharPass = (SByte)((0x05BAACFB / (i + 1) % 0x100) + pName[i % (NameLength - 1)] + pServer[i % (ServerLength - 1)]);
                pBuf[i] = (Byte)((SByte)pBuf[i] ^ cCharPass);
            }
        }

        /// <summary>
        /// Decrypt the data using the character's name and the server's name.
        /// </summary>
        private void Decrypt(Byte* pBuf, Int32 Length)
        {
            for (Int32 i = 0; i < Length; i++)
            {
                SByte cCharPass = (SByte)((0x05BAACFB / (i + 1) % 0x100) + pName[i % (NameLength - 1)] + pServer[i % (ServerLength - 1)]);
                pBuf[i] = (Byte)((SByte)pBuf[i] ^ cCharPass);
            }
        }

        /// <summary>
        /// Convert a string to an unsgiend 32 bits integer.
        /// This function is used to generate the ID of the folder that contains the chat logs.
        /// </summary>
        public static UInt32 String2ID(String Str)
        {
            #region ASM
            //Conquer_004C6781:                            ;<= Procedure Start

            //        mov     edx, dword ptr [esp+4]
            //        xor     eax, eax

            //Conquer_004C6787:

            //        mov     cl, byte ptr [edx]
            //        test    cl, cl
            //        je      Conquer_004C6798
            //        imul    eax, eax, 0x21
            //        movsx   ecx, cl
            //        add     eax, ecx
            //        inc     edx
            //        jmp     Conquer_004C6787

            //Conquer_004C6798:

            //        retn                                 ;<= Procedure End
            #endregion

            UInt32 eax = 0;
            SByte ecx = 0;
            for (Int32 edx = 0; edx < Str.Length; edx++)
            {
                eax = eax * 0x21;
                ecx = (SByte)Str[edx];
                eax = (UInt32)(eax + ecx);
            }
            return eax;
        }
    }
}

// * ************************************************************
// * * END:                                          chatlog.cs *
// * ************************************************************