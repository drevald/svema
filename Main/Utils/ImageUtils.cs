using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Utils;

public class PhotoMetadata
{
    public DateTime? CreationDate { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string CameraManufacturer { get; set; }
    public string CameraModel { get; set; }
}

public static class ImageUtils
{
    public static PhotoMetadata GetMetadata(byte[] imageData)
    {
        using var stream = new MemoryStream(imageData);
        var directories = ImageMetadataReader.ReadMetadata(stream);

        var metadata = new PhotoMetadata();

        // Creation Date (DateTimeOriginal from EXIF)
        var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (exif != null && exif.ContainsTag(ExifDirectoryBase.TagDateTimeOriginal)) {
            metadata.CreationDate = exif.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
        }

        metadata.CameraManufacturer = ifd0?.GetDescription(ExifDirectoryBase.TagMake);
        metadata.CameraModel = ifd0?.GetDescription(ExifDirectoryBase.TagModel);

        // GPS Coordinates
        var gps = directories.OfType<GpsDirectory>().FirstOrDefault();
        var location = gps?.GetGeoLocation();

        if (location != null && !location.IsZero) {
            metadata.Latitude = location.Latitude;
            metadata.Longitude = location.Longitude;
        }

        return metadata;

    }

    public static Stream GetTransformedImage(Stream originalStream, int rotate, bool flip) {
        // Load image from the stream
        using (var image = Image.Load(originalStream)) {
            // Rotate image if the "rotate" parameter is provided and non-zero
            if (rotate != 0) {
                image.Mutate(x => x.Rotate(rotate)); // Rotate by the given angle
                Console.WriteLine($"Rotated image by {rotate} degrees.");
            }

            // Flip image if the "flip" parameter is true
            if (flip) {
                image.Mutate(x => x.Flip(FlipMode.Horizontal)); // Flip horizontally
                Console.WriteLine("Flipped image horizontally.");
            }

            // Save the transformed image to a memory stream
            var stream = new MemoryStream();
            image.Save(stream, new JpegEncoder()); // Save as JPEG
            stream.Position = 0;
            return stream;
        }
    }



}

