using System;
using System.IO;
using Data;

public class Storage {

    public static void StoreShot(Shot shot, byte[] data) {
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
                System.IO.File.WriteAllBytes(path, data);                
            } else {
                YandexDisk yandexDisk = new YandexDisk();
                yandexDisk.PutFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken, new MemoryStream(data));
            }
        } catch (Exception e) {
            Console.Write("Error " + e);
        }
    }

    public static Stream GetFile(Shot shot) {
        try {
            if (shot.Storage == null) {
                Console.Write("Storage not defined for " + shot);
                return null;
            } else if (shot.Storage.Provider == Provider.Local) {
                var stream = System.IO.File.OpenRead(shot.Storage.Root + shot.SourceUri);
                stream.Position = 0;
                return stream;
            } else {
                YandexDisk yandexDisk = new YandexDisk();
                var stream = yandexDisk.GetFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
                stream.Position = 0;
                return stream;
            }               
        } catch (Exception e) {
            Console.Write("Error " + e);
            return null;
        }
    }

    public static void DeleteFile(Shot shot) {
        try {
            if (shot.Storage == null) {
                Console.Write("Storage not defined for " + shot);
                return;
            } else if (shot.Storage.Provider == Provider.Local) {
                try {
                    System.IO.File.Delete(shot.Storage.Root + shot.SourceUri);
                } catch (Exception e) {
                    Console.Write("Error " + e);
                }
            } else {
                YandexDisk yandexDisk = new YandexDisk();
                yandexDisk.DeleteFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
            }    
        } catch (Exception e) {
            Console.Write("Error " + e);
        }
    }

}
