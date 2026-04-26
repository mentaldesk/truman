using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Feeds;

public static class FeedEndpoints
{
    public static void MapFeedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/feeds")
            .WithTags("Admin / Feeds")
            .RequireAuthorization("RequireAdmin");

        group.MapGet("/", async (TrumanDbContext db) =>
        {
            var feeds = await db.Feeds
                .OrderBy(f => f.Name)
                .Select(f => new FeedDto(f.Id, f.Url, f.Name, f.IsEnabled))
                .ToListAsync();
            return Results.Ok(feeds);
        });

        group.MapPost("/", async (
            [FromBody] CreateFeedRequest request,
            TrumanDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Url) ||
                !Uri.TryCreate(request.Url, UriKind.Absolute, out _))
            {
                return Results.BadRequest("A valid absolute URL is required");
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest("Name is required");
            }

            var feed = new Feed
            {
                Url = request.Url.Trim(),
                Name = request.Name.Trim(),
                IsEnabled = request.IsEnabled
            };

            db.Feeds.Add(feed);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                return Results.Conflict($"A feed with URL '{feed.Url}' already exists");
            }

            return Results.Created($"/api/admin/feeds/{feed.Id}",
                new FeedDto(feed.Id, feed.Url, feed.Name, feed.IsEnabled));
        });

        group.MapPatch("/{id:int}", async (
            int id,
            [FromBody] UpdateFeedRequest request,
            TrumanDbContext db) =>
        {
            var feed = await db.Feeds.FindAsync(id);
            if (feed is null) return Results.NotFound();

            if (request.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest("Name cannot be empty");
                }
                feed.Name = request.Name.Trim();
            }

            if (request.IsEnabled.HasValue)
            {
                feed.IsEnabled = request.IsEnabled.Value;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new FeedDto(feed.Id, feed.Url, feed.Name, feed.IsEnabled));
        });

        group.MapDelete("/{id:int}", async (int id, TrumanDbContext db) =>
        {
            var feed = await db.Feeds.FindAsync(id);
            if (feed is null) return Results.NotFound();

            db.Feeds.Remove(feed);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
