using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Text.Json;


public class YandexDisk {

    HttpClient client = new HttpClient();

    // var localPath = "/Users/denis/Documents/1321803893_vasnecov-appolinariy-mihaylovich-vsehsvyatskiy-kamennyy-most.-moskva-konca-xvii-veka.jpeg";
    // var path="SVEMA/sample_a.jpeg";
    // //var authToken = GetToken(5319142);
    // var authToken = Environment.GetEnvironmentVariable("AUTH_TOKEN");
    // var uploadRl = GetUploadUrl(path, authToken);
    // PutFile(uploadRl, localPath);
    // Console.WriteLine(GetDownloadUrl(path, authToken));
    // GetFile(GetDownloadUrl(path, authToken));
    // DeleteFile(path, authToken);

    string GetAuthString() {
        var authString = Environment.GetEnvironmentVariable("CLIENT_ID")+":"+Environment.GetEnvironmentVariable("CLIENT_SECRET");
        var authStringBytes = System.Text.Encoding.UTF8.GetBytes(authString);
        authString = System.Convert.ToBase64String(authStringBytes);
        return authString;
    }

    string GetToken (int code) {
        client.DefaultRequestHeaders.Host = "oauth.yandex.ru";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", GetAuthString());
        var contentString = "grant_type=authorization_code&code=" + code;
        HttpContent content = new StringContent(contentString);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        content.Headers.ContentLength = contentString.Length;
        Task<HttpResponseMessage> response = client.PostAsync("https://oauth.yandex.ru/token", content);
        var text = response.Result.Content.ReadAsStringAsync().Result;
        var stream = response.Result.Content.ReadAsStreamAsync().Result;
        AuthResponse authResponse = JsonSerializer.Deserialize<AuthResponse>(stream);
        return authResponse.access_token;
    }

    string GetDownloadUrl(string path, string authToken) {
        Console.WriteLine(">>> GetDownloadUrl");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", authToken);
        Task<HttpResponseMessage> response = client.GetAsync("https://cloud-api.yandex.net/v1/disk/resources/download?path=" + path);    
        var text = response.Result.Content.ReadAsStringAsync().Result;
        var stream = response.Result.Content.ReadAsStreamAsync().Result;
        DiskResponse uploadResponse = JsonSerializer.Deserialize<DiskResponse>(stream);
        return uploadResponse.href;  
    }

    string GetUploadUrl(string path, string authToken) {
        Console.WriteLine(">>> GetUploadUrl");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", authToken);
        Task<HttpResponseMessage> response = client.GetAsync("https://cloud-api.yandex.net/v1/disk/resources/upload?path=" + path + "&overwrite=true");    
        var text = response.Result.Content.ReadAsStringAsync().Result;
        var stream = response.Result.Content.ReadAsStreamAsync().Result;
        DiskResponse uploadResponse = JsonSerializer.Deserialize<DiskResponse>(stream);
        return uploadResponse.href;  
    }

    async void PutFile(string url, string localPath) {
        Console.WriteLine(">>> PutFile");
        HttpContent content = new StreamContent(System.IO.File.OpenRead(localPath));
        HttpResponseMessage response = await client.PutAsync(url, content);
    }

    async void PutFileStream(string url, Stream stream) {
        Console.WriteLine(">>> PutFile");
        HttpContent content = new StreamContent(stream);
        HttpResponseMessage response = await client.PutAsync(url, content);
    }    

    void DeleteFile(string path, string authToken) {
        Console.WriteLine(">>> DeleteFile");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", authToken);
        Task<HttpResponseMessage> response = client.DeleteAsync("https://cloud-api.yandex.net/v1/disk/resources?path=" + path + "&permanently=true");    
        var text = response.Result.Content.ReadAsStringAsync().Result;
    }

    Stream GetFile(string url) {
        Console.WriteLine(">>> GetFile");
        Task<HttpResponseMessage> response = client.GetAsync(url);
        Task<byte[]> resultBytes = response.Result.Content.ReadAsByteArrayAsync();
        return response.Result.Content.ReadAsStream();
    }

    public Stream GetFileByPath(string path, string auth_token) {
        var url = GetDownloadUrl(path, auth_token);
        return GetFile(url);
    }

    public void PutFileByPath(string path, string auth_token, Stream stream) {
        var url = GetUploadUrl(path, auth_token);
        PutFileStream(url, stream);
    }

    public void DeleteFileByPath(string path, string auth_token) {
        DeleteFile(path, auth_token);
    }

}

class DiskResponse {
    public string operation_id {get; set;}
    public string href {get; set;}
    public string method {get; set;}
    public bool templated {get; set;}
}

class AuthResponse {
    public string access_token {get; set;}
    public string expires_in {get; set;}
    public string refresh_token {get; set;}
    public string token_type {get; set;}
}
