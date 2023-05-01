using System.Collections.Generic;
using Data;
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Form;

public class AlbumsListDTO {
    public DateTime DateStart {get; set;}
    public DateTime DateEnd {get; set;}
    public ICollection<Location> Locations {get; set;}
    
    public ICollection<Album> Albums {get; set;}

}

public class AlbumDTO {
    public int AlbumId {get; set;}
    public int UserId {get; set;}
    public string Name {get; set;}
    public int LocationId {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateStart {get; set;}
    [BindProperty, DataType(DataType.Date)] 
    public DateTime DateEnd {get; set;}    
    public ICollection<AlbumComment> AlbumComments {get; set;}
    public ICollection<Location> Locations {get; set;}
    public List<ShotPreviewDTO> Shots {get; set;}
}

public class ShotPreviewDTO {

    public int ShotId {get; set;}
    public string Name {get; set;}
    public bool IsChecked {get; set;}

    public ShotPreviewDTO () {
    }    

    public ShotPreviewDTO (Shot shot) {
        ShotId = shot.ShotId;
        Name = shot.Name;
        IsChecked = false;
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
    public byte[] Data {get; set;}

    public string Mime {get; set;}

    public ShotREST() {

    }

    public ShotREST(Shot shot) {
        ShotId = shot.ShotId;
        AlbumId = shot.AlbumId;
        Name = shot.Name;
        UserId = shot.Album.User.UserId;
        DateStart = shot.DateStart;
        DateEnd = shot.DateEnd;
        Mime = shot.ContentType;;
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

    public ShotDTO() {

    }    

    public ShotDTO(Shot shot) {
        ShotId = shot.ShotId;
        Name = shot.Name;
        AlbumId = shot.AlbumId;
        DateStart = shot.DateStart;
        DateEnd = shot.DateEnd;
        Preview = shot.Preview;
        LocationId = shot.LocationId;
        ShotComments = shot.ShotComments;
    }

}