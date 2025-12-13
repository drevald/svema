using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Services;

public class PythonFaceRecognitionClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PythonFaceRecognitionClient> _logger;
    private readonly string _serviceUrl;
    private readonly string _faceDetectionModel;

    public PythonFaceRecognitionClient(
        HttpClient httpClient,
        ILogger<PythonFaceRecognitionClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _serviceUrl = configuration.GetValue<string>("FaceRecognitionServiceUrl") ?? "http://localhost:5555";
        _faceDetectionModel = configuration.GetValue<string>("FaceDetectionModel") ?? "hog";
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<FaceDetectionResponse> DetectFacesAsync(byte[] imageData)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageData);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "image", "image.jpg");

            var response = await _httpClient.PostAsync($"{_serviceUrl}/detect?model={_faceDetectionModel}", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<FaceDetectionResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Python face detection service");
            return new FaceDetectionResponse { Faces = new List<DetectedFace>(), Count = 0 };
        }
    }

    public async Task<ClusteringResponse> ClusterFacesAsync(List<float[]> encodings)
    {
        try
        {
            var request = new { encodings = encodings };
            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_serviceUrl}/cluster", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ClusteringResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Python face clustering service");
            return new ClusteringResponse { Clusters = new List<int>(), UniqueClusters = 0 };
        }
    }
}

public class FaceDetectionResponse
{
    public List<DetectedFace> Faces { get; set; }
    public int Count { get; set; }
}

public class DetectedFace
{
    public FaceLocation Location { get; set; }
    public float[] Encoding { get; set; }
}

public class FaceLocation
{
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }
    public int Left { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ClusteringResponse
{
    public List<int> Clusters { get; set; }
    public int UniqueClusters { get; set; }
}
