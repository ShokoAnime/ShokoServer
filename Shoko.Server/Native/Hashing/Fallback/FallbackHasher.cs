using System;
using System.Security.Cryptography;
using NLog;
using System.Collections.Generic;

namespace Shoko.Server.Native.Hashing.Fallback
{
    public class FallbackHasher
    {
        public const int ED2K_CHUNK_SIZE = 9728000;

        public static void HashWorker(object f)
        {
            ThreadUnit tu = (ThreadUnit)f;
            long ed2kchunks;
            long lastpart;
            byte[] ED2KHashBuffer=null;
            Dictionary<HashTypes, byte[]> hashes = new Dictionary<HashTypes, byte[]>();
            MD4 md4=null;
            MD5 md5=null;
            SHA1 sha1=null;
            Crc32 crc32=null;
            long position = 0;
            if ((tu.WorkUnit.Types & HashTypes.ED2K) == HashTypes.ED2K)
            { 
                ed2kchunks = tu.FileSize / ED2K_CHUNK_SIZE;
                lastpart = tu.FileSize % ED2K_CHUNK_SIZE;
                if (lastpart > 0)
                    ed2kchunks++;
                ED2KHashBuffer = new byte[ed2kchunks << 4];
                md4 = MD4.Create();
            }
            if ((tu.WorkUnit.Types & HashTypes.MD5) == HashTypes.MD5)
            {
                md5 = MD5.Create();
            }
            if ((tu.WorkUnit.Types & HashTypes.SHA1) == HashTypes.SHA1)
            {
                sha1 = SHA1.Create();
            }
            if ((tu.WorkUnit.Types & HashTypes.CRC) == HashTypes.CRC)
            {
                crc32 = new Crc32();
            }
            do
            {
                tu.WorkerAutoResetEvent.WaitOne();
                if (tu.Abort)
                {
                    tu.MainAutoResetEvent.Set();
                    return;
                }

                int buffernumber = tu.BufferNumber;
                long size = tu.CurrentSize;
                tu.MainAutoResetEvent.Set();
                try
                {
               
                    if (size != 0)
                    {
                        long bufferpos = 0;
                        long current_md4 = position / ED2K_CHUNK_SIZE;
                        long till_md4 = (position + size) / ED2K_CHUNK_SIZE;
                        for (long x = current_md4; x <= till_md4; x++)
                        {
                            long init_size = ED2K_CHUNK_SIZE - (position % ED2K_CHUNK_SIZE);
                            if (init_size > size)
                                init_size = size;
                            if (init_size == 0)
                                break;
                            if ((tu.WorkUnit.Types & HashTypes.ED2K) == HashTypes.ED2K)
                                md4.TransformBlock(tu.Buffer[buffernumber], (int)bufferpos, (int)init_size, tu.Buffer[buffernumber], (int)bufferpos);
                            if ((tu.WorkUnit.Types & HashTypes.CRC) == HashTypes.CRC)
                                crc32.TransformBlock(tu.Buffer[buffernumber], (int)bufferpos, (int)init_size, tu.Buffer[buffernumber], (int)bufferpos);
                            if ((tu.WorkUnit.Types & HashTypes.MD5) == HashTypes.MD5)
                                md4.TransformBlock(tu.Buffer[buffernumber], (int)bufferpos, (int)init_size, tu.Buffer[buffernumber], (int)bufferpos);
                            if ((tu.WorkUnit.Types & HashTypes.SHA1) == HashTypes.SHA1)
                                sha1.TransformBlock(tu.Buffer[buffernumber], (int)bufferpos, (int)init_size, tu.Buffer[buffernumber], (int)bufferpos);
                            bufferpos += init_size;
                            position += init_size;
                            size -= init_size;
                            if (((tu.WorkUnit.Types & HashTypes.ED2K) == HashTypes.ED2K) && (position % ED2K_CHUNK_SIZE) == 0)
                            {
                                md4.TransformFinalBlock(tu.Buffer[buffernumber], (int)bufferpos, 0);
                                Array.Copy(md4.Hash, 0, ED2KHashBuffer, x * 16, 16);
                                md4= MD4.Create();
                            }
                        }
                    }
                    else
                    {
                        if ((tu.WorkUnit.Types & HashTypes.ED2K) == HashTypes.ED2K)
                        {
                            long x = position / ED2K_CHUNK_SIZE;
                            if ((position % ED2K_CHUNK_SIZE) != 0)
                            {
                                md4.TransformFinalBlock(tu.Buffer[buffernumber], 0, 0);
                                Array.Copy(md4.Hash, 0, ED2KHashBuffer, x * 16, 16);
                            }
                            if (position > ED2K_CHUNK_SIZE)
                            {
                                if ((position % ED2K_CHUNK_SIZE) > 0)
                                    x++;
                                md4 = MD4.Create();
                                md4.ComputeHash(ED2KHashBuffer, 0, (int)x*16);
                                hashes[HashTypes.ED2K] = md4.Hash;
                            }
                            else
                            {
                                hashes[HashTypes.ED2K] = new byte[16];
                                Array.Copy(ED2KHashBuffer, 0, hashes[HashTypes.ED2K], 0, 16);
                            }
                        }
                        if ((tu.WorkUnit.Types & HashTypes.CRC) == HashTypes.CRC)
                        {
                            crc32.TransformFinalBlock(tu.Buffer[buffernumber], 0, 0);
                            hashes[HashTypes.CRC] = crc32.Hash;
                        }
                        if ((tu.WorkUnit.Types & HashTypes.MD5) == HashTypes.MD5)
                        {
                            md5.TransformFinalBlock(tu.Buffer[buffernumber], 0, 0);
                            hashes[HashTypes.MD5] = md5.Hash;
                        }
                        if ((tu.WorkUnit.Types & HashTypes.SHA1) == HashTypes.SHA1)
                        {
                            sha1.TransformFinalBlock(tu.Buffer[buffernumber], 0, 0);
                            hashes[HashTypes.SHA1] = sha1.Hash;
                        }
                        tu.WorkUnit.Result = hashes;
                        tu.MainAutoResetEvent.Set();
                        return;
                    }
                }
                catch (Exception e)
                {
                    tu.WorkerError(e.Message);
                }
            } while (tu.CurrentSize != 0);
        }
    }
}