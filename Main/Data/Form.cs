using System.Collections.Generic;
using Data;
using System;
using Common;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace Form;


public class AlbumViewDTO
{
    public int AlbumId { get; set; }
    public string Name { get; set; }

    // Locations for the map
    public List<LocationDTO> Locations { get; set; } = new();

    // Shots / photos in the album
    public List<ShotDTO> Shots { get; set; } = new();

    // Comments for the album
    public List<CommentDTO> AlbumComments { get; set; } = new();
}

public class CommentDTO
{
    public int Id { get; set; }
    public string AuthorUsername { get; set; }
    public DateTime Timestamp { get; set; }
    public string Text { get; set; }
}

public class AddCommentDto
{
    public string Caption { get; set; }
}
public class AlbumCardDTO
{
    public int AlbumId { get; set; }
    public String Name { get; set; }
    public int PreviewId { get; set; }
    public bool PreviewFlip { get; set; }
    public int PreviewRotate { get; set; }
    public int Size { get; set; }
    public bool IsSelected { get; set; }
    public bool IsChecked {get; set;}
}

public class AlbumsListDTO {
    public String DateStart {get; set;}
    public String DateEnd {get; set;}
    public int LocationId {get; set;}
    public string Camera {get; set;}
    public ICollection<Location> Locations {get; set;}
    public List<AlbumCardDTO> Albums {get; set;}
    public ICollection<LocationDTO> Placemarks {get; set;}
    public ICollection<string> Cameras {get; set;}
    [Required]
    [Range(-90, 90)]
    public double North {get; set;}
    [Required]
    [Range(-90, 90)]
    public double South {get; set;}
    [Required]
    [Range(-180, 180)]
    public double East {get; set;}
    [Required]
    [Range(-180, 180)]
    public double West {get; set;}
    public SortBy SortBy {get; set;}
    public List<SelectListItem> SortByOptions { get; set; }
    public SortDirection SortDirection {get; set;}
    public List<SelectListItem> SortDirectionOptions { get; set; }
    public Boolean EditLocation { get; set; }
    public double Longitude {get; set;}
    public double Latitude {get; set;}
    public int Zoom {get; set;}
    public String LocationName { get; set; }
    public AlbumsListDTO()
    {
        Albums = new List<AlbumCardDTO>();
        North = 90;
        South = -90;
        West = -180;
        East = 180;
    }

}


public class SharedLinkDTO
{
    public SharedLink SharedLink { get; set; }
    public string BaseUrl { get; set; }
    public string LinkWithToken { get; set; }

    public SharedLinkDTO()
    {

    }

    public SharedLinkDTO(SharedLink sharedLink)
    {
        SharedLink = sharedLink;
    }
}

public class AlbumDTO
{
    public int AlbumId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; }
    public int Year { get; set; }
    public int LocationId { get; set; }
    [BindProperty, DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}")]
    public DateTime DateStart { get; set; }
    [BindProperty, DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:dd-MM-yyyy}")]
    public DateTime DateEnd { get; set; }
    public ICollection<AlbumComment> AlbumComments { get; set; }
    public ICollection<Location> Locations { get; set; }
    public ICollection<LocationDTO> Placemarks { get; set; }
    public List<ShotPreviewDTO> Shots { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public int Zoom { get; set; }
    public String LocationName { get; set; }
    [Required]
    [Range(-90, 90)]
    public double North { get; set; }
    [Required]
    [Range(-90, 90)]
    public double South { get; set; }
    [Required]
    [Range(-180, 180)]
    public double East { get; set; }
    [Required]
    [Range(-180, 180)]
    public double West { get; set; }
    public string Token { get; set; }
    public AlbumDTO()
    {
        North = 90;
        South = -90;
        West = -180;
        East = 180;
    }

}

public class ShotPreviewDTO {

    public int ShotId {get; set;}
    public string Name {get; set;}
    public bool IsChecked {get; set;}
    public string SourceUri {get; set;}
    public int Rotate {get; set;}
    public bool Flip {get; set;}

    public ShotPreviewDTO () {
    }    

    public ShotPreviewDTO (Shot shot) {
        ShotId = shot.ShotId;
        Name = shot.Name;
        IsChecked = false;
        SourceUri = shot.SourceUri;
        Flip = shot.Flip;
        Rotate = shot.Rotate;
    }

}

public class ShotREST {
    public int ShotId {get; set;}
    public int AlbumId {get; set;}
    public string Name {get; set;}
    public int UserId {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateStart {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateEnd {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateUploaded {get; set;}
    public byte[] Data {get; set;}
    public string Mime {get; set;}  
    public string OrigPath {get; set;}  
    public ShotREST() {

    }

    public ShotREST(Shot shot) {
        ShotId = shot.ShotId;
        AlbumId = shot.AlbumId;
        Name = shot.Name;
        UserId = shot.Album.User.UserId;
        DateStart = shot.DateStart;
        DateEnd = shot.DateEnd;
        DateUploaded = shot.DateUploaded;
        OrigPath = shot.OrigPath;
        Mime = shot.ContentType;
    }

}

public class ShotDTO {

    public int ShotId {get; set;}
    public string Name {get; set;}
    public int AlbumId {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateStart {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateEnd {get; set;}
    public byte[] Preview {get; set;}
    public System.Nullable<int> LocationId {get; set;}    
    public Location Location {get; set;}    
    public ICollection<ShotComment> ShotComments {get; set;}     
    public ICollection<Location> Locations {get; set;} 
    public bool IsCover {get; set;}
    public double Longitude {get; set;}
    public double Latitude {get; set;}
    public int Zoom {get; set;}
    public String LocationName {get; set;}
    public bool Flip {get; set;}
    public int Rotate {get; set;}
    public String Token { get; set; }
    public Album Album { get; set; }
    public String CameraModel { get; set; }
    public String CameraManufacturer { get; set; }
    public String OrigPath { get; set; }
    public ShotDTO()
    {

    }    

    public ShotDTO(Shot shot) {
        ShotId = shot.ShotId;
        Album = shot.Album;
        Name = shot.Name;
        AlbumId = shot.AlbumId;
        DateStart = shot.DateStart;
        DateEnd = shot.DateEnd;
        Preview = shot.Preview;
        ShotComments = shot.ShotComments;
        Flip = shot.Flip;
        Rotate = shot.Rotate;
        OrigPath = shot.OrigPath;
        CameraManufacturer = shot.CameraManufacturer;
        CameraModel = shot.CameraModel;
    }

}

public class LoginDTO : DTO {

    public string Username {get; set;}
    public string Password {get; set;}

}

public class UploadedFilesDTO : DTO {

    public int AlbumId {get; set;}
    public List<IFormFile> Files {get; set;}
    public Dictionary<string, string> FileErrors {get; set;}

}

public class DTO {

    public string ErrorMessage {get; set;}

}

public class ProfileDTO : DTO {
    public List<ShotStorage> Storages {get; set;}
    public User User {get; set;}
}

public class StorageDTO : DTO {

    public ShotStorage Storage {get; set;}

}

public class LocationDTO : DTO {
    public string Label {get; set;}
    public double Longitude {get; set;}
    public double Latitude {get; set;}

}

public class SelectAlbumDTO {
    public int SourceAlbumId {get; set;}
    public int TargetAlbumId {get; set;}
    public List<AlbumCardDTO> Albums {get; set;}
    public List<ShotPreviewDTO> Shots {get; set;}

}
