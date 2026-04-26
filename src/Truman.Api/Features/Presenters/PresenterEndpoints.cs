using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Truman.Data;
using Truman.Data.Entities;

namespace Truman.Api.Features.Presenters;

public static class PresenterEndpoints
{
    public static void MapPresenterEndpoints(this WebApplication app)
    {
        var publicGroup = app.MapGroup("/api/presenters")
            .WithTags("Presenters");

        publicGroup.MapGet("/available", async (TrumanDbContext db) =>
        {
            var options = await db.Presenters
                .Where(p => db.ArticlePresenters.Any(ap => ap.PresenterId == p.Id))
                .OrderBy(p => p.Label)
                .Select(p => new PresenterOptionDto(p.Id, p.Label))
                .ToListAsync();
            return Results.Ok(options);
        });

        var group = app.MapGroup("/api/admin/presenters")
            .WithTags("Admin / Presenters")
            .RequireAuthorization("RequireAdmin");

        group.MapGet("/", async (TrumanDbContext db) =>
        {
            var presenters = await db.Presenters
                .OrderBy(p => p.Label)
                .Select(p => new PresenterDto(p.Id, p.Label, p.PresenterStyle))
                .ToListAsync();
            return Results.Ok(presenters);
        });

        group.MapPost("/", async (
            [FromBody] CreatePresenterRequest request,
            TrumanDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Label))
            {
                return Results.BadRequest("Label is required");
            }
            if (string.IsNullOrWhiteSpace(request.PresenterStyle))
            {
                return Results.BadRequest("PresenterStyle is required");
            }

            var presenter = new Presenter
            {
                Label = request.Label.Trim(),
                PresenterStyle = request.PresenterStyle.Trim()
            };

            db.Presenters.Add(presenter);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                return Results.Conflict($"A presenter with style '{presenter.PresenterStyle}' already exists");
            }

            return Results.Created($"/api/admin/presenters/{presenter.Id}",
                new PresenterDto(presenter.Id, presenter.Label, presenter.PresenterStyle));
        });

        group.MapPatch("/{id:int}", async (
            int id,
            [FromBody] UpdatePresenterRequest request,
            TrumanDbContext db) =>
        {
            var presenter = await db.Presenters.FindAsync(id);
            if (presenter is null) return Results.NotFound();

            if (request.Label is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Label))
                {
                    return Results.BadRequest("Label cannot be empty");
                }
                presenter.Label = request.Label.Trim();
            }

            if (request.PresenterStyle is not null)
            {
                if (string.IsNullOrWhiteSpace(request.PresenterStyle))
                {
                    return Results.BadRequest("PresenterStyle cannot be empty");
                }
                presenter.PresenterStyle = request.PresenterStyle.Trim();
            }

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                return Results.Conflict($"A presenter with style '{presenter.PresenterStyle}' already exists");
            }

            return Results.Ok(new PresenterDto(presenter.Id, presenter.Label, presenter.PresenterStyle));
        });

        group.MapDelete("/{id:int}", async (int id, TrumanDbContext db) =>
        {
            var presenter = await db.Presenters.FindAsync(id);
            if (presenter is null) return Results.NotFound();

            db.Presenters.Remove(presenter);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
