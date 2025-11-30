using Form;
using Npgsql;
using Data;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Linq;
using System;
using System.Globalization;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Utils;
using Common;
using Services;


namespace Services;

public class CommentService : Service
{

    public CommentService(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public void AddComment(string username, string text, int id, int commentId)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.Username == username);

        if (commentId == 0)
        {
            var comment = new AlbumComment
            {
                Author = user,
                AuthorId = user.UserId,
                AuthorUsername = user.Username,
                Text = text,
                AlbumId = id,
                Timestamp = DateTime.Now
            };
            dbContext.AlbumComments.Add(comment);
        }
        else
        {
            var comment = dbContext.AlbumComments.Find(commentId);
            if (comment == null) return;
            comment.Text = text;
            comment.AlbumId = id;
            comment.Timestamp = DateTime.Now;
            dbContext.AlbumComments.Update(comment);
        }
        dbContext.SaveChanges();
    }

    public List<ShotComment> GetShotComments(int id)
    {
        return dbContext.ShotComments.Where(s => s.ShotId == id).ToList();
    }

    public void DeleteShotComment(int id)
    {
        var comment = dbContext.ShotComments.Find(id);
        if (comment != null)
        {
            dbContext.ShotComments.Remove(comment);
            dbContext.SaveChanges();
        }
    }

    public void AddShotComment(string username, string text, int shotId, int commentId)
    {
        var user = dbContext.Users.FirstOrDefault(u => u.Username == username);
        if (user == null) return;

        if (commentId == 0)
        {
            var comment = new ShotComment
            {
                Author = user,
                AuthorId = user.UserId,
                AuthorUsername = user.Username,
                Text = text,
                ShotId = shotId,
                Timestamp = DateTime.Now
            };
            dbContext.ShotComments.Add(comment);
        }
        else
        {
            var comment = dbContext.ShotComments.Find(commentId);
            if (comment == null) return;
            comment.Text = text;
            comment.ShotId = shotId;
            comment.Timestamp = DateTime.Now;
            dbContext.ShotComments.Update(comment);
        }
        dbContext.SaveChanges();
    }

    public void DeleteAlbumComment(int commentId)
    {
        var comment = dbContext.AlbumComments.Find(commentId);
        if (comment != null)
        {
            dbContext.AlbumComments.Remove(comment);
            dbContext.SaveChanges();
        }
    }

    public void AddShotCommentForApi(int userId, int shotId, string text)
    {
        var comment = new ShotComment
        {
            Text = text,
            ShotId = shotId,
            AuthorId = userId,
            Timestamp = DateTime.Now
        };
        dbContext.ShotComments.Add(comment);
        dbContext.SaveChanges();
    }

}
