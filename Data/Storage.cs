using System.IO;
using svema.Data;
using Microsoft.AspNetCore.Mvc;

public class Storage {

    public void StoreShot(Shot shot, byte[] data) {
        if (shot.Storage.Provider == "LocalDisk") {
            System.IO.File.WriteAllBytes(shot.Storage.Root + shot.SourceUri, data);                
        } else {
            YandexDisk yandexDisk = new YandexDisk();
            yandexDisk.PutFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken, new MemoryStream(data));
        }
    }

    public Stream GetFile(Shot shot) {
        if (shot.Storage.Provider == "LocalDisk") {
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

    public void DeleteFile(Shot shot) {
        if (shot.Storage.Provider == "LocalDisk") {
            System.IO.File.Delete(shot.Storage.Root + shot.SourceUri);
        } else {
            YandexDisk yandexDisk = new YandexDisk();
            yandexDisk.DeleteFileByPath(shot.Storage.Root + shot.SourceUri, shot.Storage.AuthToken);             
        }    
    }

}
