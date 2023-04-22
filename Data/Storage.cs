using System;
using System.IO;
using Data;

public class Storage {

    public static void StoreShot(Shot shot, byte[] data) {
        Console.WriteLine("!!!!STORE_SHOT to " + shot.SourceUri);
        if (shot.Storage == null) {
            Console.Write("Storage not defined for " + shot);
            return;
        } else if (shot.Storage.Provider == "LocalDisk") {
            System.IO.File.WriteAllBytes(shot.Storage.Root + shot.SourceUri, data);                
        } else {
            YandexDisk yandexDisk = new YandexDisk();
            yandexDisk.PutFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken, new MemoryStream(data));
        }
    }

    public static Stream GetFile(Shot shot) {
        if (shot.Storage == null) {
            Console.Write("Storage not defined for " + shot);
            return null;
        } else if (shot.Storage.Provider == "LocalDisk") {
            var stream = System.IO.File.OpenRead(shot.Storage.Root + shot.SourceUri);
            stream.Position = 0;
            return stream;
        } else {
            YandexDisk yandexDisk = new YandexDisk();
            var stream = yandexDisk.GetFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
            stream.Position = 0;
            return stream;
        }               
    }

    public static void DeleteFile(Shot shot) {
        if (shot.Storage == null) {
            Console.Write("Storage not defined for " + shot);
            return;
        } else if (shot.Storage.Provider == "LocalDisk") {
            try {
                System.IO.File.Delete(shot.Storage.Root + shot.SourceUri);
            } catch (Exception e) {
                Console.Write("Error " + e);
            }
        } else {
            YandexDisk yandexDisk = new YandexDisk();
            yandexDisk.DeleteFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
        }    
    }

}
