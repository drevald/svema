using System;
using System.IO;
using Data;
using Common;
using System.Threading.Tasks;

public class Storage {

    public static async Task StoreShot(Shot shot, byte[] data) {
        Console.WriteLine("!!!!STORE_SHOT to " + shot.SourceUri);
        try {
            if (shot.Storage == null) {
                Console.Write("Storage not defined for " + shot);
                return;
            } else if (shot.Storage.Provider == Provider.Local) {
                String path = shot.Storage.Root + shot.SourceUri;
                String folder = Path.GetDirectoryName(path);
                Directory.CreateDirectory(folder);        
                Console.WriteLine("!!!storing locally to " + path);
                await Task.Run(() => System.IO.File.WriteAllBytes(path, data));
            } else {
                YandexDisk yandexDisk = new YandexDisk();
                await Task.Run(() => yandexDisk.PutFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken, new MemoryStream(data)));
            }
        } catch (Exception e) {
            Console.Write("Error " + e);
        }
    }

    public static async Task<Stream> GetFile(Shot shot)
    {
        try
        {
            if (shot.Storage == null)
            {
                Console.Write("Storage not defined for " + shot);
                return null;
            }

            if (shot.Storage.Provider == Provider.Local)
            {
                var stream = System.IO.File.OpenRead(shot.Storage.Root + shot.SourceUri);
                stream.Position = 0;
                return stream;
            }
            else
            {
                YandexDisk yandexDisk = new YandexDisk();
                var stream = await Task.Run(() =>
                    yandexDisk.GetFileByPath(
                        shot.Storage.Root + shot.SourceUri,
                        shot.Storage.AuthToken
                    )
                );

                stream.Position = 0;
                return stream;
            }
        }
        catch (Exception e)
        {
            Console.Write("Error " + e);
            return null;
        }
    }
    public static async Task DeleteFile(Shot shot) {
        try {
            if (shot.Storage == null) {
                Console.Write("Storage not defined for " + shot);
                return;
            } else if (shot.Storage.Provider == Provider.Local) {
                try {
                    await Task.Run(() => System.IO.File.Delete(shot.Storage.Root + shot.SourceUri));
                } catch (Exception e) {
                    Console.Write("Error " + e);
                }
            } else {
                YandexDisk yandexDisk = new YandexDisk();
                yandexDisk.DeleteFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
                await Task.Run(() => yandexDisk.DeleteFileByPath(
                    shot.Storage.Root + shot.SourceUri,
                    shot.Storage.AuthToken
                ));                
            }    
        } catch (Exception e) {
            Console.Write("Error " + e);
        }
    }

    public static async Task DeleteFileAsync(ShotStorage storage, string uri)
    {
        try
        {
            if (storage == null)
            {
                Console.Write("Storage not defined for shot");
                return;
            }
            else if (storage.Provider == Provider.Local)
            {
                try
                {
                    await Task.Run(() => System.IO.File.Delete(storage.Root + uri));
                }
                catch (Exception e)
                {
                    Console.Write("Error " + e);
                }
            }
            else
            {
                YandexDisk yandexDisk = new YandexDisk();
                await Task.Run(() => yandexDisk.DeleteFileByPath(storage.Root + uri, storage.AuthToken));
            }
        }
        catch (Exception e)
        {
            Console.Write("Error " + e);
        }
    }

}
